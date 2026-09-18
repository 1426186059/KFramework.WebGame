using System.Collections.Generic;

namespace KFramework.MonoGame
{

    /// <summary>
    /// WebSocket 层：对浏览器原生 WebSocket 的 C# 封装（连接管理 + 收发二进制帧 + 事件）。
    /// <para>
    /// 底层经 JSBind_Net_WebSocket 桥接 KFramework.TSEngine 的 net_websocket 模块（薄 JS 层）。
    /// WebSocket 的所有事件都在主线程（JS 事件循环）上回调，无需跨线程同步。
    /// </para>
    /// 示例：
    /// <code>
    ///   var ws = new WebSocketClient();
    ///   ws.Opened += () => { /* 已连上 */ };
    ///   ws.MessageReceived += m => { /* m 是一帧原始字节 */ };
    ///   ws.Connect("wss://host/path");
    ///   ws.Send(System.Text.Encoding.UTF8.GetBytes("hello"));
    /// </code>
    /// </summary>
    public sealed class Net_WebSocket_Client
    {
        /// <summary>句柄 → 实例 路由表；由 JSBind_Net_WebSocket 的 [JSExport] 回调按句柄分发事件。</summary>
        internal static readonly Dictionary<int, Net_WebSocket_Client> s_instances = new Dictionary<int, Net_WebSocket_Client>();

        private int _handle = -1;

        public event Action? Opened;
        /// <summary>
        /// 收到一帧数据。
        /// <para>
        /// 小帧（≤ 64KB）：底层是运行时新分配的 byte[]，可以长期持有；
        /// 大帧（&gt; 64KB，见 Net_RecvBuffer）：走零拷贝通道，底层是共享的固定缓冲，
        /// <b>只在本次回调内有效</b>，需要留存请自行 ToArray()。
        /// </para>
        /// </summary>
        public event Action<ReadOnlyMemory<byte>>? MessageReceived;
        public event Action<int>? Closed;
        public event Action<string>? Error;

        /// <summary>
        /// 最近一次 <see cref="MessageReceived"/> 的数据是否来自大包的共享固定缓冲（<see cref="Net_RecvBuffer"/>）。
        /// <para>true = 走了零拷贝通道，这份数据<b>只在本次回调内有效</b>，要留存请自行 ToArray()；
        /// false = 运行时新分配的 byte[]，可长期持有。</para>
        /// </summary>
        public bool LastMessageFromSharedBuffer { get; private set; }

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

        internal void RaiseMessage(ReadOnlyMemory<byte> data, bool fromSharedBuffer)
        {
            LastMessageFromSharedBuffer = fromSharedBuffer;
            MessageReceived?.Invoke(data);
        }
        internal void RaiseClosed(int code) => Closed?.Invoke(code);
        internal void RaiseError(string message) => Error?.Invoke(message);
    }
}
