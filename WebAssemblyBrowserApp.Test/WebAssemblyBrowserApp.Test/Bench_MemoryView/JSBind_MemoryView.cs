using System;
using System.Runtime.InteropServices.JavaScript;

/// <summary>
/// <b>Bench_MemoryView 模块专属</b>的互操作绑定，对应 wwwroot/bench_memoryview.js。
/// <para>
/// 本模块【不测耗时】，只测"MemoryView 在 JS 侧究竟有哪几种写法、各自是不是真动到了托管内存"：
/// 每种写法一个 [JSImport]，C# 都传进同一份已知内容的 byte[8]，JS 用该写法操作它，
/// C# 再回读自己的数组 —— 字节变没变，才是这条写法"干没干活"的判据。
/// </para>
/// <para>
/// 各测试模块的跨界入口一律自带一份、不共用（理由见 Bench_CsToJs/JSBind_CsToJs.cs）。
/// 本模块只有 [JSImport]（C# 单向调出去），无需给方法名加模块前缀。
/// </para>
/// </summary>
public static partial class JSBind_MemoryView
{
    /// <summary>写法① <c>view.set(src, offset)</c> —— 官方的<b>写入</b>路径（JS → 托管内存，零拷贝）。</summary>
    [JSImport("probeSet", "bench_memoryview")]
    public static partial string ProbeSet([JSMarshalAs<JSType.MemoryView>] Span<byte> buffer);

    /// <summary>写法② <c>view.copyTo(target)</c> —— 官方的<b>读出</b>路径（托管内存 → JS 的 TypedArray）。</summary>
    [JSImport("probeCopyTo", "bench_memoryview")]
    public static partial string ProbeCopyTo([JSMarshalAs<JSType.MemoryView>] Span<byte> buffer);

    /// <summary>
    /// 写法③ <c>view.slice()</c> —— 关键争议点：它返回的是<b>副本</b>还是共享内存的<b>视图</b>？
    /// JS 侧会顺手改一下返回值，再由 C# 回读自己的数组来定性。
    /// </summary>
    [JSImport("probeSlice", "bench_memoryview")]
    public static partial string ProbeSlice([JSMarshalAs<JSType.MemoryView>] Span<byte> buffer);

    /// <summary>写法④ <c>view.length</c> / <c>view.byteLength</c> —— 两个尺寸属性各是什么。</summary>
    [JSImport("probeMeta", "bench_memoryview")]
    public static partial string ProbeMeta([JSMarshalAs<JSType.MemoryView>] Span<byte> buffer);

    /// <summary>写法⑤ <c>view[i]</c> 读、<c>view[i] = v</c> 写 —— 传闻"没有索引器"，实测。</summary>
    [JSImport("probeIndex", "bench_memoryview")]
    public static partial string ProbeIndex([JSMarshalAs<JSType.MemoryView>] Span<byte> buffer);

    /// <summary>
    /// 写法⑥ <c>view.buffer</c> —— 若存在，就能零拷贝建出共享视图（TypedArray 的常规做法）。实测存不存在。
    /// </summary>
    [JSImport("probeBuffer", "bench_memoryview")]
    public static partial string ProbeBuffer([JSMarshalAs<JSType.MemoryView>] Span<byte> buffer);

    /// <summary>
    /// 写法⑦ <c>view._unsafe_create_view()</c> —— 运行时内部方法。
    /// <para>
    /// 实现见 dotnet/runtime 的 <c>src/mono/browser/runtime/marshal.ts:481</c>。
    /// 官方类型声明已复制到 <c>KFramework.TSEngine/src/dotnet-runtime.d.ts</c>（从 runtime 源码原样复制），
    /// 但 <c>_unsafe_create_view</c> 不在公开的 <c>IMemoryView</c> 上 —— 这正是它"带 _unsafe 前缀"的含义。
    /// 实测能否调用、写入是否回写。
    /// </para>
    /// </summary>
    [JSImport("probeUnsafe", "bench_memoryview")]
    public static partial string ProbeUnsafe([JSMarshalAs<JSType.MemoryView>] Span<byte> buffer);

    /// <summary>
    /// 写法⑧-a 第一步：把 <b>Span</b> 的视图交给 JS 存起来（不 pin 托管数组）。
    /// 第二步由 <see cref="ProbeUseStored"/> 取出来再写，看它还活着没有。
    /// </summary>
    [JSImport("probeStore", "bench_memoryview")]
    public static partial string ProbeStoreSpan([JSMarshalAs<JSType.MemoryView>] Span<byte> buffer);

    /// <summary>
    /// 写法⑧-b 第一步：同一段 JS 逻辑，改传 <b>ArraySegment</b>（会 pin 托管数组，用完 dispose）。
    /// 与 ⑧-a 并排 —— 正是"想跨调用 / 长期持有该用哪个"的对照。
    /// </summary>
    [JSImport("probeStore", "bench_memoryview")]
    public static partial string ProbeStoreSegment([JSMarshalAs<JSType.MemoryView>] ArraySegment<byte> buffer);

    /// <summary>写法⑧ 第二步：取出上次存下的视图，再写一次（两种传法共用这一步）。</summary>
    [JSImport("probeUseStored", "bench_memoryview")]
    public static partial string ProbeUseStored();

    /// <summary>
    /// 写法⑨：JS 侧能否直接访问 WASM 线性内存（Emscripten 的 <c>HEAPU8</c> 那种零拷贝视图）。
    /// 有一种说法是"直接 new Uint8Array(wasmMemory.buffer, ptr, len) 即可零拷贝"——
    /// 它依赖运行时把堆视图挂到全局，而 .NET 的 WASM 运行时并不这么做。实测本工程里有没有这些入口。
    /// </summary>
    [JSImport("probeHeap", "bench_memoryview")]
    public static partial string ProbeHeap();

    /// <summary>
    /// 写法⑩：<c>_unsafe_create_view()</c> 返回值的身份细节 —— 构造函数名、是不是 Uint8Array、有没有 .buffer。
    /// ⑦ 已证明"写入会回写 C#"；按参考定义它内部是
    /// <c>new Uint8Array(localHeapViewU8().buffer, ptr, len)</c>，那么 .buffer 就应当是 WASM 线性内存。
    /// </summary>
    [JSImport("probeUnsafeDetail", "bench_memoryview")]
    public static partial string ProbeUnsafeDetail([JSMarshalAs<JSType.MemoryView>] Span<byte> buffer);

    /// <summary>
    /// 写法⑪（灵魂测试）：借"引子"MemoryView 取出 WASM buffer，再去访问【另一个】被 pin 住的 .NET 数组。
    /// <para>
    /// 写成功能证明那个 .buffer 是<b>整块线性内存</b>（而非引子自己那一小段）——
    /// 如此一来 HeapView 方案里最难的 getWasmMemoryBuffer() 就有了正解：
    /// 不必扫全局，从任意一个 MemoryView 身上取即可。
    /// </para>
    /// </summary>
    [JSImport("probeHeapBridge", "bench_memoryview")]
    public static partial string ProbeHeapBridge([JSMarshalAs<JSType.MemoryView>] Span<byte> key, nint ptr, int length);
}
