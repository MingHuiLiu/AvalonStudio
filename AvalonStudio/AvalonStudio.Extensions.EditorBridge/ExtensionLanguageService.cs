using AvalonStudio.Documents;
using AvalonStudio.Editor;
using AvalonStudio.Extensibility.Languages.CompletionAssistance;
using AvalonStudio.Extensions.Host;
using AvalonStudio.Languages;
using AvalonStudio.Projects;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using System.Threading.Tasks;

namespace AvalonStudio.Extensions.EditorBridge
{
    /// <summary>
    /// Implements ILanguageService by forwarding language requests to the VS Code Extension Host.
    /// </summary>
    public class ExtensionLanguageService : ILanguageService
    {
        private readonly ExtensionHostProcessManager _hostManager;
        private readonly VsCodeApiService _apiService;
        private ITextEditor _editor;
        private string _languageId;

        public ExtensionLanguageService(ExtensionHostProcessManager hostManager, VsCodeApiService apiService, string languageId)
        {
            _hostManager = hostManager ?? throw new ArgumentNullException(nameof(hostManager));
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _languageId = languageId ?? throw new ArgumentNullException(nameof(languageId));

            _apiService.DiagnosticsPublished += OnDiagnosticsPublished;
        }

        public string LanguageId => _languageId;

        public IDictionary<string, Func<string, string>> SnippetCodeGenerators =>
            new Dictionary<string, Func<string, string>>();

        public IDictionary<string, Func<int, int, int, string>> SnippetDynamicVariables =>
            new Dictionary<string, Func<int, int, int, string>>();

        public IEnumerable<char> IntellisenseSearchCharacters => new[] { '.', '>', ':', ' ' };

        public IEnumerable<char> IntellisenseCompleteCharacters => new[] { '.', '(', '{', '[', ' ', ';', ',', '+', '-' };

        public IEnumerable<ITextEditorInputHelper> InputHelpers => Array.Empty<ITextEditorInputHelper>();

        public bool CanTriggerIntellisense(char currentChar, char previousChar) =>
            currentChar == '.' || currentChar == '>' || currentChar == ':';

        public bool IsValidIdentifierCharacter(char data) =>
            char.IsLetterOrDigit(data) || data == '_';

        public void RegisterEditor(ITextEditor editor)
        {
            _editor = editor;

            if (editor?.SourceFile != null)
            {
                var uri = FilePathToUri(editor.SourceFile.FilePath);
                var text = editor.Document?.Text ?? string.Empty;
                _apiService.NotifyDocumentOpened(uri, _languageId, 1, text);
            }
        }

        public void UnregisterEditor()
        {
            if (_editor?.SourceFile != null)
            {
                var uri = FilePathToUri(_editor.SourceFile.FilePath);
                _apiService.NotifyDocumentClosed(uri);
            }
            _editor = null;
        }

        public int Format(uint offset, uint length, int cursor) => cursor;

        public int Comment(int firstLine, int endLine, int caret = -1, bool format = true) => caret;

        public int UnComment(int firstLine, int endLine, int caret = -1, bool format = true) => caret;

        public Task<CodeAnalysisResults> RunCodeAnalysisAsync(
            IEnumerable<UnsavedFile> unsavedFiles, Func<bool> interruptRequested)
        {
            // Diagnostics arrive via push (publishDiagnostics), so just return empty highlights here.
            return Task.FromResult(new CodeAnalysisResults());
        }

        public async Task<CodeCompletionResults> CodeCompleteAtAsync(
            int index, int line, int column, IEnumerable<UnsavedFile> unsavedFiles, char lastChar, string filter = "")
        {
            if (_editor?.SourceFile == null || !_hostManager.IsRunning)
                return new CodeCompletionResults();

            var uri = FilePathToUri(_editor.SourceFile.FilePath);

            try
            {
                var result = await _hostManager.SendRequestAsync("textDocument/completion", new
                {
                    textDocument = new { uri },
                    context = new { triggerKind = 1, triggerCharacter = lastChar.ToString() },
                    position = new { line = line - 1, character = column - 1 }
                });

                if (!result.HasValue)
                    return new CodeCompletionResults();

                return ParseCompletionResult(result.Value);
            }
            catch
            {
                return new CodeCompletionResults();
            }
        }

        public async Task<SignatureHelp> SignatureHelp(
            IEnumerable<UnsavedFile> unsavedFiles, int offset, string methodName)
        {
            if (_editor?.SourceFile == null || !_hostManager.IsRunning)
                return null;

            var uri = FilePathToUri(_editor.SourceFile.FilePath);
            var pos = OffsetToPosition(offset);

            try
            {
                var result = await _hostManager.SendRequestAsync("textDocument/signatureHelp", new
                {
                    textDocument = new { uri },
                    position = pos
                });

                if (!result.HasValue)
                    return null;

                return ParseSignatureHelp(result.Value, offset);
            }
            catch
            {
                return null;
            }
        }

        public Task<QuickInfoResult> QuickInfo(IEnumerable<UnsavedFile> unsavedFiles, int offset)
        {
            // QuickInfo requires StyledText from the Shell layer; return null to indicate no info available.
            return Task.FromResult<QuickInfoResult>(null);
        }

        public IEnumerable<IContextActionProvider> GetContextActionProviders() =>
            Array.Empty<IContextActionProvider>();

        public async Task<GotoDefinitionInfo> GotoDefinition(int offset)
        {
            if (_editor?.SourceFile == null || !_hostManager.IsRunning)
                return new GotoDefinitionInfo();

            var uri = FilePathToUri(_editor.SourceFile.FilePath);
            var pos = OffsetToPosition(offset);

            try
            {
                var result = await _hostManager.SendRequestAsync("textDocument/definition", new
                {
                    textDocument = new { uri },
                    position = pos
                });

                if (!result.HasValue)
                    return new GotoDefinitionInfo();

                return ParseGotoDefinition(result.Value);
            }
            catch
            {
                return new GotoDefinitionInfo();
            }
        }

        public async Task<IEnumerable<SymbolRenameInfo>> RenameSymbol(string renameTo)
        {
            if (_editor?.SourceFile == null || !_hostManager.IsRunning)
                return Array.Empty<SymbolRenameInfo>();

            var uri = FilePathToUri(_editor.SourceFile.FilePath);
            var pos = OffsetToPosition(_editor.Offset);

            try
            {
                var result = await _hostManager.SendRequestAsync("textDocument/rename", new
                {
                    textDocument = new { uri },
                    position = pos,
                    newName = renameTo
                });

                if (!result.HasValue)
                    return Array.Empty<SymbolRenameInfo>();

                return ParseWorkspaceEdit(result.Value);
            }
            catch
            {
                return Array.Empty<SymbolRenameInfo>();
            }
        }

        public Task<List<Symbol>> GetSymbolsAsync(IEnumerable<UnsavedFile> unsavedFiles, string name)
        {
            return Task.FromResult(new List<Symbol>());
        }

        // --- Diagnostics ---

        private void OnDiagnosticsPublished(object sender, PublishDiagnosticsParams args)
        {
            if (_editor?.SourceFile == null)
                return;

            if (!string.Equals(FilePathToUri(_editor.SourceFile.FilePath), args.Uri, StringComparison.OrdinalIgnoreCase))
                return;

            var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

            if (args.Diagnostics != null)
            {
                foreach (var d in args.Diagnostics)
                {
                    var startLine = d.Range?.Start?.Line ?? 0;
                    var startChar = d.Range?.Start?.Character ?? 0;
                    var endLine = d.Range?.End?.Line ?? 0;
                    var endChar = d.Range?.End?.Character ?? 0;

                    var startOffset = PositionToOffset(startLine, startChar);
                    var endOffset = PositionToOffset(endLine, endChar);
                    var length = Math.Max(1, endOffset - startOffset);

                    var level = (d.Severity ?? 1) switch
                    {
                        1 => DiagnosticLevel.Error,
                        2 => DiagnosticLevel.Warning,
                        3 => DiagnosticLevel.Info,
                        _ => DiagnosticLevel.Hidden
                    };

                    diagnostics.Add(new Diagnostic(
                        offset: startOffset,
                        length: length,
                        project: string.Empty,
                        file: _editor.SourceFile.FilePath,
                        line: startLine + 1,
                        message: d.Message ?? string.Empty,
                        code: d.Code ?? string.Empty,
                        level: level,
                        category: DiagnosticCategory.Compiler
                    ));
                }
            }

            DiagnosticsUpdated?.Invoke(this, new DiagnosticsUpdatedEventArgs(
                this,
                _editor.SourceFile.FilePath,
                diagnostics.Count > 0
                    ? DiagnosticsUpdatedKind.DiagnosticsCreated
                    : DiagnosticsUpdatedKind.DiagnosticsRemoved,
                DiagnosticSourceKind.Analysis,
                diagnostics.ToImmutable()
            ));
        }

        /// <summary>
        /// Raised when the Extension Host pushes new diagnostics for the registered document.
        /// </summary>
        public event EventHandler<DiagnosticsUpdatedEventArgs> DiagnosticsUpdated;

        // --- Helpers ---

        private static string FilePathToUri(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return string.Empty;
            var uri = new Uri(filePath);
            return uri.AbsoluteUri;
        }

        private object OffsetToPosition(int offset)
        {
            if (_editor?.Document == null)
                return new { line = 0, character = 0 };

            var loc = _editor.Document.GetLocation(offset);
            return new { line = loc.Line - 1, character = loc.Column - 1 };
        }

        private int PositionToOffset(int line, int character)
        {
            if (_editor?.Document == null)
                return 0;

            try
            {
                return _editor.Document.GetOffset(line + 1, character + 1);
            }
            catch
            {
                return 0;
            }
        }

        private static CodeCompletionResults ParseCompletionResult(JsonElement element)
        {
            var results = new CodeCompletionResults();

            JsonElement items;
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("items", out var itemsProp))
                items = itemsProp;
            else
                items = element;

            if (items.ValueKind != JsonValueKind.Array)
                return results;

            foreach (var item in items.EnumerateArray())
            {
                var label = item.TryGetProperty("label", out var labelProp) ? labelProp.GetString() : string.Empty;
                var insertText = item.TryGetProperty("insertText", out var insertProp) ? insertProp.GetString() : label;
                var detail = item.TryGetProperty("detail", out var detailProp) ? detailProp.GetString() : string.Empty;
                var kind = item.TryGetProperty("kind", out var kindProp) ? kindProp.GetInt32() : 0;

                results.Completions.Add(new CodeCompletionData(
                    displayText: label,
                    filterText: label,
                    insertionText: insertText ?? label
                )
                {
                    BriefComment = detail,
                    Kind = MapCompletionItemKind(kind)
                });
            }

            return results;
        }

        private static CodeCompletionKind MapCompletionItemKind(int lspKind)
        {
            return lspKind switch
            {
                1 => CodeCompletionKind.None,              // Text
                2 => CodeCompletionKind.MethodPublic,      // Method
                3 => CodeCompletionKind.MethodPublic,      // Function
                4 => CodeCompletionKind.MethodPublic,      // Constructor
                5 => CodeCompletionKind.FieldPublic,       // Field
                6 => CodeCompletionKind.Variable,          // Variable
                7 => CodeCompletionKind.ClassPublic,       // Class
                8 => CodeCompletionKind.InterfacePublic,   // Interface
                9 => CodeCompletionKind.None,              // Module
                10 => CodeCompletionKind.PropertyPublic,   // Property
                11 => CodeCompletionKind.EnumMemberPublic, // Unit
                12 => CodeCompletionKind.EnumMemberPublic, // Value
                13 => CodeCompletionKind.EnumPublic,       // Enum
                14 => CodeCompletionKind.Keyword,          // Keyword
                15 => CodeCompletionKind.Snippet,          // Snippet
                _ => CodeCompletionKind.None
            };
        }

        private static SignatureHelp ParseSignatureHelp(JsonElement element, int offset)
        {
            var sigHelp = new SignatureHelp(offset);

            if (!element.TryGetProperty("signatures", out var sigs))
                return sigHelp;

            if (element.TryGetProperty("activeSignature", out var activeSig))
                sigHelp.ActiveSignature = activeSig.GetInt32();

            if (element.TryGetProperty("activeParameter", out var activeParam))
                sigHelp.ActiveParameter = activeParam.GetInt32();

            foreach (var sig in sigs.EnumerateArray())
            {
                var label = sig.TryGetProperty("label", out var labelProp) ? labelProp.GetString() : string.Empty;
                var signature = new Signature { Name = label };

                if (sig.TryGetProperty("parameters", out var parameters))
                {
                    foreach (var param in parameters.EnumerateArray())
                    {
                        var paramLabel = param.TryGetProperty("label", out var pl) ? pl.GetString() : string.Empty;
                        var paramDoc = param.TryGetProperty("documentation", out var pd) ? pd.GetString() : string.Empty;
                        signature.Parameters.Add(new Parameter { Name = paramLabel, Documentation = paramDoc });
                    }
                }

                sigHelp.Signatures.Add(signature);
            }

            return sigHelp;
        }

        private static GotoDefinitionInfo ParseGotoDefinition(JsonElement element)
        {
            var info = new GotoDefinitionInfo();

            JsonElement location;
            if (element.ValueKind == JsonValueKind.Array)
            {
                if (element.GetArrayLength() == 0) return info;
                location = element[0];
            }
            else
            {
                location = element;
            }

            if (location.TryGetProperty("uri", out var uriProp))
            {
                var uriStr = uriProp.GetString();
                if (Uri.TryCreate(uriStr, UriKind.Absolute, out var uri))
                    info.FileName = uri.LocalPath;
            }

            if (location.TryGetProperty("range", out var range) &&
                range.TryGetProperty("start", out var start))
            {
                info.Line = start.TryGetProperty("line", out var l) ? l.GetInt32() + 1 : 0;
                info.Column = start.TryGetProperty("character", out var c) ? c.GetInt32() + 1 : 0;
            }

            return info;
        }

        private static IEnumerable<SymbolRenameInfo> ParseWorkspaceEdit(JsonElement element)
        {
            var results = new List<SymbolRenameInfo>();

            if (!element.TryGetProperty("changes", out var changes))
                return results;

            foreach (var fileEdit in changes.EnumerateObject())
            {
                if (!Uri.TryCreate(fileEdit.Name, UriKind.Absolute, out var uri))
                    continue;

                var filePath = uri.LocalPath;
                var edits = new List<LinePositionSpanTextChange>();

                foreach (var edit in fileEdit.Value.EnumerateArray())
                {
                    if (!edit.TryGetProperty("range", out var range) ||
                        !edit.TryGetProperty("newText", out var newText))
                        continue;

                    range.TryGetProperty("start", out var start);
                    range.TryGetProperty("end", out var end);

                    edits.Add(new LinePositionSpanTextChange
                    {
                        NewText = newText.GetString(),
                        StartLine = start.TryGetProperty("line", out var sl) ? sl.GetInt32() + 1 : 0,
                        StartColumn = start.TryGetProperty("character", out var sc) ? sc.GetInt32() + 1 : 0,
                        EndLine = end.TryGetProperty("line", out var el) ? el.GetInt32() + 1 : 0,
                        EndColumn = end.TryGetProperty("character", out var ec) ? ec.GetInt32() + 1 : 0,
                    });
                }

                results.Add(new SymbolRenameInfo(filePath) { Changes = edits });
            }

            return results;
        }
    }
}
