using Library.Network;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Client.Network
{
    /// <summary>
    /// C# 自带 WebSocket 客户端传输（System.Net.WebSockets.ClientWebSocket）。
    /// 仅在非浏览器运行时可用（浏览器 WASM 下 ConnectAsync 会抛 PlatformNotSupportedException）。
    /// 后台 Task 接收完整 binary 消息并入队，Receive() 轮询取出——与 JsWebSocketTransport 保持同一接口。
    /// </summary>
    public sealed class ManagedWebSocketTransport : INetworkTransport
    {
        private ClientWebSocket _ws;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly ConcurrentQueue<byte[]> _incoming = new ConcurrentQueue<byte[]>();
        private byte[] _retained;
        private int _retainedOffset;

        public bool IsConnected => _ws != null && _ws.State == WebSocketState.Open && !_cts.IsCancellationRequested;

        public int State
        {
            get
            {
                if (_ws == null) return 3;
                return _ws.State switch
                {
                    WebSocketState.None => 0,
                    WebSocketState.Connecting => 0,
                    WebSocketState.Open => 1,
                    WebSocketState.CloseSent => 2,
                    WebSocketState.CloseReceived => 2,
                    _ => 3,
                };
            }
        }

        public void Connect(string host, int port)
        {
            _ws = new ClientWebSocket();
            var uri = new Uri($"ws://{host}:{port}/");
            _ = _ws.ConnectAsync(uri, _cts.Token).ContinueWith(_ => { }, TaskScheduler.Default);
            _ = Task.Run(ReceiveLoop);
        }

        private async Task ReceiveLoop()
        {
            var buf = new byte[8192];
            try
            {
                while (!_cts.IsCancellationRequested && _ws != null && _ws.State == WebSocketState.Open)
                {
                    using var ms = new MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _ws.ReceiveAsync(new ArraySegment<byte>(buf), _cts.Token).ConfigureAwait(false);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            Disconnect();
                            return;
                        }
                        ms.Write(buf, 0, result.Count);
                    } while (!result.EndOfMessage);

                    _incoming.Enqueue(ms.ToArray());
                }
            }
            catch
            {
                Disconnect();
            }
        }

        public void Send(byte[] buffer, int offset, int count)
        {
            if (_ws == null || _ws.State != WebSocketState.Open) return;
            try
            {
                _ws.SendAsync(new ArraySegment<byte>(buffer, offset, count), WebSocketMessageType.Binary, true, _cts.Token)
                   .GetAwaiter().GetResult();
            }
            catch
            {
                Disconnect();
            }
        }

        public int Receive(byte[] buffer)
        {
            if (_retained == null || _retainedOffset >= _retained.Length)
            {
                if (!_incoming.TryDequeue(out var msg) || msg.Length == 0) return 0;
                _retained = msg;
                _retainedOffset = 0;
            }

            int n = Math.Min(_retained.Length - _retainedOffset, buffer.Length);
            Buffer.BlockCopy(_retained, _retainedOffset, buffer, 0, n);
            _retainedOffset += n;
            return n;
        }

        public void Disconnect()
        {
            try { _cts.Cancel(); } catch { }

            if (_ws != null)
            {
                try { _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None).GetAwaiter().GetResult(); } catch { }
                try { _ws.Dispose(); } catch { }
                _ws = null;
            }
            _retained = null;
            _retainedOffset = 0;
        }

        public void Dispose() => Disconnect();
    }
}
