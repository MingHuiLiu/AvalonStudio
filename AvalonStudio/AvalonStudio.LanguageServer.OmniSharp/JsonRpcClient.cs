using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AvalonStudio.LanguageServer.OmniSharp.Protocol
{
    /// <summary>
    /// Lightweight JSON-RPC 2.0 client that communicates over a pair of
    /// stdin/stdout streams (LSP framing: Content-Length header + blank line).
    /// </summary>
    public class JsonRpcClient : IDisposable
    {
        private readonly Stream _input;
        private readonly Stream _output;
        private readonly ConcurrentDictionary<int, TaskCompletionSource<JToken>> _pending
            = new ConcurrentDictionary<int, TaskCompletionSource<JToken>>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private int _nextId;
        private readonly JsonSerializer _serializer = new JsonSerializer
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        public event Action<string, JToken> NotificationReceived;

        public JsonRpcClient(Stream serverOutput, Stream serverInput)
        {
            _input = serverOutput;   // we read from the server's stdout
            _output = serverInput;   // we write to the server's stdin
        }

        /// <summary>Starts the background read loop.</summary>
        public void Start()
        {
            Task.Run(() => ReadLoopAsync(_cts.Token), _cts.Token);
        }

        /// <summary>Sends a request and returns the result token.</summary>
        public async Task<JToken> SendRequestAsync(string method, object @params,
            CancellationToken ct = default)
        {
            int id = Interlocked.Increment(ref _nextId);
            var tcs = new TaskCompletionSource<JToken>();
            _pending[id] = tcs;

            ct.Register(() =>
            {
                if (_pending.TryRemove(id, out _))
                    tcs.TrySetCanceled();
            });

            var msg = new JsonRpcRequest { Id = id, Method = method, Params = @params };
            await WriteMessageAsync(msg).ConfigureAwait(false);

            return await tcs.Task.ConfigureAwait(false);
        }

        /// <summary>Sends a request and deserialises the result to <typeparamref name="T"/>.</summary>
        public async Task<T> SendRequestAsync<T>(string method, object @params,
            CancellationToken ct = default)
        {
            var token = await SendRequestAsync(method, @params, ct).ConfigureAwait(false);
            if (token == null || token.Type == JTokenType.Null) return default;
            return token.ToObject<T>(_serializer);
        }

        /// <summary>Sends a notification (no response expected).</summary>
        public Task SendNotificationAsync(string method, object @params)
        {
            var msg = new JsonRpcRequest { Id = null, Method = method, Params = @params };
            return WriteMessageAsync(msg);
        }

        // ─── private ──────────────────────────────────────────────────────────

        private async Task WriteMessageAsync(object message)
        {
            string json = JsonConvert.SerializeObject(message, Formatting.None,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            byte[] body = Encoding.UTF8.GetBytes(json);
            byte[] header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");

            byte[] frame = new byte[header.Length + body.Length];
            Buffer.BlockCopy(header, 0, frame, 0, header.Length);
            Buffer.BlockCopy(body, 0, frame, header.Length, body.Length);

            lock (_output)
            {
                _output.Write(frame, 0, frame.Length);
                _output.Flush();
            }
        }

        private async Task ReadLoopAsync(CancellationToken ct)
        {
            var headerBuilder = new StringBuilder(128);
            var buffer = new byte[1];

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // Read headers until empty line
                    int contentLength = -1;
                    headerBuilder.Clear();
                    string lastLine = null;

                    while (true)
                    {
                        string line = await ReadLineAsync(_input, ct).ConfigureAwait(false);
                        if (line == null) return; // stream closed

                        if (line.Length == 0 && lastLine?.Length == 0)
                        {
                            // Two blank lines - treat as separator
                            break;
                        }

                        if (line.Length == 0)
                        {
                            // Single blank line ends the header block
                            break;
                        }

                        if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                        {
                            if (int.TryParse(line.Substring("Content-Length:".Length).Trim(),
                                out int cl))
                            {
                                contentLength = cl;
                            }
                        }

                        lastLine = line;
                    }

                    if (contentLength < 0) continue;

                    // Read body
                    byte[] body = new byte[contentLength];
                    int offset = 0;
                    while (offset < contentLength)
                    {
                        int read = await _input.ReadAsync(body, offset, contentLength - offset, ct)
                            .ConfigureAwait(false);
                        if (read == 0) return;
                        offset += read;
                    }

                    string json = Encoding.UTF8.GetString(body);
                    JObject obj;
                    try
                    {
                        obj = JObject.Parse(json);
                    }
                    catch
                    {
                        continue;
                    }

                    DispatchMessage(obj);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (IOException)
                {
                    break;
                }
                catch
                {
                    // Swallow unexpected parse errors and continue
                }
            }
        }

        private void DispatchMessage(JObject obj)
        {
            if (obj.ContainsKey("id") && obj["id"].Type != JTokenType.Null)
            {
                // Response
                int id = obj["id"].Value<int>();
                if (_pending.TryRemove(id, out var tcs))
                {
                    if (obj.ContainsKey("error") && obj["error"].Type != JTokenType.Null)
                    {
                        var err = obj["error"].ToObject<JsonRpcError>();
                        tcs.TrySetException(new JsonRpcException(err.Code, err.Message));
                    }
                    else
                    {
                        tcs.TrySetResult(obj["result"]);
                    }
                }
            }
            else if (obj.ContainsKey("method"))
            {
                // Notification
                string method = obj["method"].Value<string>();
                NotificationReceived?.Invoke(method, obj["params"]);
            }
        }

        private static async Task<string> ReadLineAsync(Stream stream, CancellationToken ct)
        {
            var sb = new StringBuilder();
            var buf = new byte[1];

            while (true)
            {
                int read = await stream.ReadAsync(buf, 0, 1, ct).ConfigureAwait(false);
                if (read == 0) return null;

                char ch = (char)buf[0];
                if (ch == '\n')
                {
                    // Strip trailing \r
                    if (sb.Length > 0 && sb[sb.Length - 1] == '\r')
                        sb.Remove(sb.Length - 1, 1);
                    return sb.ToString();
                }

                sb.Append(ch);
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();

            foreach (var kv in _pending)
                kv.Value.TrySetCanceled();

            _pending.Clear();
        }
    }

    public class JsonRpcException : Exception
    {
        public int Code { get; }

        public JsonRpcException(int code, string message) : base(message)
        {
            Code = code;
        }
    }
}
