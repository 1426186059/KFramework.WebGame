using System.Runtime.InteropServices;

namespace KFramework.MonoGame
{
    public sealed class Net_WebSocket_Client
    {
        internal static readonly Dictionary<int, Net_WebSocket_Client> s_instances = new Dictionary<int, Net_WebSocket_Client>();

        private int _handle = -1;
        public event Action? Opened;
        public event Action<ReadOnlyMemory<byte>>? MessageReceived;
        public event Action<int>? Closed;
        public event Action<string>? Error;

        /// <summary>连接是否已建立（readyState == OPEN）。</summary>
        public bool IsOpen
        {
            get
            {
                if (_handle < 0) return false;
                return JSBind_Net_WebSocket.NetState(_handle) == 1; // WebSocket.OPEN
            }
        }

        /// <summary>异步建立连接；返回的 Task 在 OnOpen 时完成，在 OnError 时异常结束。</summary>
        public Task ConnectAsync(string url)
        {
            var tcs = new TaskCompletionSource();
            void OnOpened() { Opened -= OnOpened; Error -= OnErrored; tcs.TrySetResult(); }
            void OnErrored(string _) { Opened -= OnOpened; Error -= OnErrored; tcs.TrySetException(new InvalidOperationException("WebSocket 连接失败")); }
            Opened += OnOpened;
            Error += OnErrored;
            Connect(url);
            return tcs.Task;
        }

        /// <summary>开始连接（结果通过 Opened / Error 事件异步回报）。</summary>
        public void Connect(string url)
        {
            _handle = JSBind_Net_WebSocket.NetCreate(url);
            s_instances[_handle] = this;
        }

        /// <summary>发送一帧二进制数据；连接未就绪时返回 false。</summary>
        public bool Send(ReadOnlyMemory<byte> data)
        {
            if (_handle < 0) return false;

            // 传进来的本来就是整段数组（最常见的情况）时直接复用，别再 ToArray() 多复制一份
            if (MemoryMarshal.TryGetArray(data, out ArraySegment<byte> segment)
                && segment.Array is { } array && segment.Offset == 0 && segment.Count == data.Length)
            {
                return JSBind_Net_WebSocket.NetSend(_handle, array);
            }

            return JSBind_Net_WebSocket.NetSend(_handle, data.ToArray());
        }

        /// <summary>关闭连接。</summary>
        public void Close()
        {
            if (_handle >= 0)
            {
                JSBind_Net_WebSocket.NetClose(_handle);
                s_instances.Remove(_handle);
                _handle = -1;
            }
        }

        internal void RaiseOpened() => Opened?.Invoke();

        internal void RaiseMessage(ReadOnlyMemory<byte> data) => MessageReceived?.Invoke(data);
        internal void RaiseClosed(int code) => Closed?.Invoke(code);
        internal void RaiseError(string message) => Error?.Invoke(message);
    }
}
