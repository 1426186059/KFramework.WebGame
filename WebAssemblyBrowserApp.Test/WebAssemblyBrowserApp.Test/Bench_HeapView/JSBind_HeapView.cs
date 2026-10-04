using System;
using System.Runtime.InteropServices.JavaScript;

/// <summary>
/// <b>Bench_HeapView 模块专属</b>的互操作绑定，对应 wwwroot/bench_heapview.js。
/// <para>
/// 本模块验证的是"GCHandle pin + 裸地址 → JS TypedArray"这条零拷贝路线：
/// C# 把数组钉住后，把【内存地址 + 长度】交给 JS，由 JS 建出指向同一块内存的视图。
/// </para>
/// <para>
/// 【为什么这里不再有引子参数】
/// 取 WASM 的 <c>memory.buffer</c> 换过三版入口：扫全局（⑨ 证明必然失败）→
/// 借 MemoryView 的 <c>_unsafe_create_view()</c>（能用，但是 _unsafe 内部方法）→
/// 现在直接用<b>公开 API</b> <c>runtime.localHeapViewU8().buffer</c>。
/// 按 <c>export-api.ts:51</c> 与 <c>dotnet.d.ts:629</c> 它是正式公开接口，
/// 而 <c>marshal.ts:481</c> 显示 <c>_unsafe_create_view()</c> 内部就是它的一层包装
/// （Bench_RuntimeApi ⑤ 已实测两条路拿到的是同一块内存）。
/// 所以建视图的这几个函数<b>都不再收引子</b> —— 只有一个对照项 <see cref="ProbeViaKey"/> 还留着，
/// 用来把"两条路同一块"这件事摆在同一页上。
/// </para>
/// </summary>
public static partial class JSBind_HeapView
{
    /// <summary>
    /// ①-a 对照：只扫全局找 WASM memory.buffer。<b>预期一无所获</b>（⑨ 的结论）。
    /// 保留它是为了给"入口不在那几个全局符号上"提供同页对照证据，而不是靠记忆。
    /// </summary>
    [JSImport("probeGlobal", "bench_heapview")]
    public static partial string ProbeGlobal();

    /// <summary>
    /// ①-b 正解：公开 API <c>runtime.localHeapViewU8().buffer</c> —— 本方案现在的入口。
    /// </summary>
    [JSImport("probeViaPublic", "bench_heapview")]
    public static partial string ProbeViaPublic();

    /// <summary>
    /// ①-c 对照：前一版用过的引子绕道 <c>MemoryView._unsafe_create_view()</c>（_unsafe 内部方法）。
    /// 它<b>不再是必须</b>的，留着只为与 ①-b 摆在一起看：两条路拿到的是同一块 buffer。
    /// </summary>
    [JSImport("probeViaKey", "bench_heapview")]
    public static partial string ProbeViaKey([JSMarshalAs<JSType.MemoryView>] Span<byte> key);

    /// <summary>
    /// ② 附加校验：pin 到的这个地址是否<b>落在那块 buffer 之内</b>。
    /// 只看"ptr != 0"是不够的 —— 地址非零却越界，建视图照样失败，
    /// 而那种失败极易被读成"零拷贝不成立"。
    /// </summary>
    [JSImport("checkRange", "bench_heapview")]
    public static partial string CheckRange(nint ptr, int byteLength);

    /// <summary>
    /// 零拷贝：把「内存地址 + 元素个数 + 视图类型」交给 JS，建出指向同一块内存的 TypedArray。
    /// <para>JS 侧会先校验地址非 0、不越界、且按元素宽度对齐，三者任一不满足都会抛错。</para>
    /// </summary>
    [JSImport("createView", "bench_heapview")]
    public static partial JSObject CreateView(nint ptr, int length, string typedArrayName);

    /// <summary>拷贝：让 JS 先建视图再 slice() 一份出来，脱离 WASM 内存（多一次 memcpy）。</summary>
    [JSImport("copyArray", "bench_heapview")]
    public static partial JSObject CopyArray(nint ptr, int length, string typedArrayName);

    /// <summary>
    /// 让 JS 往指定视图里写入固定值（按视图元素宽度决定写法）。
    /// 若视图真指向 .NET 数组，C# 侧回读自己的数组就该看到这个值 —— 这是"零拷贝成立"的核心判据。
    /// </summary>
    [JSImport("writeView", "bench_heapview")]
    public static partial string WriteView(JSObject view, int value);

    /// <summary>让 JS 从指定视图读回前 count 个元素。</summary>
    [JSImport("readView", "bench_heapview")]
    public static partial string ReadView(JSObject view, int count);

    /// <summary>
    /// ⑧ 跨数组验证：用公开 API 的 buffer 去访问<b>另一个</b>被 pin 住的数组并写入。
    /// 写成功就证明那块 buffer 是整块线性内存，而不只是某个视图的私有区域。
    /// </summary>
    [JSImport("writeOther", "bench_heapview")]
    public static partial string WriteOther(nint ptr, int length, int value);

    /// <summary>
    /// ⑦-a 把 buffer 与视图<b>跨调用</b>存在 JS 侧，回报初始状态。
    /// 存下来才谈得上"下一步让堆增长，再回来看它死没死"。
    /// </summary>
    [JSImport("keepForGrow", "bench_heapview")]
    public static partial string KeepForGrow(nint ptr, int length);

    /// <summary>
    /// ⑦-b 只查询、不写入：存的 buffer / 视图有没有因为堆增长而 detach 或失效。
    /// </summary>
    [JSImport("inspectKept", "bench_heapview")]
    public static partial string InspectKept();

    /// <summary>
    /// ⑦-c 真正往存的视图里写一次 —— 只有这一步能证明视图是活是死。
    /// detached 的 buffer 会让视图 length 归零，此时读写<b>既不抛错也不生效</b>，
    /// 单看"没报错"会得出完全相反的结论。
    /// </summary>
    [JSImport("writeKept", "bench_heapview")]
    public static partial string WriteKept(int value);

    /// <summary>⑦-d 收尾：放掉 JS 侧跨调用持有的引用，别让它活到下一轮。</summary>
    [JSImport("releaseKept", "bench_heapview")]
    public static partial string ReleaseKept();

    /// <summary>
    /// ⑦-e 胁迫堆增长：调 emscripten 的 <c>Module._malloc</c> 占一块并<b>保持不放</b>。
    /// <para>
    /// 为什么不用 .NET 的 <c>new byte[]</c>：那走的是 .NET 的 GC 堆，
    /// GC 向 wasm 堆要内存有自己的策略，未必走到 <c>sbrk</c>/<c>memory.grow</c>
    /// （上一轮 96MB 都没逼出来，就是这个原因）。
    /// 而 <c>_malloc</c> 走 emscripten 自己的堆，耗尽时 <c>emscripten_resize_heap</c> →
    /// <c>sbrk</c> → <c>memory.grow</c> → <c>updateMemoryViews()</c> 重建 <c>Module.HEAPU8</c>，
    /// 于是<b>旧视图必然 detach</b> —— 这样才是确定性复现，而不是看运气。
    /// </para>
    /// <para>返回值以 "OK ——" 开头才算成功，否则 C# 侧会退回托管分配。</para>
    /// </summary>
    [JSImport("mallocPressure", "bench_heapview")]
    public static partial string MallocPressure(int bytes);

    /// <summary>⑦-f 放掉所有胁迫分配（必须在写完、回读完之后调用）。</summary>
    [JSImport("releasePressure", "bench_heapview")]
    public static partial string ReleasePressure();
}
