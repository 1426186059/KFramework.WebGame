using System.Collections.Generic;
using System.Threading.Tasks;

namespace KFramework.MonoGame
{

    /// <summary>
    /// QUIC 传输层（浏览器侧即 WebTransport，基于 HTTP/3）：对浏览器原生 WebTransport 的 C# 封装。
    /// <para>
    /// 底层经 JSBind_Net_Quic 桥接 KFramework.TSEngine 的 net_quic 模块（薄 JS 层）。
    /// 所有事件都在主线程（JS 事件循环）上回调，无需跨线程同步。
    /// </para>
    /// <para>
    /// 使用 WebTransport 的“可靠双向流”（createBidirectionalStream）作为收发主通道：有序、不丢包，语义对齐 WebSocket，
    /// 适合游戏消息。不可靠 datagram 未在本客户端暴露（游戏消息通常需要可靠有序）。
    /// </para>
    /// 示例：
    /// <code>
    ///   var quic = new QuicClient();
    ///   quic.Opened += () => { /* 已连上 */ };
    ///   quic.MessageReceived += m => { /* m 是一帧原始字节 */ };
    ///   quic.Connect("https://host/path");          // 注意：WebTransport 用 https（HTTP/3），服务端须支持
    ///   quic.Send(System.Text.Encoding.UTF8.GetBytes("hello"));
    /// </code>
    /// </summary>
    public sealed class Net_Quic_Client
    {
        /// <summary>句柄 → 实例 路由表；由 JSBind_Net_Quic 的 [JSExport] 回调按句柄分发事件。</summary>
        internal static readonly Dictionary<int, Net_Quic_Client> s_instances = new Dictionary<int, Net_Quic_Client>();

        private int _handle = -1;

        public event Action? Opened;
        public event Action<ReadOnlyMemory<byte>>? MessageReceived;
        public event Action<int>? Closed;
        public event Action<string>? Error;

        /// <summary>连接是否已建立（state == OPEN）。</summary>
        public bool IsOpen
        {
            get
            {
                if (_handle < 0) return false;
                return JSBind_Net_Quic.QuicState(_handle) == 1; // OPEN
            }
        }

        /// <summary>异步建立连接；返回的 Task 在 OnOpen 时完成，在 OnError 时异常结束。</summary>
        public Task ConnectAsync(string url)
        {
            var tcs = new TaskCompletionSource();
            void OnOpened() { Opened -= OnOpened; Error -= OnErrored; tcs.TrySetResult(); }
            void OnErrored(string _) { Opened -= OnOpened; Error -= OnErrored; tcs.TrySetException(new InvalidOperationException("WebTransport 连接失败")); }
            Opened += OnOpened;
            Error += OnErrored;
            Connect(url);
            return tcs.Task;
        }

        /// <summary>开始连接（结果通过 Opened / Error 事件异步回报）。</summary>
        public void Connect(string url)
        {
            _handle = JSBind_Net_Quic.QuicCreate(url);
            s_instances[_handle] = this;
        }

        /// <summary>发送一帧二进制数据（datagram）；连接未就绪时返回 false。</summary>
        public bool Send(ReadOnlyMemory<byte> data)
        {
            if (_handle < 0) return false;
            return JSBind_Net_Quic.QuicSend(_handle, data.ToArray());
        }

        /// <summary>关闭连接。</summary>
        public void Close()
        {
            if (_handle >= 0)
            {
                JSBind_Net_Quic.QuicClose(_handle);
                s_instances.Remove(_handle);
                _handle = -1;
            }
        }

        internal void RaiseOpened() => Opened?.Invoke();
        internal void RaiseMessage(byte[] data) => MessageReceived?.Invoke(data);
        internal void RaiseClosed(int code) => Closed?.Invoke(code);
        internal void RaiseError(string message) => Error?.Invoke(message);
    }
}
