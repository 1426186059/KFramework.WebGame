using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Library.Network
{
    /// <summary>
    /// 把 WebSocket 包装成 Stream，使上层 BaseConnection 的字节流收发逻辑无需改动。
    /// 服务端在已建立的 TCP 流上先完成 HTTP 升级握手，再用 WebSocket.CreateFromStream（框架自带，无 NuGet）创建。
    /// 这样服务器对外就是 WebSocket，浏览器 WASM 客户端可直接连接。
    ///
    /// 关键点：BaseConnection 的收发走的是 Stream.BeginRead/BeginWrite（异步回调模型）。
    /// 因此这里必须真正重写 BeginRead/BeginWrite/EndRead/EndWrite，使用 WebSocket 原生的
    /// ReceiveAsync/SendAsync，并且不在游戏主线程上同步阻塞（避免 SendAsync().GetResult() 死锁）。
    /// </summary>
    public sealed class WebSocketStream : Stream
    {
        private readonly WebSocket _ws;
        private bool _disposed;

        public WebSocketStream(WebSocket webSocket)
        {
            _ws = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
        }

        public override bool CanRead =>
            !_disposed && (_ws.State == WebSocketState.Open || _ws.State == WebSocketState.CloseReceived || _ws.State == WebSocketState.CloseSent);
        public override bool CanSeek => false;
        public override bool CanWrite =>
            !_disposed && (_ws.State == WebSocketState.Open || _ws.State == WebSocketState.CloseReceived);
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        #region 异步读：包装 WebSocket.ReceiveAsync，跳过非二进制帧
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            var tcs = new TaskCompletionSource<int>(state, TaskCreationOptions.RunContinuationsAsynchronously);
            ReceiveLoop(buffer, offset, count, tcs, callback);
            return tcs.Task;
        }

        private void ReceiveLoop(byte[] buffer, int offset, int count, TaskCompletionSource<int> tcs, AsyncCallback callback)
        {
            if (_disposed)
            {
                tcs.TrySetResult(0);
                callback?.Invoke(tcs.Task);
                return;
            }

            _ws.ReceiveAsync(new ArraySegment<byte>(buffer, offset, count), CancellationToken.None).ContinueWith(t =>
            {
                if (t.IsFaulted || t.IsCanceled)
                {
                    Console.WriteLine($"[WS-DIAG] Read: exception -> return 0: {t.Exception?.GetBaseException().Message}");
                    tcs.TrySetResult(0);
                    callback?.Invoke(tcs.Task);
                    return;
                }

                var result = t.Result;

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Console.WriteLine($"[WS-DIAG] Read: received WS Close frame -> return 0");
                    tcs.TrySetResult(0);
                    callback?.Invoke(tcs.Task);
                    return;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    // 跳过文本帧，继续读，直到拿到二进制数据或关闭。
                    ReceiveLoop(buffer, offset, count, tcs, callback);
                    return;
                }

                Console.WriteLine($"[WS-DIAG] Read: got {result.Count} bytes, State={_ws.State}");
                tcs.TrySetResult(result.Count);
                callback?.Invoke(tcs.Task);
            }, TaskScheduler.Default);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            var task = (Task<int>)asyncResult;
            return task.GetAwaiter().GetResult();
        }
        #endregion

        #region 异步写：包装 WebSocket.SendAsync
        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            var tcs = new TaskCompletionSource<bool>(state, TaskCreationOptions.RunContinuationsAsynchronously);

            if (_disposed)
            {
                tcs.TrySetException(new ObjectDisposedException(nameof(WebSocketStream)));
                callback?.Invoke(tcs.Task);
                return tcs.Task;
            }

            _ws.SendAsync(new ArraySegment<byte>(buffer, offset, count), WebSocketMessageType.Binary, true, CancellationToken.None).ContinueWith(t =>
            {
                if (t.IsFaulted || t.IsCanceled)
                {
                    Console.WriteLine($"[WS-DIAG] Write FAILED: State={_ws.State}, {t.Exception?.GetBaseException().Message}");
                    tcs.TrySetException(t.Exception!);
                }
                else
                {
                    Console.WriteLine($"[WS-DIAG] Write OK: {count} bytes sent, State={_ws.State}");
                    tcs.TrySetResult(true);
                }
                callback?.Invoke(tcs.Task);
            }, TaskScheduler.Default);

            return tcs.Task;
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            var task = (Task<bool>)asyncResult;
            task.GetAwaiter().GetResult();
        }
        #endregion

        #region 同步 Read/Write 兜底（委托给上面的异步实现，避免任何直接阻塞点被绕过）
        public override int Read(byte[] buffer, int offset, int count)
        {
            return EndRead(BeginRead(buffer, offset, count, null, null));
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            EndWrite(BeginWrite(buffer, offset, count, null, null));
        }
        #endregion

        /// <summary>在已建立的 TCP 流上完成 WebSocket 握手，返回包装后的 Stream（握手失败抛异常）。</summary>
        public static WebSocketStream Accept(NetworkStream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            Console.WriteLine($"[WS-DIAG] Accept: new WebSocket upgrade request received");

            // 逐字节读取 HTTP 请求头直到 \r\n\r\n，避免 StreamReader 预读破坏紧随其后的 WS 帧。
            var headerBytes = new List<byte>(256);
            int b;
            while ((b = stream.ReadByte()) >= 0)
            {
                headerBytes.Add((byte)b);
                int n = headerBytes.Count;
                if (n >= 4 && headerBytes[n - 4] == '\r' && headerBytes[n - 3] == '\n' && headerBytes[n - 2] == '\r' && headerBytes[n - 1] == '\n')
                    break;
            }

            string headerText = Encoding.ASCII.GetString(headerBytes.ToArray());
            string key = null;
            foreach (var line in headerText.Split('\n'))
            {
                if (line.StartsWith("Sec-WebSocket-Key", StringComparison.OrdinalIgnoreCase))
                {
                    int idx = line.IndexOf(':');
                    if (idx >= 0) key = line.Substring(idx + 1).Trim();
                }
            }

            if (string.IsNullOrEmpty(key))
                throw new InvalidOperationException("请求不是有效的 WebSocket 升级请求。");

            Console.WriteLine($"[WS-DIAG] Accept: Sec-WebSocket-Key='{key}', computing accept + sending 101");
            string accept = ComputeAccept(key);
            string response = "HTTP/1.1 101 Switching Protocols\r\n" +
                             "Upgrade: websocket\r\n" +
                             "Connection: Upgrade\r\n" +
                             "Sec-WebSocket-Accept: " + accept + "\r\n\r\n";
            byte[] resp = Encoding.ASCII.GetBytes(response);
            stream.Write(resp, 0, resp.Length);
            Console.WriteLine($"[WS-DIAG] Accept: 101 response written to stream");

            WebSocket ws = WebSocket.CreateFromStream(stream, isServer: true, subProtocol: null, keepAliveInterval: TimeSpan.FromSeconds(30));
            Console.WriteLine($"[WS-DIAG] Accept: WebSocket.CreateFromStream done, State={ws.State}");
            return new WebSocketStream(ws);
        }

        private static string ComputeAccept(string key)
        {
            const string Guid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
            byte[] hash = SHA1.HashData(Encoding.ASCII.GetBytes(key + Guid));
            return Convert.ToBase64String(hash);
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed) return;
            _disposed = true;

            Console.WriteLine($"[WS-DIAG] Dispose: disposing, ws.State={_ws.State}");

            try
            {
                if (_ws.State == WebSocketState.Open || _ws.State == WebSocketState.CloseReceived)
                    _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WS-DIAG] Dispose: CloseAsync exception: {ex.Message}");
            }
            try { _ws.Abort(); } catch { }
            try { _ws.Dispose(); } catch { }

            base.Dispose(disposing);
        }
    }
}
