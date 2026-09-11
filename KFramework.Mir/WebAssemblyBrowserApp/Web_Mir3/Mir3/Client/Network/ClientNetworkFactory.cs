using System;
using Library.Network;
using Client.Envir;

namespace Client.Network
{
    /// <summary>
    /// 根据 Config.NetworkMode 创建对应的 INetworkTransport，使「手写 JS WebSocket」与
    /// 「C# 自带 ClientWebSocket」两者可选。Auto 模式下浏览器自动落到 JS 版，非浏览器落到 Managed 版。
    /// </summary>
    public static class ClientNetworkFactory
    {
        public static INetworkTransport Create()
        {
            NetworkMode mode = Config.NetworkMode;

            if (mode == NetworkMode.Auto)
                mode = OperatingSystem.IsBrowser() ? NetworkMode.JsWebSocket : NetworkMode.ManagedWebSocket;

            switch (mode)
            {
                case NetworkMode.ManagedWebSocket:
                    return new ManagedWebSocketTransport();
                case NetworkMode.JsWebSocket:
                default:
                    return new JsWebSocketTransport();
            }
        }

        public static string ResolveHost() => Config.UseNetworkConfig ? Config.IPAddress : Config.DefaultIPAddress;
        public static int ResolvePort() => Config.UseNetworkConfig ? Config.Port : Config.DefaultPort;
    }
}
