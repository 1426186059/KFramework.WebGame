using System;
using System.Runtime.InteropServices.JavaScript;

/// <summary>
/// <b>Bench_ZeroCopy 模块专属</b>的互操作绑定，对应 wwwroot/bench_zerocopy.js。
/// <para>
/// 本模块测的是"把一块数据交到 JS 手上"这件事，<b>零拷贝与非零拷贝差多少</b>：
/// <list type="bullet">
/// <item>零拷贝：C# 用 <see cref="GCHandle"/> 钉住数组，把（地址, 长度）给 JS，
/// JS 用公开 API <c>localHeapViewU8().buffer</c> 建出指向同一块内存的视图 —— 零 memcpy。</item>
/// <item>非零拷贝：C# 把 <c>Span&lt;byte&gt;</c> 传过去（运行时封送成 MemoryView），
/// JS 用 <c>copyTo</c> 或 <c>slice</c> 拷一份 —— 一次 memcpy（slice 还多一次分配）。</item>
/// </list>
/// </para>
/// <para>
/// 【为什么每个函数都只"读首末各一个字节"就返回】
/// 若让 JS 拿到后把整块求和，那个 O(n) 的 JS 循环会把 memcpy 的 O(n) 成本完全淹没
/// （memcpy 高度优化，JS 逐字节循环比它慢得多），几条路线测出来会几乎一样快 ——
/// 那是"看着跑通了、其实没测到点子上"。所以后续动作统一取 O(1)，
/// 耗时差异才能纯粹反映<b>搬运方式</b>本身。
/// </para>
/// <para>
/// 四个函数返回同一个校验值（首字节 | 末字节&lt;&lt;8），C# 侧会拿它和自己的算法对账 ——
/// 对不上就说明那条路线读到的不是这块数据。
/// </para>
/// </summary>
public static partial class JSBind_ZeroCopy
{
    /// <summary>
    /// 零拷贝：JS 用「公开 API 的 buffer + C# 钉住的地址」建视图，读首末各一个字节。
    /// <para>视图现场建、当场用、用完扔 —— 官方注释原话：
    /// Don't store the reference, don't use it after await。</para>
    /// </summary>
    [JSImport("peekZeroCopy", "bench_zerocopy")]
    public static partial int PeekZeroCopy(nint ptr, int length);

    /// <summary>
    /// 零拷贝（缓存视图）：<b>(ptr, length) 没变就复用上一次的视图</b>。
    /// <para>
    /// 既然用的是指针，指针不变则视图也不必每次重建 —— 这正是引擎里最典型的形态：
    /// 一块固定的帧缓冲 / 顶点缓冲，逐帧反复传。省掉的不只是建对象的开销，
    /// 还有它带来的 JS 垃圾（每次 <c>new Uint8Array</c> 都在喂 V8 的新生代回收）。
    /// </para>
    /// <para>
    /// 顺带处理了 detach：堆增长后旧 buffer 被摘掉，视图 length 会归零，此时必须重建。
    /// </para>
    /// </summary>
    [JSImport("peekZeroCopyCached", "bench_zerocopy")]
    public static partial int PeekZeroCopyCached(nint ptr, int length);

    /// <summary>
    /// <b>跨界底噪</b>：什么都不做，立刻返回一个常数。
    /// 有了它才能把"过一次界"这件事本身的价钱量出来 —— 各路线都含它，
    /// 扣掉之后剩下的才是各自真正的增量成本。
    /// </summary>
    [JSImport("peekNoop", "bench_zerocopy")]
    public static partial int PeekNoop();

    /// <summary>
    /// 非零拷贝之一：<c>MemoryView.copyTo</c> 拷进 JS 侧<b>复用</b>的缓冲（1 次 memcpy，无分配）。
    /// <para>复用是为了把"分配"这项成本排除掉，测出来的才是纯 memcpy 的代价。</para>
    /// </summary>
    [JSImport("peekCopyTo", "bench_zerocopy")]
    public static partial int PeekCopyTo([JSMarshalAs<JSType.MemoryView>] Span<byte> data);

    /// <summary>
    /// 非零拷贝之二：<c>MemoryView.slice()</c> 拿一份<b>新副本</b>（1 次 memcpy + 每调用一次就新分配一块）。
    /// <para>这里刻意<b>不复用</b>缓冲 —— 每次新分配本就是 slice 的成本之一，要如实计入。</para>
    /// </summary>
    [JSImport("peekSlice", "bench_zerocopy")]
    public static partial int PeekSlice([JSMarshalAs<JSType.MemoryView>] Span<byte> data);

    /// <summary>
    /// 基线：JS 内部的纯 memcpy（<c>dst.set(src)</c>），全程不碰 WASM 内存。
    /// 它是<b>解释性</b>的 —— 零拷贝省掉的，正是这一块的量级。
    /// </summary>
    [JSImport("peekJsMemcpy", "bench_zerocopy")]
    public static partial int PeekJsMemcpy(int length);
}
