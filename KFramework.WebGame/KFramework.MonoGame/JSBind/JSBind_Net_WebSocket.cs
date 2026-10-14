using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{
    public static partial class JSBind_Net_WebSocket
    {
        [JSImport("netCreate", "net_websocket")]
        public static partial int NetCreate(string url);

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

        [JSExport]
        public static void OnBinaryMessage(int handle, byte[] data)
        {
            if (!Net_WebSocket_Client.s_instances.TryGetValue(handle, out var c)) return;
            c.RaiseMessage(data);
        }
    }
}
