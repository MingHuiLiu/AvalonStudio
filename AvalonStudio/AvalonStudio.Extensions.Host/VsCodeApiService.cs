using AvalonStudio.Extensions.Host.Protocol;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace AvalonStudio.Extensions.Host
{
    /// <summary>
    /// Handles VS Code API calls made by extensions via the Extension Host.
    /// Implements the C# side of the vscode.* namespace APIs.
    /// </summary>
    public class VsCodeApiService
    {
        private readonly ExtensionHostProcessManager _hostManager;
        private readonly Dictionary<string, Func<JsonElement?, JsonElement?>> _handlers
            = new Dictionary<string, Func<JsonElement?, JsonElement?>>(StringComparer.Ordinal);

        public VsCodeApiService(ExtensionHostProcessManager hostManager)
        {
            _hostManager = hostManager ?? throw new ArgumentNullException(nameof(hostManager));
            _hostManager.NotificationReceived += OnNotificationReceived;

            RegisterHandlers();
        }

        // Events that the editor bridge can subscribe to
        public event EventHandler<PublishDiagnosticsParams> DiagnosticsPublished;
        public event EventHandler<ShowMessageParams> MessageShown;
        public event EventHandler<string> CommandExecuted;

        private void RegisterHandlers()
        {
            // workspace
            _handlers["workspace/workspaceFolders"] = _ => null;
            _handlers["workspace/getConfiguration"] = HandleGetConfiguration;

            // window
            _handlers["window/showInformationMessage"] = HandleShowMessage;
            _handlers["window/showWarningMessage"] = HandleShowMessage;
            _handlers["window/showErrorMessage"] = HandleShowMessage;

            // diagnostics
            _handlers["textDocument/publishDiagnostics"] = HandlePublishDiagnostics;
        }

        private void OnNotificationReceived(object sender, JsonRpcNotification notification)
        {
            if (_handlers.TryGetValue(notification.Method, out var handler))
            {
                handler(notification.Params);
            }
        }

        private JsonElement? HandleGetConfiguration(JsonElement? @params)
        {
            return JsonSerializer.Deserialize<JsonElement>("{}");
        }

        private JsonElement? HandleShowMessage(JsonElement? @params)
        {
            if (@params.HasValue)
            {
                var p = JsonSerializer.Deserialize<ShowMessageParams>(@params.Value.GetRawText());
                MessageShown?.Invoke(this, p);
            }
            return null;
        }

        private JsonElement? HandlePublishDiagnostics(JsonElement? @params)
        {
            if (@params.HasValue)
            {
                var p = JsonSerializer.Deserialize<PublishDiagnosticsParams>(@params.Value.GetRawText());
                DiagnosticsPublished?.Invoke(this, p);
            }
            return null;
        }

        // --- Outgoing notifications to Extension Host ---

        public void NotifyDocumentOpened(string uri, string languageId, int version, string text)
        {
            _hostManager.SendNotification("textDocument/didOpen", new
            {
                textDocument = new { uri, languageId, version, text }
            });
        }

        public void NotifyDocumentChanged(string uri, int version, string text)
        {
            _hostManager.SendNotification("textDocument/didChange", new
            {
                textDocument = new { uri, version },
                contentChanges = new[] { new { text } }
            });
        }

        public void NotifyDocumentSaved(string uri, string text)
        {
            _hostManager.SendNotification("textDocument/didSave", new
            {
                textDocument = new { uri },
                text
            });
        }

        public void NotifyDocumentClosed(string uri)
        {
            _hostManager.SendNotification("textDocument/didClose", new
            {
                textDocument = new { uri }
            });
        }
    }

    // --- DTOs ---

    public class PublishDiagnosticsParams
    {
        public string Uri { get; set; }
        public LspDiagnostic[] Diagnostics { get; set; }
    }

    public class LspDiagnostic
    {
        public LspRange Range { get; set; }
        public int? Severity { get; set; }
        public string Code { get; set; }
        public string Source { get; set; }
        public string Message { get; set; }
    }

    public class LspRange
    {
        public LspPosition Start { get; set; }
        public LspPosition End { get; set; }
    }

    public class LspPosition
    {
        public int Line { get; set; }
        public int Character { get; set; }
    }

    public class ShowMessageParams
    {
        public int Type { get; set; }
        public string Message { get; set; }
    }
}
