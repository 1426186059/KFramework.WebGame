using System;
using System.Runtime.InteropServices.JavaScript;

/// <summary>
/// <b>Bench_MemoryViewAsync 模块专属</b>的互操作绑定，对应 wwwroot/bench_memoryviewasync.js。
/// <para>
/// 本工程 <c>[JSImport]</c> 源生成<b>不支持 <c>Task&lt;T&gt;</c> 返回值（SYSLIB1072）</b>，没法让 C# 直接
/// <c>await</c> 一个会返回 Promise 的 JS 调用。要验证「异步（await）期间 MemoryView 内存会不会变」，
/// 用<b>等价但可落地的同步手段</b>：C# 先把 MemoryView 存进 JS（模块级变量）→ 回到 C# 后<b>强制 GC + 分配压力</b>
/// （这正是 await 把控制权交还事件循环、GC 趁机搬迁托管数组的那段时间窗）→ 再调一次 JS 把存下的视图读回来。
/// 读回来的字节若和当初已知内容不一致，就说明「内存变了 / 视图已失效」。
/// </para>
/// </summary>
public static partial class JSBind_MemoryViewAsync
{
    /// <summary>存【unpinned Span】视图（JSMarshalAs 只在调用期间 pin，调用结束即释放）。</summary>
    [JSImport("probeStore", "bench_memoryviewasync")]
    public static partial string ProbeStore([JSMarshalAs<JSType.MemoryView>] Span<byte> buffer);

    /// <summary>存【pinned ArraySegment】视图（绑定会 pin 住托管数组，跨调用仍有效）。</summary>
    [JSImport("probeStorePinned", "bench_memoryviewasync")]
    public static partial string ProbeStorePinned([JSMarshalAs<JSType.MemoryView>] ArraySegment<byte> buffer);

    /// <summary>读回上次存的【unpinned】视图，回报当前字节（格式：<c>_stored|&lt;csv&gt;</c>）。</summary>
    [JSImport("probeReadStored", "bench_memoryviewasync")]
    public static partial string ProbeReadStored();

    /// <summary>读回上次存的【pinned】视图，回报当前字节（格式：<c>_storedPinned|&lt;csv&gt;</c>）。</summary>
    [JSImport("probeReadStoredPinned", "bench_memoryviewasync")]
    public static partial string ProbeReadStoredPinned();
}
