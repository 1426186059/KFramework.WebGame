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

        [JSImport("netSend", "net_websocket")]
        public static partial bool NetSend(int handle, byte[] data);

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

        // JS→C# 方向不能用 MemoryView（JS 侧给的是 Uint8Array，而运行时要求 IMemoryView 对象，
        // 见 dotnet.runtime.js 的 "Expected MemoryViewType.Byte" 断言，浏览器端也没有公开的
        // createMemoryView API），只能走 byte[] 封送：运行时每次调用生成一个新的 byte[]，业务层可长期持有。
        // 注：byte[] 不加 JSMarshalAs —— 生成器对 byte[] 有特化，JS 侧直接给 Uint8Array 即可。
        [JSExport]
        public static void OnBinaryMessage(int handle, byte[] data)
        {
            if (!Net_WebSocket_Client.s_instances.TryGetValue(handle, out var c)) return;
            c.RaiseMessage(data ?? Array.Empty<byte>());
        }
    }
}
