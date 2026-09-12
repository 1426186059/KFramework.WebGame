using System;
using System.Runtime.InteropServices.JavaScript;

namespace MirEngine;

/// <summary>
/// 手写 JS WebSocket 的 C# 封装。对应 tsengine/src/core/websocket.ts（mir.ws* 函数）。
/// 设计镜像 KNet.WebSocket 的 WebGL 轮询模型：JS 把二进制帧入队，C# 每帧 PollMessage 取出，
/// 避免 JS 回调线程直接调用 C# 的同步问题。浏览器端唯一可用的 WebSocket 实现。
/// </summary>
public static partial class BrowserWebSocket
{
    [JSImport("mir.wsConnect", "main.js")]
    private static partial int ConnectImpl(string url);

    [JSImport("mir.wsClose", "main.js")]
    private static partial void CloseImpl(int instanceId);

    [JSImport("mir.wsSend", "main.js")]
    private static partial int SendImpl(int instanceId, byte[] data);

    [JSImport("mir.wsGetState", "main.js")]
    private static partial int GetStateImpl(int instanceId);

    [JSImport("mir.wsReceive", "main.js")]
    private static partial byte[] ReceiveImpl(int instanceId);

    /// <summary>建立连接，返回实例 id（&lt;=0 表示失败）。</summary>
    public static int Connect(string url) => ConnectImpl(url);

    /// <summary>关闭连接并释放 JS 端实例。</summary>
    public static void Close(int instanceId) => CloseImpl(instanceId);

    /// <summary>发送二进制帧，返回 1 成功 / 0 失败（未打开）。</summary>
    public static int Send(int instanceId, byte[] data) => SendImpl(instanceId, data);

    /// <summary>返回 readyState：0 连接中 / 1 OPEN / 2 CLOSING / 3 CLOSED。</summary>
    public static int GetState(int instanceId) => GetStateImpl(instanceId);

    /// <summary>取出下一条完整消息的字节（一条 Mir Packet）；无消息返回空数组。</summary>
    public static byte[] Receive(int instanceId) => ReceiveImpl(instanceId) ?? Array.Empty<byte>();
}
