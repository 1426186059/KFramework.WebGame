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
    public static partial class JSBind_Net_WebSocket
    {
        [JSImport("netCreate", "net_websocket")]
        public static partial int NetCreate(string url);

        /// <summary>
        /// 发送一帧二进制数据（C# → JS）。
        /// <para>
        /// 参数用 <c>Span&lt;byte&gt;</c> + <c>MemoryView</c>，而<b>不是</b> <c>byte[]</c>：
        /// <c>byte[]</c> 每次调用都要在 JS 堆上按帧大小新建一个 Uint8Array 再把数据拷过去；
        /// MemoryView 只封送一个视图对象，JS 侧用 <c>copyTo</c> 直接写进它自己的复用缓冲
        /// —— 省掉"按帧大小"的那次分配（见 net_websocket.ts 的 netSend）。
        /// </para>
        /// <para>
        /// 方向差异：只有 <b>C# → JS</b> 能用 MemoryView。<b>JS → C#</b>（见 <see cref="OnBinaryMessage"/>）
        /// 用不了 —— 浏览器只能给出 Uint8Array，而运行时要求的是它内部的 MemoryView 对象。
        /// </para>
        /// <para>
        /// JS 侧只 <c>copyTo</c> 读出、不回写；<c>Span</c> 的 MemoryView 只在【同步调用期间】有效
        /// （不 pin 托管数组），故本方法必须同步 —— 它确实是（返回 bool，不跨 await）。
        /// </para>
        /// </summary>
        [JSImport("netSend", "net_websocket")]
        public static partial bool NetSend(int handle, [JSMarshalAs<JSType.MemoryView>] Span<byte> data);

        [JSImport("netClose", "net_websocket")]
        public static partial void NetClose(int handle);

        [JSImport("netState", "net_websocket")]
        public static partial int NetState(int handle);

        [JSExport]
        public static void OnOpen(int handle)
        {
            if (Net_WebSocket_Client.s_instances.TryGetValue(handle, out var c)) c.RaiseOpened();
        }

        [JSExport]
        public static void OnClose(int handle, int code)
        {
            if (Net_WebSocket_Client.s_instances.TryGetValue(handle, out var c))
            {
                Net_WebSocket_Client.s_instances.Remove(handle);
                c.RaiseClosed(code);
            }
        }

        [JSExport]
        public static void OnError(int handle, string message)
        {
            if (Net_WebSocket_Client.s_instances.TryGetValue(handle, out var c)) c.RaiseError(message);
        }

        // 【JS→C# 方向不能用 MemoryView —— 实测结论，非猜测】
        // JS 侧只能给出 Uint8Array，而运行时要求的是它内部的 MemoryView 对象
        // （断言 "Expected MemoryViewType.Byte"），浏览器端也没有公开的 createMemoryView。
        // 实测见 WebAssemblyBrowserApp.Test 的 Bench_JsToCs.JsToCs_FillSpan：该路由被直接拒绝。
        // 所以本方向只能走 byte[] 封送：运行时每次调用生成一个新的 byte[]（一次分配 + 一次拷贝），
        // 好处是业务层可以长期持有，不必在同步调用期内用完。
        // 注：byte[] 不加 JSMarshalAs —— 生成器对 byte[] 有特化，JS 侧直接给 Uint8Array 即可。
        [JSExport]
        public static void OnBinaryMessage(int handle, byte[] data)
        {
            if (!Net_WebSocket_Client.s_instances.TryGetValue(handle, out var c)) return;
            c.RaiseMessage(data ?? Array.Empty<byte>());
        }
    }
}
