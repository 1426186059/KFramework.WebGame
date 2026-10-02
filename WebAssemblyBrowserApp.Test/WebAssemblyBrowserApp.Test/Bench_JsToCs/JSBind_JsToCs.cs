using System;
using System.Runtime.InteropServices.JavaScript;

/// <summary>
/// <b>Bench_JsToCs 模块专属</b>的互操作绑定（JS → C# 四类），对应 wwwroot/bench_jstocs.js。
/// <para>
/// 方法名一律带 <c>JsToCs_</c> 前缀：导出树里各模块的 [JSExport] 是平铺的，
/// 重名会让 main.js 的 findExport 取到别的模块那一份（表现就是"测的数字不是这个模块的"）。
/// </para>
/// </summary>
public static partial class JSBind_JsToCs
{
    // ================= [JSExport]：给 bench_jstocs.js 连打 =================

    /// <summary>无参数，返回常量 int —— 纯跨界的成本下限（参照行）。</summary>
    [JSExport]
    public static int JsToCs_Tick() => 0;

    [JSExport]
    public static int JsToCs_Int(int value) => value;

    [JSExport]
    public static string JsToCs_String(string text) => text;

    /// <summary>原样返回传入的字节数组，用于测 JS→C# 的 byte[] 封送（每次一份新数组）。</summary>
    [JSExport]
    public static byte[] JsToCs_Bytes(byte[] data) => data;

    /// <summary>
    /// JS→C# 方向的 MemoryView 尝试：参数声明为 <c>Span&lt;byte&gt;</c> + <c>JSMarshalAs&lt;MemoryView&gt;</c>。
    /// <para>
    /// 预期<b>走不通</b>：浏览器端 JS 只能给出 Uint8Array，而运行时要求的是它内部的 MemoryView 对象
    /// （断言 <c>Expected MemoryViewType.Byte</c>），浏览器也没有公开的 createMemoryView。
    /// 留着它正是为了让"JS→C# 有没有 MemoryView"有实测结论 —— 结论是<b>没有</b>，大块数据只能走 byte[]。
    /// </para>
    /// </summary>
    [JSExport]
    public static int JsToCs_FillSpan([JSMarshalAs<JSType.MemoryView>] Span<byte> buffer, int length)
    {
        int n = Math.Min(length, buffer.Length);
        for (int i = 0; i < n; i++) buffer[i] = (byte)(i & 0xFF);
        return n;
    }

    /// <summary>
    /// 供 JS 在<b>每轮计时前</b>调用：强制回收。
    /// JS 侧连打的那几行自己触发不了 WASM 的 GC，只能跨界回来叫一声，
    /// 否则本方向与 C# 侧计时那几行的抗干扰规则就不一致了。
    /// </summary>
    [JSExport]
    public static void JsToCs_GcCollect() => GC.Collect();

    // ================= [JSImport]：让 JS 连打上面这些并回报结果 =================
    // 统一返回 "最快ms|最慢ms|错误"：一次要带回三个值，而源生成式 interop 不支持 JSType.Object。

    [JSImport("callTickN", "bench_jstocs")]
    public static partial string CallTickN(int times);

    [JSImport("callIntN", "bench_jstocs")]
    public static partial string CallIntN(int times);

    [JSImport("callStringN", "bench_jstocs")]
    public static partial string CallStringN(int times, int textLength);

    [JSImport("callBytesN", "bench_jstocs")]
    public static partial string CallBytesN(int times, int byteLength);

    /// <summary>连打 <see cref="JsToCs_FillSpan"/>；错误段非空即为"被拒绝"及原因。</summary>
    [JSImport("callSpanN", "bench_jstocs")]
    public static partial string CallSpanN(int times, int byteLength);
}
