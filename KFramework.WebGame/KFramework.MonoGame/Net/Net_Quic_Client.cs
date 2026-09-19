using System.Collections.Generic;
using System.Runtime.InteropServices;
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
    /// <b>多流</b>：这是它与 WebSocket 最大的区别 —— 一个连接上可以同时跑多条可靠流，
    /// 流之间互不阻塞（QUIC 的队头阻塞只发生在单条流内）。因此：
    /// <list type="bullet">
    ///   <item>连接建立时会开好一条“主流”（<see cref="DefaultStreamId"/>），不带流号的 <see cref="Send(ReadOnlyMemory{byte})"/> 走它；</item>
    ///   <item><see cref="OpenStreamAsync"/> 可主动开流（双向 / 单向），返回的流号用于 <see cref="Send(int, ReadOnlyMemory{byte})"/>；</item>
    ///   <item>服务器主动发起的入站流（双向 / 单向）会经 <see cref="StreamOpened"/> 通知，可直接收、也可发（双向流）；</item>
    ///   <item>每条流可单独结束写端（<see cref="CloseStream"/>）或中止（<see cref="AbortStream"/>）。</item>
    /// </list>
    /// </para>
    /// <para>
    /// 另注意：流上 <c>reader.read()</c> 返回的是<b>分块</b>，不是消息边界 —— 一个 1MB 的逻辑包完全可能被切成
    /// 若干块分别回调。上层需要自带长度前缀（或自己的分包协议），WebSocket 那边由浏览器做了消息重组，这里没有。
    /// </para>
    /// 示例：
    /// <code>
    ///   var quic = new Net_Quic_Client();
    ///   quic.Opened += () => { /* 已连上，quic.DefaultStreamId 可直接发 */ };
    ///   quic.StreamOpened += (id, kind) => Console.WriteLine($"流 {id} 可用（{kind}）");
    ///   quic.MessageReceived += (streamId, data) =&gt; { /* 处理这一块字节 */ };
    ///   quic.Connect("https://host/path");          // 注意：WebTransport 用 https（HTTP/3），服务端须支持
    ///   quic.Send(System.Text.Encoding.UTF8.GetBytes("hello"));
    ///
    ///   // 另开一条流做独立通道（例如资源下载），不阻塞主消息流
    ///   int id = await quic.OpenStreamAsync();
    ///   await quic.Send(id, payload);
    ///   quic.CloseStream(id);   // 发完 FIN，对端知道这条流结束了
    /// </code>
    /// </summary>
    public sealed class Net_Quic_Client
    {
        /// <summary>句柄 → 实例 路由表；由 JSBind_Net_Quic 的 [JSExport] 回调按句柄分发事件。</summary>
        internal static readonly Dictionary<int, Net_Quic_Client> s_instances = new Dictionary<int, Net_Quic_Client>();

        private int _handle = -1;

        /// <summary>等待“流建立完成”的开流请求（流号 → 完成时设置结果）。</summary>
        private readonly Dictionary<int, TaskCompletionSource<int>> _opening = new Dictionary<int, TaskCompletionSource<int>>();

        public event Action? Opened;

        /// <summary>
        /// 有流可用时回调：(streamId, 流方向)。
        /// 三种来源：连接主流、本地 <see cref="OpenStreamAsync"/> 建好、服务器主动发起的入站流。
        /// </summary>
        public event Action<int, Net_Quic_StreamKind>? StreamOpened;

        /// <summary>收到一块数据：底层是运行时新分配的 byte[]，可以长期持有。</summary>
        public event Action<int, ReadOnlyMemory<byte>>? MessageReceived;

        /// <summary>单条流结束：(streamId, code)。code 0 = 对端正常 FIN，非 0 = 异常结束。</summary>
        public event Action<int, int>? StreamClosed;

        public event Action<int>? Closed;
        public event Action<string>? Error;

        /// <summary>
        /// 主流号（连接建立时开好的第一条双向流）；不带流号的 <see cref="Send(ReadOnlyMemory{byte})"/> 走它。
        /// 连接尚未建好时为 0。
        /// </summary>
        public int DefaultStreamId { get; private set; }

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

        /// <summary>
        /// 主动开一条流。建流是异步的：返回的 Task 在 <see cref="StreamOpened"/> 报出该流号时完成，
        /// 拿到流号后即可 <see cref="Send(int, ReadOnlyMemory{byte})"/>。
        /// </summary>
        /// <param name="unidirectional">true = 单向流（只能写，对端收）；false = 双向流（可收可发）。</param>
        public Task<int> OpenStreamAsync(bool unidirectional = false)
        {
            if (_handle < 0) throw new InvalidOperationException("尚未连接，请先 Connect(url)");

            int streamId = JSBind_Net_Quic.QuicOpenStream(_handle, unidirectional);
            if (streamId <= 0) throw new InvalidOperationException("开流失败：连接未就绪");

            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            _opening[streamId] = tcs;
            return tcs.Task;
        }

        /// <summary>在主流上发送一帧；连接未就绪时返回 false。</summary>
        public bool Send(ReadOnlyMemory<byte> data) => Send(DefaultStreamId, data);

        /// <summary>往指定流发送一帧；流不存在 / 是只读单向流 / 写端已结束时返回 false。</summary>
        public bool Send(int streamId, ReadOnlyMemory<byte> data)
        {
            if (_handle < 0) return false;

            // 同 Net_WebSocket_Client.Send：整段数组直接复用，避免多余的 ToArray() 复制
            if (MemoryMarshal.TryGetArray(data, out ArraySegment<byte> segment)
                && segment.Array is { } array && segment.Offset == 0 && segment.Count == data.Length)
            {
                return JSBind_Net_Quic.QuicSend(_handle, streamId, array);
            }

            return JSBind_Net_Quic.QuicSend(_handle, streamId, data.ToArray());
        }

        /// <summary>结束流的写端（发 FIN，仍可继续收对端数据）。</summary>
        public bool CloseStream(int streamId)
        {
            if (_handle < 0) return false;
            return JSBind_Net_Quic.QuicCloseStream(_handle, streamId);
        }

        /// <summary>中止流（立即重置，双方都不再收发）。</summary>
        public bool AbortStream(int streamId, int code = 0)
        {
            if (_handle < 0) return false;
            return JSBind_Net_Quic.QuicAbortStream(_handle, streamId, code);
        }

        /// <summary>查询流的方向；流不存在或已结束返回 null。</summary>
        public Net_Quic_StreamKind? StreamKind(int streamId)
        {
            if (_handle < 0) return null;
            int kind = JSBind_Net_Quic.QuicStreamKind(_handle, streamId);
            return kind < 0 ? null : (Net_Quic_StreamKind)kind;
        }

        /// <summary>关闭整个连接（其上所有流一并结束）。</summary>
        public void Close()
        {
            if (_handle >= 0)
            {
                JSBind_Net_Quic.QuicClose(_handle);
                s_instances.Remove(_handle);
                _handle = -1;
            }
            FailPendingOpens("连接已关闭");
        }

        internal void RaiseOpened() => Opened?.Invoke();

        internal void RaiseStreamOpened(int streamId, Net_Quic_StreamKind kind)
        {
            // 主流先到（连接建立时），之后本地开流/入站流各报一次
            if (DefaultStreamId == 0) DefaultStreamId = streamId;

            if (_opening.Remove(streamId, out var tcs)) tcs.TrySetResult(streamId);
            StreamOpened?.Invoke(streamId, kind);
        }

        internal void RaiseStreamClosed(int streamId, int code)
        {
            if (_opening.Remove(streamId, out var tcs))
                tcs.TrySetException(new InvalidOperationException($"流 {streamId} 建立后即被关闭"));

            StreamClosed?.Invoke(streamId, code);
        }

        internal void RaiseMessage(int streamId, ReadOnlyMemory<byte> data)
            => MessageReceived?.Invoke(streamId, data);

        internal void RaiseClosed(int code)
        {
            FailPendingOpens("连接已关闭");
            Closed?.Invoke(code);
        }

        internal void RaiseError(string message) => Error?.Invoke(message);

        /// <summary>连接已结束：把还在等“流建立”的请求全部置为失败，避免 Task 永久挂起。</summary>
        private void FailPendingOpens(string reason)
        {
            if (_opening.Count == 0) return;

            foreach (var tcs in _opening.Values)
                tcs.TrySetException(new InvalidOperationException($"开流未完成：{reason}"));
            _opening.Clear();
        }
    }
}
