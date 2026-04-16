using AvalonStudio.LanguageServer.OmniSharp.Protocol;
using AvalonStudio.Platforms;
using AvalonStudio.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AvalonStudio.LanguageServer.OmniSharp
{
    /// <summary>
    /// Manages the lifecycle of an OmniSharp language server process and exposes
    /// a <see cref="JsonRpcClient"/> connected to it via stdin/stdout.
    /// </summary>
    public sealed class OmniSharpServerManager : IDisposable
    {
        private Process _process;
        private JsonRpcClient _rpc;
        private readonly string _solutionPath;
        private readonly SemaphoreSlim _startSemaphore = new SemaphoreSlim(1, 1);
        private bool _started;

        /// <summary>Fired when the server publishes diagnostics for a document.</summary>
        public event Action<PublishDiagnosticsParams> DiagnosticsPublished;

        public JsonRpcClient Rpc => _rpc;

        public bool IsRunning => _process != null && !_process.HasExited;

        public OmniSharpServerManager(string solutionPath)
        {
            _solutionPath = solutionPath;
        }

        /// <summary>
        /// Starts the OmniSharp server and performs the LSP initialisation handshake.
        /// Safe to call multiple times; subsequent calls are no-ops.
        /// </summary>
        public async Task StartAsync(IConsole console, CancellationToken ct = default)
        {
            await EnsureStartedAsync(console, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Ensures the server is started. Idempotent; subsequent calls are no-ops.
        /// </summary>
        public async Task EnsureStartedAsync(IConsole console, CancellationToken ct = default)
        {
            await _startSemaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_started) return;
                _started = true;
            }
            finally
            {
                _startSemaphore.Release();
            }

            string binary = FindOmniSharpBinary();
            if (binary == null)
            {
                console?.WriteLine("[OmniSharp] Cannot find OmniSharp binary. " +
                    "Please install OmniSharp and place it on your PATH, or set the " +
                    "OMNISHARP_PATH environment variable.");
                return;
            }

            var psi = BuildProcessStartInfo(binary);
            _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _process.Exited += OnProcessExited;

            try
            {
                _process.Start();
            }
            catch (Exception ex)
            {
                console?.WriteLine($"[OmniSharp] Failed to start: {ex.Message}");
                return;
            }

            _rpc = new JsonRpcClient(_process.StandardOutput.BaseStream,
                                     _process.StandardInput.BaseStream);

            _rpc.NotificationReceived += OnNotification;
            _rpc.Start();

            await InitialiseAsync(ct).ConfigureAwait(false);

            console?.WriteLine("[OmniSharp] Server started and initialised.");
        }

        private async Task InitialiseAsync(CancellationToken ct)
        {
            var rootUri = new Uri(_solutionPath, UriKind.Absolute).AbsoluteUri;

            var initParams = new InitializeParams
            {
                ProcessId = Process.GetCurrentProcess().Id,
                RootUri = rootUri,
                InitializationOptions = new { },
                Capabilities = new ClientCapabilities
                {
                    Workspace = new WorkspaceClientCapabilities
                    {
                        ApplyEdit = true,
                        WorkspaceEdit = new WorkspaceEditCapability { DocumentChanges = true }
                    },
                    TextDocument = new TextDocumentClientCapabilities
                    {
                        Synchronization = new TextDocumentSyncClientCapabilities
                        {
                            DynamicRegistration = false,
                            WillSave = false,
                            DidSave = true
                        },
                        Completion = new CompletionClientCapabilities
                        {
                            CompletionItem = new CompletionItemCapability
                            {
                                SnippetSupport = false,
                                DocumentationFormat = new[] { "plaintext", "markdown" }
                            }
                        },
                        Hover = new HoverClientCapabilities
                        {
                            ContentFormat = new[] { "plaintext", "markdown" }
                        },
                        SignatureHelp = new SignatureHelpClientCapabilities
                        {
                            SignatureInformation = new SignatureInformationCapability
                            {
                                DocumentationFormat = new[] { "plaintext", "markdown" }
                            }
                        },
                        Definition = new { },
                        References = new { },
                        DocumentHighlight = new { },
                        CodeAction = new { },
                        CodeLens = new { },
                        Rename = new { },
                        PublishDiagnostics = new { }
                    }
                }
            };

            await _rpc.SendRequestAsync<InitializeResult>("initialize", initParams, ct)
                .ConfigureAwait(false);

            await _rpc.SendNotificationAsync("initialized", new { }).ConfigureAwait(false);
        }

        private void OnNotification(string method, JToken @params)
        {
            if (method == "textDocument/publishDiagnostics" && @params != null)
            {
                var p = @params.ToObject<PublishDiagnosticsParams>();
                if (p != null)
                    DiagnosticsPublished?.Invoke(p);
            }
        }

        private void OnProcessExited(object sender, System.EventArgs e)
        {
            _started = false;
        }

        // ─── helpers ─────────────────────────────────────────────────────────

        private static string FindOmniSharpBinary()
        {
            // 1. Explicit env-var override
            string envPath = Environment.GetEnvironmentVariable("OMNISHARP_PATH");
            if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath))
                return envPath;

            // 2. Alongside the running executable (published alongside the app)
            string baseDir = AppContext.BaseDirectory;
            foreach (string candidate in new[]
            {
                Path.Combine(baseDir, "OmniSharp" + Platform.ExecutableExtension),
                Path.Combine(baseDir, ".omnisharp", "OmniSharp" + Platform.ExecutableExtension),
                Path.Combine(baseDir, "omnisharp", "OmniSharp" + Platform.ExecutableExtension),
            })
            {
                if (File.Exists(candidate)) return candidate;
            }

            // 3. User-level location: ~/.omnisharp/
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            foreach (string candidate in new[]
            {
                Path.Combine(home, ".omnisharp", "OmniSharp" + Platform.ExecutableExtension),
                Path.Combine(home, ".omnisharp", "run"),
            })
            {
                if (File.Exists(candidate)) return candidate;
            }

            // 4. PATH
            string exeName = "OmniSharp" + Platform.ExecutableExtension;
            string pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string dir in pathVar.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                string full = Path.Combine(dir.Trim(), exeName);
                if (File.Exists(full)) return full;
            }

            return null;
        }

        private ProcessStartInfo BuildProcessStartInfo(string binary)
        {
            string solutionDir = Directory.Exists(_solutionPath)
                ? _solutionPath
                : Path.GetDirectoryName(_solutionPath) ?? _solutionPath;

            var psi = new ProcessStartInfo
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // OmniSharp may be a self-contained binary or a .dll that needs 'dotnet'
            if (binary.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                psi.FileName = "dotnet";
                psi.Arguments = $"\"{binary}\" -lsp -s \"{solutionDir}\"";
            }
            else
            {
                psi.FileName = binary;
                psi.Arguments = $"-lsp -s \"{solutionDir}\"";
            }

            return psi;
        }

        public void Dispose()
        {
            _rpc?.Dispose();
            try
            {
                if (_process != null && !_process.HasExited)
                {
                    _process.Kill();
                    _process.WaitForExit(3000);
                }
            }
            catch { }

            _process?.Dispose();
        }
    }
}
