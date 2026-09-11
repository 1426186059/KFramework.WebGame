using System;

namespace Library.Network
{
    /// <summary>
    /// 网络传输模式。浏览器 WASM 下只能用手写 JS WebSocket；
    /// 非浏览器（如将来把同一套客户端代码编到桌面）才能用 C# 自带的 ClientWebSocket。
    /// </summary>
    public enum NetworkMode
    {
        /// <summary>浏览器→JS WebSocket；非浏览器→Managed WebSocket（C# ClientWebSocket）。</summary>
        Auto = 0,
        /// <summary>手写 JS WebSocket（浏览器 WebSocket API，经 [JSImport] mir.ws* 驱动）。</summary>
        JsWebSocket = 1,
        /// <summary>C# 自带 ClientWebSocket（仅非浏览器运行时可用）。</summary>
        ManagedWebSocket = 2,
    }

    /// <summary>
    /// 字节流传输抽象，取代原 BaseConnection 直接依赖的 TcpClient/Socket。
    /// 实现：JsWebSocketTransport（手写 JS）、ManagedWebSocketTransport（C# ClientWebSocket）。
    /// 采用轮询模型：JS/后台线程把收到的二进制帧入队，C# 在每帧 Process() 里 Receive() 取出。
    /// </summary>
    public interface INetworkTransport : IDisposable
    {
        /// <summary>当前是否已建立可收发连接（OPEN）。</summary>
        bool IsConnected { get; }

        /// <summary>连接状态：0 连接中 / 1 已打开 / 2 关闭中 / 3 已关闭（与浏览器 WebSocket.readyState 对齐）。</summary>
        int State { get; }

        /// <summary>发起连接（异步；连接结果通过轮询 State/IsConnected 获得）。</summary>
        void Connect(string host, int port);

        /// <summary>发送二进制数据（一条 Mir Packet 的字节）。同步语义，内部负责 WS 二进制帧。</summary>
        void Send(byte[] buffer, int offset, int count);

        /// <summary>
        /// 轮询接收：把下一段待处理字节（最多 buffer.Length）拷入 buffer（从 0 开始），返回实际拷贝字节数；无数据返回 0。
        /// 单条 WS 消息可能跨多次 Receive 调用（内部保留偏移），调用方无需关心分片。
        /// </summary>
        int Receive(byte[] buffer);

        /// <summary>主动断开连接。</summary>
        void Disconnect();
    }
}
