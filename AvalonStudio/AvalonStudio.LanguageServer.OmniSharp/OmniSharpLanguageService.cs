using AvalonStudio.Controls;
using AvalonStudio.Editor;
using AvalonStudio.Extensibility;
using AvalonStudio.Extensibility.Languages.CompletionAssistance;
using AvalonStudio.LanguageServer.OmniSharp.Protocol;
using AvalonStudio.Languages;
using AvalonStudio.Projects;
using AvalonStudio.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AvalonStudio.LanguageServer.OmniSharp
{
    /// <summary>
    /// ILanguageService implementation backed by an out-of-process OmniSharp LSP server.
    /// Supports: completions, hover, signature help, go-to-definition, references,
    /// rename, code actions, code lens, diagnostics, and document formatting.
    /// </summary>
    internal sealed class OmniSharpLanguageService : ILanguageService
    {
        // ─── Per-editor state ────────────────────────────────────────────────

        private ITextEditor _editor;
        private OmniSharpServerManager _server;
        private int _docVersion;

        // ─── ILanguageService ─────────────────────────────────────────────────

        public string LanguageId => "cs";

        public IDictionary<string, Func<string, string>> SnippetCodeGenerators { get; }
            = new Dictionary<string, Func<string, string>>
            {
                ["ToFieldName"] = s => string.IsNullOrEmpty(s) ? s : "_" + char.ToLower(s[0]) + s.Substring(1)
            };

        public IDictionary<string, Func<int, int, int, string>> SnippetDynamicVariables { get; }
            = new Dictionary<string, Func<int, int, int, string>>();

        public IEnumerable<ITextEditorInputHelper> InputHelpers { get; }
            = new ITextEditorInputHelper[]
            {
                new AutoBrackedInputHelper(),
                new CBasedLanguageIndentationInputHelper()
            };

        public IEnumerable<char> IntellisenseTriggerCharacters { get; } = new[] { '.', '<', ':' };
        public IEnumerable<char> IntellisenseSearchCharacters { get; } = new[] { '(', ')', '.', ':', '-', '>', ';', '<' };
        public IEnumerable<char> IntellisenseCompleteCharacters { get; } = new[] { '.', ':', ';', '-', ' ', '(', '=', '+', '*', '/', '%', '|', '&', '!', '^' };

        public bool IsValidIdentifierCharacter(char data) => char.IsLetterOrDigit(data) || data == '_';

        public bool CanTriggerIntellisense(char currentChar, char previousChar)
            => IntellisenseTriggerCharacters.Contains(currentChar)
               || (currentChar == ':' && previousChar == ':');

        // ─── Editor registration ──────────────────────────────────────────────

        public void RegisterEditor(ITextEditor editor)
        {
            _editor = editor;

            _server = OmniSharpServiceRegistry.GetOrCreateServer(editor.SourceFile.Project.Solution);

            _server.DiagnosticsPublished += OnDiagnosticsPublished;

            string uri = FilePathToUri(editor.SourceFile.Location);
            string text = editor.Document.Text;

            _server.EnsureStartedAsync(IoC.Get<IConsole>()).ContinueWith(_ =>
            {
                if (_server.Rpc != null)
                {
                    _server.Rpc.SendNotificationAsync("textDocument/didOpen", new DidOpenTextDocumentParams
                    {
                        TextDocument = new TextDocumentItem
                        {
                            Uri = uri,
                            LanguageId = "csharp",
                            Version = ++_docVersion,
                            Text = text
                        }
                    }).Forget();
                }
            });
        }

        public void UnregisterEditor()
        {
            if (_server?.Rpc != null && _editor != null)
            {
                _server.DiagnosticsPublished -= OnDiagnosticsPublished;

                _server.Rpc.SendNotificationAsync("textDocument/didClose", new DidCloseTextDocumentParams
                {
                    TextDocument = new TextDocumentIdentifier { Uri = FilePathToUri(_editor.SourceFile.Location) }
                }).Forget();
            }

            _editor = null;
        }

        // ─── Text synchronization ─────────────────────────────────────────────

        private void SyncDocument()
        {
            if (_server?.Rpc == null || _editor == null) return;

            _server.Rpc.SendNotificationAsync("textDocument/didChange", new DidChangeTextDocumentParams
            {
                TextDocument = new VersionedTextDocumentIdentifier
                {
                    Uri = FilePathToUri(_editor.SourceFile.Location),
                    Version = ++_docVersion
                },
                ContentChanges = new[]
                {
                    new TextDocumentContentChangeEvent { Text = _editor.Document.Text }
                }
            }).Forget();
        }

        // ─── Completions ──────────────────────────────────────────────────────

        public async Task<CodeCompletionResults> CodeCompleteAtAsync(int index, int line, int column,
            IEnumerable<UnsavedFile> unsavedFiles, char previousChar, string filter)
        {
            if (_server?.Rpc == null) return null;

            SyncDocument();

            var (lspLine, lspCol) = ToLspPosition(line, column);

            var completionList = await _server.Rpc.SendRequestAsync<CompletionList>(
                "textDocument/completion",
                new CompletionParams
                {
                    TextDocument = new TextDocumentIdentifier { Uri = FilePathToUri(_editor.SourceFile.Location) },
                    Position = new Position { Line = lspLine, Character = lspCol },
                    Context = new CompletionContext
                    {
                        TriggerKind = previousChar == '.' ? 2 : 1,
                        TriggerCharacter = previousChar != '\0' ? previousChar.ToString() : null
                    }
                }).ConfigureAwait(false);

            if (completionList == null) return null;

            var results = new CodeCompletionResults();
            var items = completionList.Items ?? Array.Empty<CompletionItem>();

            foreach (var item in items)
            {
                if (!string.IsNullOrWhiteSpace(filter) &&
                    !(item.Label?.StartsWith(filter, StringComparison.OrdinalIgnoreCase) ?? false))
                    continue;

                string insertionText = item.InsertText ?? item.Label;
                string displayText = item.Label;
                string filterText = item.FilterText ?? item.Label;

                var completion = new CodeCompletionData(displayText, filterText, insertionText, null,
                    CompletionItemSelectionBehavior.Default, 0)
                {
                    BriefComment = item.Detail,
                    Kind = FromLspCompletionKind(item.Kind)
                };

                results.Completions.Add(completion);
            }

            return results;
        }

        // ─── Quick info / hover ───────────────────────────────────────────────

        public async Task<QuickInfoResult> QuickInfo(IEnumerable<UnsavedFile> unsavedFiles, int offset)
        {
            if (_server?.Rpc == null) return null;

            SyncDocument();

            var (line, col) = OffsetToLspPosition(offset);

            var hover = await _server.Rpc.SendRequestAsync<HoverResult>(
                "textDocument/hover",
                new TextDocumentPositionParams
                {
                    TextDocument = new TextDocumentIdentifier { Uri = FilePathToUri(_editor.SourceFile.Location) },
                    Position = new Position { Line = line, Character = col }
                }).ConfigureAwait(false);

            if (hover?.Contents == null) return null;

            string text = ExtractHoverText(hover.Contents);
            if (string.IsNullOrWhiteSpace(text)) return null;

            var styledText = StyledText.Create();
            styledText.Append(text);
            return new QuickInfoResult(styledText);
        }

        // ─── Signature help ───────────────────────────────────────────────────

        public async Task<Extensibility.Languages.CompletionAssistance.SignatureHelp> SignatureHelp(
            IEnumerable<UnsavedFile> unsavedFiles, int offset, string methodName)
        {
            if (_server?.Rpc == null) return null;

            SyncDocument();

            var (line, col) = OffsetToLspPosition(offset);

            var result = await _server.Rpc.SendRequestAsync<SignatureHelpResult>(
                "textDocument/signatureHelp",
                new SignatureHelpParams
                {
                    TextDocument = new TextDocumentIdentifier { Uri = FilePathToUri(_editor.SourceFile.Location) },
                    Position = new Position { Line = line, Character = col }
                }).ConfigureAwait(false);

            if (result?.Signatures == null || result.Signatures.Length == 0) return null;

            var help = new Extensibility.Languages.CompletionAssistance.SignatureHelp(offset)
            {
                ActiveSignature = result.ActiveSignature ?? 0,
                ActiveParameter = result.ActiveParameter ?? 0,
            };

            foreach (var sig in result.Signatures)
            {
                var signature = new Signature
                {
                    Name = sig.Label,
                    Documentation = ExtractDocumentation(sig.Documentation),
                    Parameters = new List<Parameter>()
                };

                if (sig.Parameters != null)
                {
                    foreach (var param in sig.Parameters)
                    {
                        string paramLabel = param.Label?.Type == JTokenType.String
                            ? param.Label.Value<string>()
                            : sig.Label; // fallback to full label

                        signature.Parameters.Add(new Parameter
                        {
                            Name = paramLabel,
                            Documentation = ExtractDocumentation(param.Documentation),
                            Label = paramLabel
                        });
                    }
                }

                help.Signatures.Add(signature);
            }

            return help;
        }

        // ─── Goto definition ──────────────────────────────────────────────────

        public async Task<GotoDefinitionInfo> GotoDefinition(int offset)
        {
            if (_server?.Rpc == null) return null;

            SyncDocument();

            var (line, col) = OffsetToLspPosition(offset);

            var token = await _server.Rpc.SendRequestAsync(
                "textDocument/definition",
                new DefinitionParams
                {
                    TextDocument = new TextDocumentIdentifier { Uri = FilePathToUri(_editor.SourceFile.Location) },
                    Position = new Position { Line = line, Character = col }
                }).ConfigureAwait(false);

            if (token == null || token.Type == JTokenType.Null) return null;

            Location loc = null;
            if (token.Type == JTokenType.Array)
            {
                var arr = token.ToObject<Location[]>();
                loc = arr?.FirstOrDefault();
            }
            else
            {
                loc = token.ToObject<Location>();
            }

            if (loc == null) return null;

            return new GotoDefinitionInfo
            {
                FileName = UriToFilePath(loc.Uri),
                Line = (loc.Range?.Start?.Line ?? 0) + 1,  // 1-based
                Column = (loc.Range?.Start?.Character ?? 0) + 1
            };
        }

        // ─── Find references ──────────────────────────────────────────────────

        public async Task<List<Symbol>> GetSymbolsAsync(IEnumerable<UnsavedFile> unsavedFiles, string name)
        {
            if (_server?.Rpc == null) return null;

            SyncDocument();

            var (line, col) = OffsetToLspPosition(_editor.Offset);

            var refs = await _server.Rpc.SendRequestAsync<Location[]>(
                "textDocument/references",
                new ReferenceParams
                {
                    TextDocument = new TextDocumentIdentifier { Uri = FilePathToUri(_editor.SourceFile.Location) },
                    Position = new Position { Line = line, Character = col },
                    Context = new ReferenceContext { IncludeDeclaration = true }
                }).ConfigureAwait(false);

            if (refs == null) return null;

            return refs.Select(r => new Symbol
            {
                Name = Path.GetFileName(UriToFilePath(r.Uri)),
                Definition = $"{UriToFilePath(r.Uri)}:{(r.Range?.Start?.Line ?? 0) + 1}"
            }).ToList();
        }

        // ─── Rename ───────────────────────────────────────────────────────────

        public async Task<IEnumerable<SymbolRenameInfo>> RenameSymbol(string renameTo)
        {
            if (_server?.Rpc == null) return null;

            SyncDocument();

            var (line, col) = OffsetToLspPosition(_editor.Offset);

            var edit = await _server.Rpc.SendRequestAsync<WorkspaceEdit>(
                "textDocument/rename",
                new RenameParams
                {
                    TextDocument = new TextDocumentIdentifier { Uri = FilePathToUri(_editor.SourceFile.Location) },
                    Position = new Position { Line = line, Character = col },
                    NewName = renameTo
                }).ConfigureAwait(false);

            if (edit?.Changes == null) return Enumerable.Empty<SymbolRenameInfo>();

            var results = new List<SymbolRenameInfo>();
            foreach (var kv in edit.Changes)
            {
                string filePath = UriToFilePath(kv.Key);
                var info = new SymbolRenameInfo(filePath);
                info.Changes = kv.Value.Select(te => new LinePositionSpanTextChange
                {
                    StartLine = (te.Range?.Start?.Line ?? 0) + 1,
                    StartColumn = (te.Range?.Start?.Character ?? 0) + 1,
                    EndLine = (te.Range?.End?.Line ?? 0) + 1,
                    EndColumn = (te.Range?.End?.Character ?? 0) + 1,
                    NewText = te.NewText ?? ""
                }).ToList();
                results.Add(info);
            }

            return results;
        }

        // ─── Context actions ──────────────────────────────────────────────────

        public IEnumerable<IContextActionProvider> GetContextActionProviders()
        {
            return new[] { new OmniSharpContextActionProvider(this) };
        }

        internal async Task<IEnumerable<CodeFix>> GetCodeFixesAsync(int offset, int length, CancellationToken ct)
        {
            if (_server?.Rpc == null) return Enumerable.Empty<CodeFix>();

            SyncDocument();

            var (startLine, startCol) = OffsetToLspPosition(offset);
            var (endLine, endCol) = OffsetToLspPosition(offset + length);

            var actions = await _server.Rpc.SendRequestAsync<JToken>(
                "textDocument/codeAction",
                new CodeActionParams
                {
                    TextDocument = new TextDocumentIdentifier { Uri = FilePathToUri(_editor.SourceFile.Location) },
                    Range = new Protocol.Range
                    {
                        Start = new Position { Line = startLine, Character = startCol },
                        End = new Position { Line = endLine, Character = endCol }
                    },
                    Context = new CodeActionContext { Diagnostics = Array.Empty<LspDiagnostic>() }
                }, ct).ConfigureAwait(false);

            if (actions == null) return Enumerable.Empty<CodeFix>();

            var fixes = new List<CodeFix>();
            JArray arr = actions as JArray ?? (actions.Type == JTokenType.Array ? (JArray)actions : null);
            if (arr == null) return fixes;

            foreach (var item in arr)
            {
                var action = item.ToObject<CodeAction>();
                if (action == null) continue;

                fixes.Add(new CodeFix
                {
                    Action = new OmniSharpCodeAction(action, _server, _editor)
                });
            }

            return fixes;
        }

        // ─── Code lens ────────────────────────────────────────────────────────

        public async Task<IEnumerable<Languages.CodeLens>> GetCodeLensAsync(CancellationToken ct = default)
        {
            if (_server?.Rpc == null) return Enumerable.Empty<Languages.CodeLens>();

            SyncDocument();

            var items = await _server.Rpc.SendRequestAsync<Protocol.CodeLensItem[]>(
                "textDocument/codeLens",
                new Protocol.CodeLensParams
                {
                    TextDocument = new Protocol.TextDocumentIdentifier { Uri = FilePathToUri(_editor.SourceFile.Location) }
                }, ct).ConfigureAwait(false);

            if (items == null) return Enumerable.Empty<Languages.CodeLens>();

            return items
                .Where(i => i?.Range != null)
                .Select(i => new Languages.CodeLens
                {
                    Line = (i.Range.Start?.Line ?? 0) + 1,
                    Column = (i.Range.Start?.Character ?? 0) + 1,
                    Label = i.Command?.Title ?? "code lens",
                    Description = i.Command?.Title
                })
                .ToList();
        }

        // ─── Code analysis (syntax highlight + diagnostics) ───────────────────

        public async Task<CodeAnalysisResults> RunCodeAnalysisAsync(IEnumerable<UnsavedFile> unsavedFiles,
            Func<bool> interruptRequested)
        {
            SyncDocument();

            var result = new CodeAnalysisResults();
            return await Task.FromResult(result);
        }

        // ─── Formatting ───────────────────────────────────────────────────────

        public int Format(uint offset, uint length, int cursor)
        {
            if (_server?.Rpc == null) return cursor;

            Task.Run(async () =>
            {
                var edits = await _server.Rpc.SendRequestAsync<TextEdit[]>(
                    "textDocument/formatting",
                    new DocumentFormattingParams
                    {
                        TextDocument = new TextDocumentIdentifier { Uri = FilePathToUri(_editor.SourceFile.Location) },
                        Options = new FormattingOptions { TabSize = 4, InsertSpaces = true }
                    }).ConfigureAwait(false);

                if (edits == null || edits.Length == 0) return;

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    using (_editor.Document.RunUpdate())
                    {
                        // Apply edits in reverse order to preserve offsets
                        foreach (var edit in edits.Reverse())
                        {
                            int start = LspPositionToOffset(edit.Range.Start);
                            int end = LspPositionToOffset(edit.Range.End);
                            _editor.Document.Replace(start, end - start, edit.NewText);
                        }
                    }
                });
            }).Forget();

            return cursor;
        }

        // ─── Comment / Uncomment ──────────────────────────────────────────────

        public int Comment(int firstLine, int endLine, int caret = -1, bool format = true)
        {
            var doc = _editor.Document;
            using (doc.RunUpdate())
            {
                for (int l = firstLine; l <= endLine; l++)
                    doc.Insert(doc.GetLineByNumber(l).Offset, "//");
            }
            return caret;
        }

        public int UnComment(int firstLine, int endLine, int caret = -1, bool format = true)
        {
            var doc = _editor.Document;
            using (doc.RunUpdate())
            {
                for (int l = firstLine; l <= endLine; l++)
                {
                    var docLine = doc.GetLineByNumber(l);
                    string lineText = doc.GetText(docLine.Offset, docLine.Length);
                    int idx = lineText.IndexOf("//", StringComparison.Ordinal);
                    if (idx >= 0)
                        doc.Replace(docLine.Offset + idx, 2, string.Empty);
                }
            }
            return caret;
        }

        // ─── Diagnostics callback ─────────────────────────────────────────────

        private void OnDiagnosticsPublished(PublishDiagnosticsParams p)
        {
            if (_editor == null) return;

            string myUri = FilePathToUri(_editor.SourceFile.Location);
            if (!string.Equals(p.Uri, myUri, StringComparison.OrdinalIgnoreCase)) return;

            var diagnostics = (p.Diagnostics ?? Array.Empty<LspDiagnostic>())
                .Select(d => FromLspDiagnostic(d, _editor.SourceFile.Location, _editor.SourceFile.Project))
                .ToImmutableArray();

            try
            {
                var errorList = IoC.Get<IErrorList>();
                var tag = ("omnisharp", _editor.SourceFile);
                errorList.Remove(tag);
                errorList.Create(tag, _editor.SourceFile.FilePath, DiagnosticSourceKind.Analysis, diagnostics);
            }
            catch { }
        }

        private static Diagnostic FromLspDiagnostic(LspDiagnostic d, string filePath, IProject project)
        {
            int severity = d.Severity ?? 1;
            DiagnosticLevel level = severity switch
            {
                1 => DiagnosticLevel.Error,
                2 => DiagnosticLevel.Warning,
                3 => DiagnosticLevel.Info,
                _ => DiagnosticLevel.Hidden
            };

            int startLine = (d.Range?.Start?.Line ?? 0) + 1;
            int startChar = d.Range?.Start?.Character ?? 0;

            return new Diagnostic(
                offset: 0,
                length: 1,
                project: project?.Name ?? "",
                file: filePath,
                line: startLine,
                message: d.Message ?? "",
                code: d.Code?.ToString() ?? "",
                level: level,
                category: DiagnosticCategory.Compiler,
                kind: DiagnosticSourceKind.Analysis);
        }

        // ─── Position helpers ─────────────────────────────────────────────────

        private (int line, int character) OffsetToLspPosition(int offset)
        {
            if (_editor?.Document == null) return (0, 0);

            try
            {
                var loc = _editor.Document.GetLocation(offset);
                return (loc.Line - 1, loc.Column - 1);
            }
            catch
            {
                return (0, 0);
            }
        }

        private (int line, int character) ToLspPosition(int oneBased_line, int oneBased_col)
            => (oneBased_line - 1, oneBased_col - 1);

        private int LspPositionToOffset(Position pos)
        {
            if (_editor?.Document == null) return 0;
            try
            {
                return _editor.Document.GetOffset(pos.Line + 1, pos.Character + 1);
            }
            catch
            {
                return 0;
            }
        }

        // ─── URI helpers ──────────────────────────────────────────────────────

        internal static string FilePathToUri(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return "";
            if (filePath.StartsWith("file://", StringComparison.OrdinalIgnoreCase)) return filePath;

            filePath = filePath.Replace('\\', '/');
            if (!filePath.StartsWith("/")) filePath = "/" + filePath;
            return "file://" + filePath;
        }

        private static string UriToFilePath(string uri)
        {
            if (string.IsNullOrEmpty(uri)) return uri;
            if (!uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase)) return uri;

            string path = uri.Substring("file://".Length);
            path = Uri.UnescapeDataString(path);

            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                    System.Runtime.InteropServices.OSPlatform.Windows))
            {
                path = path.TrimStart('/').Replace('/', '\\');
            }

            return path;
        }

        // ─── Hover text extraction ─────────────────────────────────────────────

        private static string ExtractHoverText(JToken contents)
        {
            if (contents == null) return null;
            if (contents.Type == JTokenType.String) return contents.Value<string>();
            if (contents.Type == JTokenType.Object)
            {
                var mc = contents.ToObject<MarkupContent>();
                return mc?.Value;
            }
            if (contents.Type == JTokenType.Array)
            {
                var parts = new System.Text.StringBuilder();
                foreach (var item in (JArray)contents)
                {
                    parts.AppendLine(ExtractHoverText(item));
                }
                return parts.ToString().Trim();
            }
            return contents.ToString();
        }

        private static string ExtractDocumentation(JToken doc)
        {
            if (doc == null) return null;
            if (doc.Type == JTokenType.String) return doc.Value<string>();
            if (doc.Type == JTokenType.Object)
            {
                var mc = doc.ToObject<MarkupContent>();
                return mc?.Value;
            }
            return doc.ToString();
        }

        // ─── LSP CompletionItemKind → CodeCompletionKind ─────────────────────

        private static CodeCompletionKind FromLspCompletionKind(int? kind)
        {
            return kind switch
            {
                1 => CodeCompletionKind.None,         // Text
                2 => CodeCompletionKind.MethodPublic, // Method
                3 => CodeCompletionKind.MethodPublic, // Function
                4 => CodeCompletionKind.MethodPublic, // Constructor
                5 => CodeCompletionKind.FieldPublic,  // Field
                6 => CodeCompletionKind.Variable,     // Variable
                7 => CodeCompletionKind.ClassPublic,  // Class
                8 => CodeCompletionKind.InterfacePublic, // Interface
                9 => CodeCompletionKind.None,         // Module
                10 => CodeCompletionKind.PropertyPublic, // Property
                11 => CodeCompletionKind.EnumPublic,  // Unit
                12 => CodeCompletionKind.EnumMemberPublic, // Value
                13 => CodeCompletionKind.EnumPublic,  // Enum
                14 => CodeCompletionKind.Keyword,     // Keyword
                15 => CodeCompletionKind.None,        // Snippet
                16 => CodeCompletionKind.None,        // Color
                17 => CodeCompletionKind.None,        // File
                18 => CodeCompletionKind.None,        // Reference
                19 => CodeCompletionKind.None,        // Folder
                20 => CodeCompletionKind.EnumMemberPublic, // EnumMember
                21 => CodeCompletionKind.None,        // Constant
                22 => CodeCompletionKind.StructurePublic,// Struct
                23 => CodeCompletionKind.EventPublic, // Event
                24 => CodeCompletionKind.None,        // Operator
                25 => CodeCompletionKind.None,        // TypeParameter
                _ => CodeCompletionKind.None
            };
        }
    }

    // ─── Helper: OmniSharp code action wrapped as ICodeAction ─────────────────

    internal sealed class OmniSharpCodeAction : ICodeAction
    {
        private readonly CodeAction _action;
        private readonly OmniSharpServerManager _server;
        private readonly ITextEditor _editor;

        public OmniSharpCodeAction(CodeAction action, OmniSharpServerManager server, ITextEditor editor)
        {
            _action = action;
            _server = server;
            _editor = editor;
        }

        public ImmutableArray<ICodeAction> NestedCodeActions => ImmutableArray<ICodeAction>.Empty;
        public bool IsInlinable => false;
        public string EquivalenceKey => _action.Title;
        public string Message => _action.Title;
        public string Title => _action.Title;

        public Task<ImmutableArray<ICodeActionOperation>> GetOperationsAsync(CancellationToken ct)
        {
            var op = new OmniSharpWorkspaceEditOperation(_action, _editor);
            return Task.FromResult(ImmutableArray.Create<ICodeActionOperation>(op));
        }
    }

    internal sealed class OmniSharpWorkspaceEditOperation : ICodeActionOperation
    {
        private readonly CodeAction _action;
        private readonly ITextEditor _editor;

        public OmniSharpWorkspaceEditOperation(CodeAction action, ITextEditor editor)
        {
            _action = action;
            _editor = editor;
        }

        public string Title => _action.Title;

        public void Apply(object workspace, CancellationToken ct)
        {
            var edit = _action.Edit;
            if (edit?.Changes == null) return;

            string myUri = OmniSharpLanguageService.FilePathToUri(_editor.SourceFile.Location);

            if (!edit.Changes.TryGetValue(myUri, out var edits)) return;

            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                using (_editor.Document.RunUpdate())
                {
                    foreach (var te in edits.Reverse())
                    {
                        int start = _editor.Document.GetOffset(
                            (te.Range?.Start?.Line ?? 0) + 1,
                            (te.Range?.Start?.Character ?? 0) + 1);
                        int end = _editor.Document.GetOffset(
                            (te.Range?.End?.Line ?? 0) + 1,
                            (te.Range?.End?.Character ?? 0) + 1);
                        _editor.Document.Replace(start, end - start, te.NewText ?? "");
                    }
                }
            });
        }
    }

    internal sealed class OmniSharpContextActionProvider : IContextActionProvider
    {
        private readonly OmniSharpLanguageService _service;

        public OmniSharpContextActionProvider(OmniSharpLanguageService service)
        {
            _service = service;
        }

        public ICommand GetActionCommand(object action) => null;

        public Task<IEnumerable<CodeFix>> GetCodeFixes(ITextEditor editor, int offset, int length, CancellationToken ct)
            => _service.GetCodeFixesAsync(offset, length, ct);
    }
}
