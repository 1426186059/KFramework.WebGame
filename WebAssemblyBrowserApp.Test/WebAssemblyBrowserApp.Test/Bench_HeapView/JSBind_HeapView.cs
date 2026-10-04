using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;

/// <summary>
/// <b>Bench_HeapView 模块专属</b>的互操作绑定，对应 wwwroot/bench_heapview.js。
/// <para>
/// 本模块验证的是"GCHandle pin + 裸地址 → JS TypedArray"这条零拷贝路线：
/// C# 把数组钉住后，把【内存地址 + 长度】交给 JS，由 JS 建出指向同一块内存的视图。
/// </para>
/// <para>
/// 【为什么几乎每个函数都要多收一个 key 参数】
/// Bench_MemoryView 的 ⑨ 已实测：.NET 的 BrowserApp 不把 wasmMemory / HEAPU8 挂在全局，
/// 扫全局【必然一无所获】；而 ⑩⑪ 实测：任意一个 MemoryView 的
/// <c>_unsafe_create_view().buffer</c> 就是<b>整块 WASM 线性内存</b>。
/// 所以这里的 key 就是那个"引子" —— 一个内容无关紧要的 <c>Span&lt;byte&gt;</c>，
/// 传过去只为让 JS 侧换出一个 MemoryView，进而从它身上取到 buffer。
/// </para>
/// </summary>
public static partial class JSBind_HeapView
{
    /// <summary>
    /// ①-a 对照：只扫全局找 WASM memory.buffer。<b>预期一无所获</b>（⑨ 的结论）。
    /// 保留它是为了给"必须借道 MemoryView"提供同页对照证据，而不是靠记忆。
    /// </summary>
    [JSImport("probeGlobal", "bench_heapview")]
    public static partial string ProbeGlobal();

    /// <summary>
    /// ①-b 正解：借引子 MemoryView 取 WASM memory.buffer —— 本方案真正的入口。
    /// </summary>
    [JSImport("probeViaKey", "bench_heapview")]
    public static partial string ProbeViaKey([JSMarshalAs<JSType.MemoryView>] Span<byte> key);

    /// <summary>
    /// ② 附加校验：pin 到的这个地址是否<b>落在那块 buffer 之内</b>。
    /// 只看"ptr != 0"是不够的 —— 地址非零却越界，建视图照样失败，
    /// 而那种失败极易被读成"零拷贝不成立"。
    /// </summary>
    [JSImport("checkRange", "bench_heapview")]
    public static partial string CheckRange([JSMarshalAs<JSType.MemoryView>] Span<byte> key, nint ptr, int byteLength);

    /// <summary>
    /// 零拷贝：把「引子 + 内存地址 + 元素个数 + 视图类型」交给 JS，建出指向同一块内存的 TypedArray。
    /// <para>JS 侧会先校验地址非 0、不越界、且按元素宽度对齐，三者任一不满足都会抛错。</para>
    /// </summary>
    [JSImport("createView", "bench_heapview")]
    public static partial JSObject CreateView([JSMarshalAs<JSType.MemoryView>] Span<byte> key, nint ptr, int length, string typedArrayName);

    /// <summary>拷贝：让 JS 先建视图再 slice() 一份出来，脱离 WASM 内存（多一次 memcpy）。</summary>
    [JSImport("copyArray", "bench_heapview")]
    public static partial JSObject CopyArray([JSMarshalAs<JSType.MemoryView>] Span<byte> key, nint ptr, int length, string typedArrayName);

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
    /// ⑧ 跨数组验证：用引子的 buffer 去访问<b>另一个</b>被 pin 住的数组并写入。
    /// 写成功就证明那块 buffer 是整块线性内存，而不是引子自己那一小段。
    /// </summary>
    [JSImport("writeOther", "bench_heapview")]
    public static partial string WriteOther([JSMarshalAs<JSType.MemoryView>] Span<byte> key, nint ptr, int length, int value);

    /// <summary>
    /// ⑦-a 把 buffer 与视图<b>跨调用</b>存在 JS 侧，回报初始状态。
    /// 存下来才谈得上"下一步让堆增长，再回来看它死没死"。
    /// </summary>
    [JSImport("keepForGrow", "bench_heapview")]
    public static partial string KeepForGrow([JSMarshalAs<JSType.MemoryView>] Span<byte> key, nint ptr, int length);

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
}
