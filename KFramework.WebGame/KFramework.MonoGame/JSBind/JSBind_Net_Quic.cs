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
    /// 与 JSBind_Net_WebSocket 同构，仅模块名与底层客户端不同。
    /// </summary>
    internal static partial class JSBind_Net_Quic
    {
        [JSImport("quicCreate", "net_quic")]
        internal static partial int QuicCreate(string url);

        [JSImport("quicSend", "net_quic")]
        internal static partial bool QuicSend(int handle, byte[] data);

        [JSImport("quicClose", "net_quic")]
        internal static partial void QuicClose(int handle);

        [JSImport("quicState", "net_quic")]
        internal static partial int QuicState(int handle);

        // 大包零拷贝通道：同 JSBind_Net_WebSocket.NetRead，把固定缓冲递给 JS 直接写入。
        [JSImport("quicRead", "net_quic")]
        internal static partial void QuicRead(int handle, int offset, int length, [JSMarshalAs<JSType.MemoryView>] Span<byte> target);

        [JSExport]
        internal static void OnOpen(int handle)
        {
            if (Net_Quic_Client.s_instances.TryGetValue(handle, out var c)) c.RaiseOpened();
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

        // 同 JSBind_Net_WebSocket：JS→C# 方向不能用 MemoryView（JS 侧给的是 Uint8Array），
        // 统一用 JSType.Array 由运行时封送成新的 byte[]。
        // 注：byte[] 不加 JSMarshalAs —— 生成器对 byte[] 有特化，JS 侧直接给 Uint8Array 即可。
        [JSExport]
        internal static void OnBinaryMessage(int handle, byte[] data)
        {
            if (!Net_Quic_Client.s_instances.TryGetValue(handle, out var c)) return;
            c.RaiseMessage(data ?? Array.Empty<byte>(), fromSharedBuffer: false);
        }

        /// <summary>大包零拷贝通道，同 <see cref="JSBind_Net_WebSocket.OnBigMessage"/>。</summary>
        [JSExport]
        internal static void OnBigMessage(int handle, int offset, int length)
        {
            if (!Net_Quic_Client.s_instances.TryGetValue(handle, out var c)) return;
            if (length <= 0)
            {
                c.RaiseMessage(ReadOnlyMemory<byte>.Empty, fromSharedBuffer: false);
                return;
            }

            byte[] buffer = Net_RecvBuffer.Ensure(length);
            QuicRead(handle, offset, length, buffer.AsSpan(0, length));
            // 共享缓冲：只在本次回调内有效（见 Net_RecvBuffer 的说明）
            c.RaiseMessage(buffer.AsMemory(0, length), fromSharedBuffer: true);
        }
    }
}
