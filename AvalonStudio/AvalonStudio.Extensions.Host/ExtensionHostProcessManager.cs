using AvalonStudio.Extensions.Host.Protocol;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AvalonStudio.Extensions.Host
{
    /// <summary>
    /// Manages the VS Code Extension Host Node.js process lifecycle and JSON-RPC communication.
    /// </summary>
    public class ExtensionHostProcessManager : IDisposable
    {
        private Process _process;
        private readonly ConcurrentDictionary<object, TaskCompletionSource<JsonElement?>> _pendingRequests
            = new ConcurrentDictionary<object, TaskCompletionSource<JsonElement?>>();
        private int _nextId = 1;
        private CancellationTokenSource _cts;
        private Task _readTask;
        private readonly object _writeLock = new object();
        private bool _disposed;

        public event EventHandler<JsonRpcNotification> NotificationReceived;

        public bool IsRunning => _process != null && !_process.HasExited;

        /// <summary>
        /// Starts the VS Code Extension Host process using Node.js.
        /// </summary>
        /// <param name="extensionHostScriptPath">Path to extensionHostProcess.js</param>
        /// <param name="extensionsDir">Directory containing installed extensions</param>
        public void Start(string extensionHostScriptPath, string extensionsDir)
        {
            if (IsRunning)
                throw new InvalidOperationException("Extension Host is already running.");

            if (!File.Exists(extensionHostScriptPath))
                throw new FileNotFoundException("Extension Host script not found.", extensionHostScriptPath);

            _cts = new CancellationTokenSource();

            var psi = new ProcessStartInfo
            {
                FileName = "node",
                Arguments = $"\"{extensionHostScriptPath}\"",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardInputEncoding = Encoding.UTF8,
            };

            psi.Environment["VSCODE_EXTENSIONS"] = extensionsDir;
            psi.Environment["VSCODE_IPC_HOOK_EXTHOST"] = "pipe";

            _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _process.Exited += OnProcessExited;
            _process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    OnExtensionHostError(e.Data);
            };

            _process.Start();
            _process.BeginErrorReadLine();

            _readTask = Task.Run(() => ReadLoopAsync(_cts.Token), _cts.Token);
        }

        /// <summary>
        /// Sends a JSON-RPC request and awaits the response.
        /// </summary>
        public async Task<JsonElement?> SendRequestAsync(string method, object parameters, CancellationToken cancellationToken = default)
        {
            var id = Interlocked.Increment(ref _nextId);
            var tcs = new TaskCompletionSource<JsonElement?>();
            _pendingRequests[id] = tcs;

            var request = new JsonRpcRequest
            {
                Id = id,
                Method = method,
                Params = parameters != null
                    ? JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(parameters))
                    : (JsonElement?)null
            };

            SendMessage(request);

            using (cancellationToken.Register(() =>
            {
                if (_pendingRequests.TryRemove(id, out var t))
                    t.TrySetCanceled();
            }))
            {
                return await tcs.Task.ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Sends a JSON-RPC notification (fire-and-forget).
        /// </summary>
        public void SendNotification(string method, object parameters = null)
        {
            var notification = new JsonRpcNotification
            {
                Method = method,
                Params = parameters != null
                    ? JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(parameters))
                    : (JsonElement?)null
            };

            SendMessage(notification);
        }

        private void SendMessage(object message)
        {
            if (!IsRunning)
                throw new InvalidOperationException("Extension Host is not running.");

            var json = JsonSerializer.Serialize(message);
            var bytes = Encoding.UTF8.GetBytes(json);
            var header = $"Content-Length: {bytes.Length}\r\n\r\n";
            var headerBytes = Encoding.ASCII.GetBytes(header);

            lock (_writeLock)
            {
                var stdin = _process.StandardInput.BaseStream;
                stdin.Write(headerBytes, 0, headerBytes.Length);
                stdin.Write(bytes, 0, bytes.Length);
                stdin.Flush();
            }
        }

        private async Task ReadLoopAsync(CancellationToken cancellationToken)
        {
            var reader = _process.StandardOutput;

            while (!cancellationToken.IsCancellationRequested && !_process.HasExited)
            {
                try
                {
                    // Read Content-Length header
                    string headerLine;
                    int contentLength = -1;

                    while ((headerLine = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                    {
                        if (headerLine.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                        {
                            if (int.TryParse(headerLine.Substring(15).Trim(), out int len))
                                contentLength = len;
                        }
                        else if (string.IsNullOrEmpty(headerLine) && contentLength >= 0)
                        {
                            break;
                        }
                    }

                    if (contentLength <= 0)
                        continue;

                    var buffer = new char[contentLength];
                    int totalRead = 0;
                    while (totalRead < contentLength)
                    {
                        int read = await reader.ReadAsync(buffer, totalRead, contentLength - totalRead).ConfigureAwait(false);
                        if (read == 0) break;
                        totalRead += read;
                    }

                    var json = new string(buffer, 0, totalRead);
                    ProcessIncomingMessage(json);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    OnExtensionHostError($"Read loop error: {ex.Message}");
                }
            }
        }

        private void ProcessIncomingMessage(string json)
        {
            try
            {
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("id", out var idProp))
                {
                    // It's a response
                    object id = idProp.ValueKind == JsonValueKind.Number
                        ? (object)idProp.GetInt32()
                        : idProp.GetString();

                    if (_pendingRequests.TryRemove(id, out var tcs))
                    {
                        if (root.TryGetProperty("error", out var errorProp))
                        {
                            var error = JsonSerializer.Deserialize<JsonRpcError>(errorProp.GetRawText());
                            tcs.TrySetException(new Exception($"JSON-RPC error {error.Code}: {error.Message}"));
                        }
                        else
                        {
                            root.TryGetProperty("result", out var resultProp);
                            tcs.TrySetResult(resultProp.ValueKind == JsonValueKind.Undefined ? (JsonElement?)null : resultProp);
                        }
                    }
                }
                else if (root.TryGetProperty("method", out var methodProp))
                {
                    // It's a notification
                    var notification = new JsonRpcNotification
                    {
                        Method = methodProp.GetString()
                    };

                    if (root.TryGetProperty("params", out var paramsProp))
                        notification.Params = paramsProp;

                    NotificationReceived?.Invoke(this, notification);
                }
            }
            catch (Exception ex)
            {
                OnExtensionHostError($"Message parse error: {ex.Message}");
            }
        }

        private void OnProcessExited(object sender, EventArgs e)
        {
            foreach (var pending in _pendingRequests.Values)
                pending.TrySetException(new Exception("Extension Host process exited unexpectedly."));
            _pendingRequests.Clear();
        }

        protected virtual void OnExtensionHostError(string message)
        {
            // Subclasses or consumers can subscribe to logs
        }

        public void Stop()
        {
            _cts?.Cancel();

            try
            {
                if (_process != null && !_process.HasExited)
                {
                    _process.Kill();
                    _process.WaitForExit(3000);
                }
            }
            catch { }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            _process?.Dispose();
            _cts?.Dispose();
        }
    }
}
