using System;
using System.Runtime.InteropServices.JavaScript;

/// <summary>
/// 互操作测试所需的跨界入口，集中放在一处：
/// <list type="bullet">
///   <item><description><c>Cs*</c>：[JSExport]，给 JS 调用 —— 测 <b>JS → C#</b>。</description></item>
///   <item><description>其余：[JSImport]，C# 调用 main.js 里的 <c>bench.*</c> —— 测 <b>C# → JS</b>。</description></item>
/// </list>
/// 命名上用 Cs 前缀区分方向，是因为同一个类里 [JSExport] 与 [JSImport] 的方法不能同名。
/// </summary>
public static partial class BenchInterop
{
    // ================= JS → C#（[JSExport]，供 JS 调用）=================

    /// <summary>无参数，返回常量 int —— 纯跨界的成本下限。</summary>
    [JSExport]
    public static int CsTick() => 0;

    [JSExport]
    public static int CsEchoInt(int value) => value;

    [JSExport]
    public static string CsEchoString(string text) => text;

    /// <summary>原样返回传入的字节数组，用于测 JS→C# 的 byte[] 封送。</summary>
    [JSExport]
    public static byte[] CsEchoBytes(byte[] data) => data;

    // ================= C# → JS（[JSImport]，调用 main.js 的 bench.*）=================

    /// <summary>调用空的 JS 函数，测 C#→JS 的纯跨界开销。</summary>
    [JSImport("bench.noop", "main.js")]
    public static partial void Noop();

    [JSImport("bench.echoInt", "main.js")]
    public static partial int EchoInt(int value);

    [JSImport("bench.echoString", "main.js")]
    public static partial string EchoString(string text);

    /// <summary>
    /// C# 提供缓冲（MemoryView），JS 直接往里写 —— <b>零拷贝</b>路径。
    /// 数据不经过复制，JS 写的就是 C# 那段内存本身。
    /// </summary>
    [JSImport("bench.fillSpan", "main.js")]
    public static partial int FillSpan([JSMarshalAs<JSType.MemoryView>] Span<byte> buffer, int length);

    /// <summary>
    /// 由 JS 新建数组并返回，C# 收成 byte[] —— <b>拷贝</b>路径，
    /// 每次都要复制一份并产生一次分配。与 <see cref="FillSpan"/> 对照即可量化差距。
    /// </summary>
    [JSImport("bench.makeArray", "main.js")]
    public static partial byte[] MakeArray(int length);

    // ---- 让 JS 连打 C# 并回报耗时（毫秒），用于测 JS→C# ----

    [JSImport("bench.callTickN", "main.js")]
    public static partial double CallTickN(int times);

    [JSImport("bench.callIntN", "main.js")]
    public static partial double CallIntN(int times);

    [JSImport("bench.callStringN", "main.js")]
    public static partial double CallStringN(int times, int textLength);

    [JSImport("bench.callBytesN", "main.js")]
    public static partial double CallBytesN(int times, int byteLength);
}
