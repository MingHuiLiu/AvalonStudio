using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace AvalonStudio.LanguageServer.OmniSharp.Protocol
{
    // ─── JSON-RPC base types ──────────────────────────────────────────────────

    public class JsonRpcMessage
    {
        [JsonProperty("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";
    }

    public class JsonRpcRequest : JsonRpcMessage
    {
        [JsonProperty("id")]
        public int? Id { get; set; }

        [JsonProperty("method")]
        public string Method { get; set; }

        [JsonProperty("params")]
        public object Params { get; set; }
    }

    public class JsonRpcResponse : JsonRpcMessage
    {
        [JsonProperty("id")]
        public int? Id { get; set; }

        [JsonProperty("result")]
        public JToken Result { get; set; }

        [JsonProperty("error")]
        public JsonRpcError Error { get; set; }
    }

    public class JsonRpcError
    {
        [JsonProperty("code")]
        public int Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("data")]
        public JToken Data { get; set; }
    }

    // ─── LSP basic types ──────────────────────────────────────────────────────

    public class Position
    {
        [JsonProperty("line")]
        public int Line { get; set; }

        [JsonProperty("character")]
        public int Character { get; set; }
    }

    public class Range
    {
        [JsonProperty("start")]
        public Position Start { get; set; }

        [JsonProperty("end")]
        public Position End { get; set; }
    }

    public class Location
    {
        [JsonProperty("uri")]
        public string Uri { get; set; }

        [JsonProperty("range")]
        public Range Range { get; set; }
    }

    public class TextDocumentIdentifier
    {
        [JsonProperty("uri")]
        public string Uri { get; set; }
    }

    public class VersionedTextDocumentIdentifier : TextDocumentIdentifier
    {
        [JsonProperty("version")]
        public int Version { get; set; }
    }

    public class TextDocumentPositionParams
    {
        [JsonProperty("textDocument")]
        public TextDocumentIdentifier TextDocument { get; set; }

        [JsonProperty("position")]
        public Position Position { get; set; }
    }

    public class TextDocumentItem
    {
        [JsonProperty("uri")]
        public string Uri { get; set; }

        [JsonProperty("languageId")]
        public string LanguageId { get; set; }

        [JsonProperty("version")]
        public int Version { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }
    }

    public class TextEdit
    {
        [JsonProperty("range")]
        public Range Range { get; set; }

        [JsonProperty("newText")]
        public string NewText { get; set; }
    }

    public class WorkspaceEdit
    {
        [JsonProperty("changes")]
        public Dictionary<string, TextEdit[]> Changes { get; set; }
    }

    public class Command
    {
        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("command")]
        public string CommandId { get; set; }

        [JsonProperty("arguments")]
        public object[] Arguments { get; set; }
    }

    public class MarkupContent
    {
        [JsonProperty("kind")]
        public string Kind { get; set; }   // "plaintext" | "markdown"

        [JsonProperty("value")]
        public string Value { get; set; }
    }

    // ─── initialize ──────────────────────────────────────────────────────────

    public class InitializeParams
    {
        [JsonProperty("processId")]
        public int? ProcessId { get; set; }

        [JsonProperty("rootUri")]
        public string RootUri { get; set; }

        [JsonProperty("capabilities")]
        public ClientCapabilities Capabilities { get; set; }

        [JsonProperty("initializationOptions")]
        public object InitializationOptions { get; set; }
    }

    public class ClientCapabilities
    {
        [JsonProperty("textDocument")]
        public TextDocumentClientCapabilities TextDocument { get; set; }

        [JsonProperty("workspace")]
        public WorkspaceClientCapabilities Workspace { get; set; }
    }

    public class TextDocumentClientCapabilities
    {
        [JsonProperty("synchronization")]
        public TextDocumentSyncClientCapabilities Synchronization { get; set; }

        [JsonProperty("completion")]
        public CompletionClientCapabilities Completion { get; set; }

        [JsonProperty("hover")]
        public HoverClientCapabilities Hover { get; set; }

        [JsonProperty("signatureHelp")]
        public SignatureHelpClientCapabilities SignatureHelp { get; set; }

        [JsonProperty("definition")]
        public object Definition { get; set; }

        [JsonProperty("references")]
        public object References { get; set; }

        [JsonProperty("documentHighlight")]
        public object DocumentHighlight { get; set; }

        [JsonProperty("codeAction")]
        public object CodeAction { get; set; }

        [JsonProperty("codeLens")]
        public object CodeLens { get; set; }

        [JsonProperty("rename")]
        public object Rename { get; set; }

        [JsonProperty("publishDiagnostics")]
        public object PublishDiagnostics { get; set; }
    }

    public class TextDocumentSyncClientCapabilities
    {
        [JsonProperty("dynamicRegistration")]
        public bool DynamicRegistration { get; set; }

        [JsonProperty("willSave")]
        public bool WillSave { get; set; }

        [JsonProperty("didSave")]
        public bool DidSave { get; set; }
    }

    public class CompletionClientCapabilities
    {
        [JsonProperty("completionItem")]
        public CompletionItemCapability CompletionItem { get; set; }
    }

    public class CompletionItemCapability
    {
        [JsonProperty("snippetSupport")]
        public bool SnippetSupport { get; set; }

        [JsonProperty("documentationFormat")]
        public string[] DocumentationFormat { get; set; }
    }

    public class HoverClientCapabilities
    {
        [JsonProperty("contentFormat")]
        public string[] ContentFormat { get; set; }
    }

    public class SignatureHelpClientCapabilities
    {
        [JsonProperty("signatureInformation")]
        public SignatureInformationCapability SignatureInformation { get; set; }
    }

    public class SignatureInformationCapability
    {
        [JsonProperty("documentationFormat")]
        public string[] DocumentationFormat { get; set; }
    }

    public class WorkspaceClientCapabilities
    {
        [JsonProperty("applyEdit")]
        public bool ApplyEdit { get; set; }

        [JsonProperty("workspaceEdit")]
        public WorkspaceEditCapability WorkspaceEdit { get; set; }
    }

    public class WorkspaceEditCapability
    {
        [JsonProperty("documentChanges")]
        public bool DocumentChanges { get; set; }
    }

    public class InitializeResult
    {
        [JsonProperty("capabilities")]
        public ServerCapabilities Capabilities { get; set; }
    }

    public class ServerCapabilities
    {
        [JsonProperty("textDocumentSync")]
        public JToken TextDocumentSync { get; set; }

        [JsonProperty("completionProvider")]
        public CompletionOptions CompletionProvider { get; set; }

        [JsonProperty("hoverProvider")]
        public bool HoverProvider { get; set; }

        [JsonProperty("signatureHelpProvider")]
        public SignatureHelpOptions SignatureHelpProvider { get; set; }

        [JsonProperty("definitionProvider")]
        public bool DefinitionProvider { get; set; }

        [JsonProperty("referencesProvider")]
        public bool ReferencesProvider { get; set; }

        [JsonProperty("documentHighlightProvider")]
        public bool DocumentHighlightProvider { get; set; }

        [JsonProperty("codeActionProvider")]
        public bool CodeActionProvider { get; set; }

        [JsonProperty("codeLensProvider")]
        public CodeLensOptions CodeLensProvider { get; set; }

        [JsonProperty("renameProvider")]
        public bool RenameProvider { get; set; }

        [JsonProperty("documentFormattingProvider")]
        public bool DocumentFormattingProvider { get; set; }
    }

    public class CompletionOptions
    {
        [JsonProperty("resolveProvider")]
        public bool ResolveProvider { get; set; }

        [JsonProperty("triggerCharacters")]
        public string[] TriggerCharacters { get; set; }
    }

    public class SignatureHelpOptions
    {
        [JsonProperty("triggerCharacters")]
        public string[] TriggerCharacters { get; set; }
    }

    public class CodeLensOptions
    {
        [JsonProperty("resolveProvider")]
        public bool ResolveProvider { get; set; }
    }

    // ─── textDocument/didOpen ─────────────────────────────────────────────────

    public class DidOpenTextDocumentParams
    {
        [JsonProperty("textDocument")]
        public TextDocumentItem TextDocument { get; set; }
    }

    // ─── textDocument/didChange ───────────────────────────────────────────────

    public class DidChangeTextDocumentParams
    {
        [JsonProperty("textDocument")]
        public VersionedTextDocumentIdentifier TextDocument { get; set; }

        [JsonProperty("contentChanges")]
        public TextDocumentContentChangeEvent[] ContentChanges { get; set; }
    }

    public class TextDocumentContentChangeEvent
    {
        [JsonProperty("text")]
        public string Text { get; set; }
    }

    // ─── textDocument/didClose ────────────────────────────────────────────────

    public class DidCloseTextDocumentParams
    {
        [JsonProperty("textDocument")]
        public TextDocumentIdentifier TextDocument { get; set; }
    }

    // ─── textDocument/completion ──────────────────────────────────────────────

    public class CompletionParams : TextDocumentPositionParams
    {
        [JsonProperty("context")]
        public CompletionContext Context { get; set; }
    }

    public class CompletionContext
    {
        [JsonProperty("triggerKind")]
        public int TriggerKind { get; set; }

        [JsonProperty("triggerCharacter")]
        public string TriggerCharacter { get; set; }
    }

    public class CompletionList
    {
        [JsonProperty("isIncomplete")]
        public bool IsIncomplete { get; set; }

        [JsonProperty("items")]
        public CompletionItem[] Items { get; set; }
    }

    public class CompletionItem
    {
        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonProperty("kind")]
        public int? Kind { get; set; }

        [JsonProperty("detail")]
        public string Detail { get; set; }

        [JsonProperty("documentation")]
        public JToken Documentation { get; set; }

        [JsonProperty("insertText")]
        public string InsertText { get; set; }

        [JsonProperty("insertTextFormat")]
        public int? InsertTextFormat { get; set; }

        [JsonProperty("filterText")]
        public string FilterText { get; set; }

        [JsonProperty("sortText")]
        public string SortText { get; set; }

        [JsonProperty("textEdit")]
        public TextEdit TextEdit { get; set; }
    }

    // ─── textDocument/hover ───────────────────────────────────────────────────

    public class HoverResult
    {
        [JsonProperty("contents")]
        public JToken Contents { get; set; }

        [JsonProperty("range")]
        public Range Range { get; set; }
    }

    // ─── textDocument/signatureHelp ───────────────────────────────────────────

    public class SignatureHelpParams : TextDocumentPositionParams
    {
    }

    public class SignatureHelpResult
    {
        [JsonProperty("signatures")]
        public SignatureInformation[] Signatures { get; set; }

        [JsonProperty("activeSignature")]
        public int? ActiveSignature { get; set; }

        [JsonProperty("activeParameter")]
        public int? ActiveParameter { get; set; }
    }

    public class SignatureInformation
    {
        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonProperty("documentation")]
        public JToken Documentation { get; set; }

        [JsonProperty("parameters")]
        public ParameterInformation[] Parameters { get; set; }
    }

    public class ParameterInformation
    {
        [JsonProperty("label")]
        public JToken Label { get; set; }

        [JsonProperty("documentation")]
        public JToken Documentation { get; set; }
    }

    // ─── textDocument/definition ──────────────────────────────────────────────

    public class DefinitionParams : TextDocumentPositionParams
    {
    }

    // ─── textDocument/references ──────────────────────────────────────────────

    public class ReferenceParams : TextDocumentPositionParams
    {
        [JsonProperty("context")]
        public ReferenceContext Context { get; set; }
    }

    public class ReferenceContext
    {
        [JsonProperty("includeDeclaration")]
        public bool IncludeDeclaration { get; set; }
    }

    // ─── textDocument/codeAction ──────────────────────────────────────────────

    public class CodeActionParams
    {
        [JsonProperty("textDocument")]
        public TextDocumentIdentifier TextDocument { get; set; }

        [JsonProperty("range")]
        public Range Range { get; set; }

        [JsonProperty("context")]
        public CodeActionContext Context { get; set; }
    }

    public class CodeActionContext
    {
        [JsonProperty("diagnostics")]
        public LspDiagnostic[] Diagnostics { get; set; }
    }

    public class LspDiagnostic
    {
        [JsonProperty("range")]
        public Range Range { get; set; }

        [JsonProperty("severity")]
        public int? Severity { get; set; }

        [JsonProperty("code")]
        public JToken Code { get; set; }

        [JsonProperty("source")]
        public string Source { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    public class CodeAction
    {
        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("kind")]
        public string Kind { get; set; }

        [JsonProperty("diagnostics")]
        public LspDiagnostic[] Diagnostics { get; set; }

        [JsonProperty("edit")]
        public WorkspaceEdit Edit { get; set; }

        [JsonProperty("command")]
        public Command CommandField { get; set; }
    }

    // ─── textDocument/codeLens ────────────────────────────────────────────────

    public class CodeLensParams
    {
        [JsonProperty("textDocument")]
        public TextDocumentIdentifier TextDocument { get; set; }
    }

    public class CodeLensItem
    {
        [JsonProperty("range")]
        public Range Range { get; set; }

        [JsonProperty("command")]
        public Command Command { get; set; }

        [JsonProperty("data")]
        public JToken Data { get; set; }
    }

    // ─── textDocument/rename ──────────────────────────────────────────────────

    public class RenameParams
    {
        [JsonProperty("textDocument")]
        public TextDocumentIdentifier TextDocument { get; set; }

        [JsonProperty("position")]
        public Position Position { get; set; }

        [JsonProperty("newName")]
        public string NewName { get; set; }
    }

    // ─── textDocument/formatting ──────────────────────────────────────────────

    public class DocumentFormattingParams
    {
        [JsonProperty("textDocument")]
        public TextDocumentIdentifier TextDocument { get; set; }

        [JsonProperty("options")]
        public FormattingOptions Options { get; set; }
    }

    public class FormattingOptions
    {
        [JsonProperty("tabSize")]
        public int TabSize { get; set; }

        [JsonProperty("insertSpaces")]
        public bool InsertSpaces { get; set; }
    }

    // ─── textDocument/publishDiagnostics (notification) ──────────────────────

    public class PublishDiagnosticsParams
    {
        [JsonProperty("uri")]
        public string Uri { get; set; }

        [JsonProperty("diagnostics")]
        public LspDiagnostic[] Diagnostics { get; set; }
    }
}
