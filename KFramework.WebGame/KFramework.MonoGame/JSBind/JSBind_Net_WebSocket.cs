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

        [JSExport]
        internal static void OnBinaryMessage(int handle, [JSMarshalAs<JSType.MemoryView>] Span<byte> data)
        {
            if (!Net_WebSocket_Client.s_instances.TryGetValue(handle, out var c)) return;
            // MemoryView 只在调用期间有效，必须立即拷出。
            var copy = new byte[data.Length];
            data.CopyTo(copy);
            c.RaiseMessage(copy);
        }
    }
}
