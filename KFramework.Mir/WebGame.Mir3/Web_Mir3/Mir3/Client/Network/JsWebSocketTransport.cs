using Library.Network;
using System;
using MirEngine;

namespace Client.Network
{
    /// <summary>
    /// 手写 JS WebSocket 传输（浏览器唯一可用的 WebSocket 实现）。
    /// 经 [JSImport] mir.ws* 驱动浏览器原生 WebSocket，采用轮询模型：
    /// JS 把收到的二进制帧（一条 Mir Packet）入队，C# 每帧 Receive() 取出。
    /// </summary>
    public sealed class JsWebSocketTransport : INetworkTransport
    {
        private int _instanceId = -1;
        private byte[] _retained;       // 当前消息尚未拷贝完的剩余字节
        private int _retainedOffset;

        public bool IsConnected => _instanceId > 0 && BrowserWebSocket.GetState(_instanceId) == 1;
        public int State => _instanceId > 0 ? BrowserWebSocket.GetState(_instanceId) : 3;

        public void Connect(string host, int port)
        {
            // Console.WriteLine($"[WS-DIAG] JsWebSocketTransport.Connect ws://{host}:{port}/");
            _instanceId = BrowserWebSocket.Connect($"ws://{host}:{port}/");
            // Console.WriteLine($"[WS-DIAG] JsWebSocketTransport.Connect => instanceId={_instanceId}");
        }

        public void Send(byte[] buffer, int offset, int count)
        {
            if (_instanceId <= 0 || count <= 0)
            {
                // Console.WriteLine($"[WS-DIAG] JsWebSocketTransport.Send skipped: id={_instanceId} count={count}");
                return;
            }

            byte[] slice = new byte[count];
            Buffer.BlockCopy(buffer, offset, slice, 0, count);
            int r = BrowserWebSocket.Send(_instanceId, slice);
            // Console.WriteLine($"[WS-DIAG] JsWebSocketTransport.Send: id={_instanceId} len={count} result={r}");
        }

        public int Receive(byte[] buffer)
        {
            if (_instanceId <= 0) return 0;

            if (_retained == null || _retainedOffset >= _retained.Length)
            {
                byte[] msg = BrowserWebSocket.Receive(_instanceId);
                if (msg == null || msg.Length == 0) return 0;
                _retained = msg;
                _retainedOffset = 0;
                // Console.WriteLine($"[WS-DIAG] JsWebSocketTransport.Receive: new msg len={msg.Length}");
            }

            int n = Math.Min(_retained.Length - _retainedOffset, buffer.Length);
            Buffer.BlockCopy(_retained, _retainedOffset, buffer, 0, n);
            _retainedOffset += n;
            return n;
        }

        public void Disconnect()
        {
            // Console.WriteLine($"[WS-DIAG] JsWebSocketTransport.Disconnect: id={_instanceId}");
            if (_instanceId > 0)
            {
                BrowserWebSocket.Close(_instanceId);
                _instanceId = -1;
            }
            _retained = null;
            _retainedOffset = 0;
        }

        public void Dispose() => Disconnect();
    }
}
