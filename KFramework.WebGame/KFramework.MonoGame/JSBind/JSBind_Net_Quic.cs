using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{

    /// <summary>
    /// QUIC 网络层（浏览器侧即 WebTransport）C# ⇄ JS 绑定（JSImport 集中地，业务不要直接调用）。
    /// <list type="bullet">
    ///   <item>[JSImport] 映射到 KFramework.TSEngine/src/net_quic.ts 的 "net_quic" 模块（薄 JS 层）；</item>
    ///   <item>[JSExport] 由 net_quic.ts 经 setHandlers 注册的回调反向调用，把 WebTransport 事件推回 C#。</item>
    /// </list>
    /// 事件按连接句柄（handle）路由到对应的 QuicClient 实例。
    /// <para>
    /// 与 WebSocket 最大的差别：WebTransport 是<b>多流</b>的，一个连接上可以有任意多条流，
    /// 因此收包事件都带 streamId，另有流的建立/结束通知。
    /// </para>
    /// </summary>
    internal static partial class JSBind_Net_Quic
    {
        [JSImport("quicCreate", "net_quic")]
        internal static partial int QuicCreate(string url);

        /// <summary>往指定流发送；streamId &lt;= 0 表示主流。</summary>
        [JSImport("quicSend", "net_quic")]
        internal static partial bool QuicSend(int handle, int streamId, byte[] data);

        /// <summary>开一条新流（异步建立，建好后由 <see cref="OnStreamOpen"/> 回报）；返回流号，失败返回 0。</summary>
        [JSImport("quicOpenStream", "net_quic")]
        internal static partial int QuicOpenStream(int handle, bool unidirectional);

        /// <summary>结束流的写端（发 FIN，仍可继续收）。</summary>
        [JSImport("quicCloseStream", "net_quic")]
        internal static partial bool QuicCloseStream(int handle, int streamId);

        /// <summary>中止流（立即重置）。</summary>
        [JSImport("quicAbortStream", "net_quic")]
        internal static partial bool QuicAbortStream(int handle, int streamId, int code);

        [JSImport("quicClose", "net_quic")]
        internal static partial void QuicClose(int handle);

        [JSImport("quicState", "net_quic")]
        internal static partial int QuicState(int handle);

        /// <summary>返回流的类型（<see cref="Net_Quic_StreamKind"/>）；流不存在或已结束返回 -1。</summary>
        [JSImport("quicStreamKind", "net_quic")]
        internal static partial int QuicStreamKind(int handle, int streamId);

        [JSExport]
        internal static void OnOpen(int handle)
        {
            if (Net_Quic_Client.s_instances.TryGetValue(handle, out var c)) c.RaiseOpened();
        }

        [JSExport]
        internal static void OnStreamOpen(int handle, int streamId, int kind)
        {
            if (Net_Quic_Client.s_instances.TryGetValue(handle, out var c))
                c.RaiseStreamOpened(streamId, (Net_Quic_StreamKind)kind);
        }

        [JSExport]
        internal static void OnStreamClose(int handle, int streamId, int code)
        {
            if (Net_Quic_Client.s_instances.TryGetValue(handle, out var c)) c.RaiseStreamClosed(streamId, code);
        }

        [JSExport]
        internal static void OnClose(int handle, int code)
        {
            if (Net_Quic_Client.s_instances.TryGetValue(handle, out var c))
            {
                Net_Quic_Client.s_instances.Remove(handle);
                c.RaiseClosed(code);
            }
        }

        [JSExport]
        internal static void OnError(int handle, string message)
        {
            if (Net_Quic_Client.s_instances.TryGetValue(handle, out var c)) c.RaiseError(message);
        }

        // JS→C# 方向不能用 MemoryView（JS 侧给的是 Uint8Array，而运行时要求 IMemoryView 对象），
        // 统一走 byte[] 封送：每次调用生成一个新的 byte[]，业务层可以长期持有。
        // 注：byte[] 不加 JSMarshalAs —— 生成器对 byte[] 有特化，JS 侧直接给 Uint8Array 即可。
        [JSExport]
        internal static void OnBinaryMessage(int handle, int streamId, byte[] data)
        {
            if (!Net_Quic_Client.s_instances.TryGetValue(handle, out var c)) return;
            c.RaiseMessage(streamId, data ?? Array.Empty<byte>());
        }
    }
}
