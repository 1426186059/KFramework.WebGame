using System;
using System.Runtime.InteropServices.JavaScript;

/// <summary>
/// <b>Bench_RuntimeApi 模块专属</b>的互操作绑定，对应 wwwroot/bench_runtimeapi.js。
/// <para>
/// 本模块探测的是 dotnet/runtime 里<b>本来就公开</b>的那套 WASM 内存 API：
/// <c>localHeapViewU8()</c> 系列、<c>setHeapU8</c>/<c>getHeapU8</c> 系列，
/// 以及 <c>create()</c> 返回的 RuntimeAPI 上挂着的 <c>Module</c>（Emscripten Module）。
/// 它们都写在 <c>export-api.ts</c> 里、在 <c>dotnet.d.ts</c> 里有正式类型声明，
/// 与 Bench_HeapView 用的 <c>MemoryView._unsafe_create_view()</c>（带 _unsafe 前缀的内部方法）不是一回事。
/// </para>
/// <para>
/// 【本模块的引子只为对照而存在】
/// ⑥ 与 ⑦ 的签名里<b>没有引子参数</b> —— 这正是要证明的点：用公开 API 就够了，不必再从 MemoryView 借道。
/// 只有 ⑤ 需要引子，为的是把三条路的 buffer 摆在一起做同一性比较。
/// </para>
/// </summary>
public static partial class JSBind_RuntimeApi
{
    /// <summary>
    /// ①-a 官方全局入口 <c>globalThis.getDotnetRuntime(runtimeId)</c>。
    /// 源码 exports.ts:71-77 写它就是为了"能在页面全局找到 dotnet runtime"；
    /// Bench_MemoryView 的 ⑨ 之所以"一无所获"，是候选名单里没扫它。
    /// </summary>
    [JSImport("probeGlobalRuntime", "bench_runtimeapi")]
    public static partial string ProbeGlobalRuntime();

    /// <summary>①-b main.js 把 <c>create()</c> 的返回值注入进来 —— 不依赖任何全局符号，最稳的一条路。</summary>
    [JSImport("probeInjectedApi", "bench_runtimeapi")]
    public static partial string ProbeInjectedApi();

    /// <summary>
    /// ①-c 对照：老办法扫全局。JS 侧会分别报"⑨ 当初那 5 个候选"与"补上 getDotnetRuntime 后"，
    /// 让"⑨ 的结论错在候选名单不全"在同一行里自证。
    /// </summary>
    [JSImport("probeLegacyScan", "bench_runtimeapi")]
    public static partial string ProbeLegacyScan();

    /// <summary>
    /// ② <c>localHeapViewU8()</c> 本身：返回什么、byteOffset 多少、buffer 多大、
    /// 以及 buffer 是不是 SharedArrayBuffer —— 后者直接决定"堆增长会不会 detach"（SAB 不可 detach）。
    /// </summary>
    [JSImport("probeHeapViewU8", "bench_runtimeapi")]
    public static partial string ProbeHeapViewU8();

    /// <summary>③ 全套 <c>localHeapViewXxx()</c>：U8/U16/U32、I8/I16/I32/I64Big、F32/F64 各返回什么。</summary>
    [JSImport("probeAllHeapViews", "bench_runtimeapi")]
    public static partial string ProbeAllHeapViews();

    /// <summary>
    /// ④ <c>api.Module</c>：上面有没有 <c>HEAPU8</c>、有没有 <c>wasmMemory</c>。
    /// 后者是 WebAssembly.Memory 本身 —— 有了它，"堆涨没涨"就是读出来的，不用再靠旧视图长度去猜。
    /// </summary>
    [JSImport("probeModule", "bench_runtimeapi")]
    public static partial string ProbeModule();

    /// <summary>
    /// ⑤ 三路 buffer 的同一性比较：公开 <c>localHeapViewU8().buffer</c>、
    /// <c>Module.wasmMemory.buffer</c>、引子 <c>_unsafe_create_view().buffer</c>。
    /// 按 marshal.ts:481-493 后者的内部就是前者，所以三者应当是同一个对象。
    /// </summary>
    [JSImport("probeBufferIdentity", "bench_runtimeapi")]
    public static partial string ProbeBufferIdentity([JSMarshalAs<JSType.MemoryView>] Span<byte> key);

    /// <summary>
    /// ⑥ 灵魂测试：用<b>公开 API</b> 的 buffer 配上 C# 钉住的数组地址建视图并写入。
    /// 签名里没有引子 —— C# 回读若看到变化，就证明公开 API 足以替代 _unsafe_create_view 那条绕道。
    /// </summary>
    [JSImport("writeViaPublicApi", "bench_runtimeapi")]
    public static partial string WriteViaPublicApi(nint ptr, int length, int value);

    /// <summary>⑦-a <c>setHeapU8(offset, value)</c>：按地址写一个字节，连视图都不用建。</summary>
    [JSImport("setU8At", "bench_runtimeapi")]
    public static partial string SetU8At(nint ptr, int value);

    /// <summary>⑦-b <c>getHeapU8(offset)</c>：按地址读回一个字节。</summary>
    [JSImport("getU8At", "bench_runtimeapi")]
    public static partial string GetU8At(nint ptr);

    /// <summary>⑧-a 存下 wasmMemory 与一个视图，作为堆增长的观测基线。</summary>
    [JSImport("startGrowWatch", "bench_runtimeapi")]
    public static partial string StartGrowWatch(nint ptr, int length);

    /// <summary>⑧-b 只查询不写入：精确回答"堆涨了没、旧 buffer detach 了没"。</summary>
    [JSImport("checkGrow", "bench_runtimeapi")]
    public static partial string CheckGrow();

    /// <summary>⑧-c 真正往存的视图里写一次 —— 只有 C# 回读能定生死（detach 后写入既不抛错也不生效）。</summary>
    [JSImport("writeKeptView", "bench_runtimeapi")]
    public static partial string WriteKeptView(int value);

    /// <summary>⑧-d 收尾：放掉 JS 侧跨调用持有的引用。</summary>
    [JSImport("releaseGrowWatch", "bench_runtimeapi")]
    public static partial string ReleaseGrowWatch();
}
