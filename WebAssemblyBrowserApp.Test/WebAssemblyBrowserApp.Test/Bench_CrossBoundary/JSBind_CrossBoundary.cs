using System;
using System.Runtime.InteropServices.JavaScript;

/// <summary>
/// <b>Bench_CrossBoundary 模块专属</b>的互操作绑定（两个方向都要），对应 wwwroot/bench_crossboundary.js。
/// <para>
/// 本模块要的是"同一操作三种走法"，两个方向都得有，所以这里<b>自带一套完整的四类入口</b> ——
/// 不去借用 CsToJs / JsToCs 那两个模块的函数：借用会让本模块的数字里混入别人那边的写法细节。
/// </para>
/// <para>
/// [JSExport] 一律带 <c>Cb_</c> 前缀、[JSImport] 一律带 <c>cb</c> 前缀：
/// 导出树是平铺的、模块名也是共享的命名空间，不加前缀迟早撞车。
/// </para>
/// </summary>
public static partial class JSBind_CrossBoundary
{
    // ================= [JSExport]：JS → C# 四类（供 bench_crossboundary.js 连打）=================

    /// <summary>无参数返回常量 int —— 一次跨界的单价（参照行）。</summary>
    [JSExport]
    public static int Cb_Tick() => 0;

    [JSExport]
    public static int Cb_Int(int value) => value;

    [JSExport]
    public static string Cb_String(string text) => text;

    [JSExport]
    public static byte[] Cb_Bytes(byte[] data) => data;

    /// <summary>
    /// JS→C# 的 MemoryView（预期走不通，理由同 JsToCs 模块那一格）。
    /// 本模块照样摆出来：它要回答的正是"两个方向是否对称"。
    /// </summary>
    [JSExport]
    public static int Cb_FillSpan([JSMarshalAs<JSType.MemoryView>] Span<byte> buffer, int length)
    {
        int n = Math.Min(length, buffer.Length);
        for (int i = 0; i < n; i++) buffer[i] = (byte)(i & 0xFF);
        return n;
    }

    [JSExport]
    public static void Cb_GcCollect() => GC.Collect();

    // ================= [JSImport]：C# → JS 四类（本模块自己的一份）=================

    [JSImport("cbEchoInt", "bench_crossboundary")]
    public static partial int EchoInt(int value);

    [JSImport("cbEchoString", "bench_crossboundary")]
    public static partial string EchoString(string text);

    [JSImport("cbMakeArray", "bench_crossboundary")]
    public static partial byte[] MakeArray(int length);

    [JSImport("cbFillSpan", "bench_crossboundary")]
    public static partial int FillSpan([JSMarshalAs<JSType.MemoryView>] Span<byte> buffer, int length);

    // ================= [JSImport]：让 JS 连打 Cb_* 并回报 "最快|最慢|错误" =================

    [JSImport("cbCallTickN", "bench_crossboundary")]
    public static partial string CallTickN(int times);

    [JSImport("cbCallIntN", "bench_crossboundary")]
    public static partial string CallIntN(int times);

    [JSImport("cbCallStringN", "bench_crossboundary")]
    public static partial string CallStringN(int times, int textLength);

    [JSImport("cbCallBytesN", "bench_crossboundary")]
    public static partial string CallBytesN(int times, int byteLength);

    [JSImport("cbCallSpanN", "bench_crossboundary")]
    public static partial string CallSpanN(int times, int byteLength);
}
