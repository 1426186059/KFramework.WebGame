using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{

    /// <summary>
    /// WebSocket 网络层 C# ⇄ JS 绑定（JSImport 集中地，业务不要直接调用）。
    /// <list type="bullet">
    ///   <item>[JSImport] 映射到 KFramework.TSEngine/src/net_websocket.ts 的 "net_websocket" 模块（薄 JS 层）；</item>
    ///   <item>[JSExport] 由 net_websocket.ts 经 setHandlers 注册的回调反向调用，把 WebSocket 事件推回 C#。</item>
    /// </list>
    /// 事件按连接句柄（handle）路由到对应的 WebSocketClient 实例。
    /// </summary>
    internal static partial class JSBind_Net_WebSocket
    {
        [JSImport("netCreate", "net_websocket")]
        internal static partial int NetCreate(string url);

        [JSImport("netSend", "net_websocket")]
        internal static partial bool NetSend(int handle, byte[] data);

        [JSImport("netClose", "net_websocket")]
        internal static partial void NetClose(int handle);

        [JSImport("netState", "net_websocket")]
        internal static partial int NetState(int handle);

        // 大包零拷贝通道（C#→JS 方向的 MemoryView）：把 Net_RecvBuffer 的固定缓冲递给 JS，
        // JS 按 (ArrayBuffer, offset, length) 直接写进这块 WASM 堆内存，不再走 JS→C# 的封送。
        [JSImport("netRead", "net_websocket")]
        internal static partial void NetRead(int handle, int offset, int length, [JSMarshalAs<JSType.MemoryView>] Span<byte> target);

        [JSExport]
        internal static void OnOpen(int handle)
        {
            if (Net_WebSocket_Client.s_instances.TryGetValue(handle, out var c)) c.RaiseOpened();
        }

        [JSExport]
        internal static void OnClose(int handle, int code)
        {
            if (Net_WebSocket_Client.s_instances.TryGetValue(handle, out var c))
            {
                Net_WebSocket_Client.s_instances.Remove(handle);
                c.RaiseClosed(code);
            }
        }

        [JSExport]
        internal static void OnError(int handle, string message)
        {
            if (Net_WebSocket_Client.s_instances.TryGetValue(handle, out var c)) c.RaiseError(message);
        }

        // 小包（<= Net_RecvBuffer.BigPacketThreshold）：JS 侧（net_websocket.js）传进来的是 Uint8Array，
        // 只能用 byte[] 封送 —— JSType.MemoryView 的 JS→C# 方向要求传 IMemoryView 对象（运行时会校验
        // _viewType，见 dotnet.runtime.js 的 "Expected MemoryViewType.Byte" 断言），而浏览器端没有公开的
        // createMemoryView API 可用。运行时每次调用都会生成一个新的 byte[]，业务层可以长期持有。
        // 注：byte[] 不加 JSMarshalAs —— 生成器对 byte[] 有特化，JS 侧直接给 Uint8Array 即可。
        [JSExport]
        internal static void OnBinaryMessage(int handle, byte[] data)
        {
            if (!Net_WebSocket_Client.s_instances.TryGetValue(handle, out var c)) return;
            c.RaiseMessage(data ?? Array.Empty<byte>(), fromSharedBuffer: false);
        }

        /// <summary>
        /// 大包（&gt; BigPacketThreshold）通道：JS 只回传 (offset, length)，数据仍在它那一侧的
        /// ArrayBuffer 里；C# 把固定缓冲作为 MemoryView 递过去，让 JS 直接写进 WASM 堆。
        /// 这样托管堆不为大包分配数组，也绕开了 JS→C# 的 MemoryView 类型校验。
        /// </summary>
        [JSExport]
        internal static void OnBigMessage(int handle, int offset, int length)
        {
            if (!Net_WebSocket_Client.s_instances.TryGetValue(handle, out var c)) return;
            if (length <= 0)
            {
                c.RaiseMessage(ReadOnlyMemory<byte>.Empty, fromSharedBuffer: false);
                return;
            }

            byte[] buffer = Net_RecvBuffer.Ensure(length);
            NetRead(handle, offset, length, buffer.AsSpan(0, length));
            // 共享缓冲：只在本次回调内有效（见 Net_RecvBuffer 的说明）
            c.RaiseMessage(buffer.AsMemory(0, length), fromSharedBuffer: true);
        }
    }
}
