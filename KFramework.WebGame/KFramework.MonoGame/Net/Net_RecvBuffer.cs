namespace KFramework.MonoGame
{

    /// <summary>
    /// 大包（超过 <see cref="BigPacketThreshold"/> 的帧）接收用的共享固定缓冲。
    /// <para>
    /// 为什么需要它：JS→C# 方向没法用 MemoryView（见 JSBind_Net_WebSocket.OnBinaryMessage 的注释），
    /// 小包由运行时封送成 byte[]（一次分配 + 一次拷贝）没问题，但大包每帧都新建大数组，
    /// GC 压力和大对象分配都不划算。
    /// </para>
    /// <para>
    /// 做法：这里用 <c>GC.AllocateArray(..., pinned: true)</c> 在固定堆上常驻一块缓冲，
    /// 它的地址就在 WASM 线性内存里且不会被 GC 搬动（只有扩容时才会换一块）。收大包时
    /// C# 把它作为 MemoryView 递给 JS（C#→JS 方向的 MemoryView 是运行时支持的），
    /// JS 用 view.set() 把浏览器的 ArrayBuffer 按偏移/长度直接写进这块内存，
    /// C# 侧按切片读取即可 —— 全程只有一次 JS→WASM 的拷贝，托管堆不再额外分配。
    /// </para>
    /// <para>
    /// 注意：缓冲是所有连接共用的（WebSocket / WebTransport 的事件都在 JS 主线程上串行回调），
    /// 因此大包数据只在本次 MessageReceived 回调内有效，业务若要留存请自行 ToArray()。
    /// </para>
    /// </summary>
    internal static class Net_RecvBuffer
    {
        /// <summary>
        /// 大包阈值（字节）：超过它的帧走零拷贝通道，否则走 byte[] 封送。
        /// JS 侧 KFramework.TSEngine/src/net_websocket.ts、net_quic.ts 里的 BIG_PACKET_LIMIT
        /// 必须与此保持一致（不一致也只影响走哪条通道，不会出错）。
        /// </summary>
        internal const int BigPacketThreshold = 64 * 1024;

        private const int InitialCapacity = 256 * 1024;

        private static byte[] s_buffer = GC.AllocateArray<byte>(InitialCapacity, pinned: true);

        /// <summary>返回一块容量不小于 size 的固定缓冲（必要时扩容）。</summary>
        internal static byte[] Ensure(int size)
        {
            if (size <= 0) return s_buffer;

            if (s_buffer.Length < size)
            {
                int capacity = s_buffer.Length;
                while (capacity < size)
                {
                    capacity *= 2;
                }
                s_buffer = GC.AllocateArray<byte>(capacity, pinned: true);
            }

            return s_buffer;
        }
    }
}
