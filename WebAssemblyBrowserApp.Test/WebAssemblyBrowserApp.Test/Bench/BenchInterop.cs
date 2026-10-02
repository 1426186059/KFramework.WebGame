using System;
using System.Runtime.InteropServices.JavaScript;

/// <summary>
/// 互操作测试所需的跨界入口，集中放在一处：
/// <list type="bullet">
///   <item><description><c>Cs*</c>：[JSExport]，给 JS 调用 —— 测 <b>JS → C#</b>。</description></item>
///   <item><description>其余：[JSImport]，C# 调用 main.js 里的 <c>bench.*</c> —— 测 <b>C# → JS</b>。</description></item>
/// </list>
/// 命名上用 Cs 前缀区分方向，是因为同一个类里 [JSExport] 与 [JSImport] 的方法不能同名。
/// <para>
/// 两个方向都按 <b>int / string / byte[] / MemoryView</b> 四类各备一份入口，好让"哪个类型在哪个方向
/// 走得通、各自多贵"能逐格对照（见 <see cref="Bench_CsToJs"/> 与 <see cref="Bench_JsToCs"/>）。
/// </para>
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

    /// <summary>
    /// JS→C# 方向的 MemoryView 尝试：参数声明为 <c>Span&lt;byte&gt;</c> + <c>JSMarshalAs&lt;MemoryView&gt;</c>。
    /// <para>
    /// 预期<b>走不通</b>：浏览器端 JS 只能给出 Uint8Array，而运行时要求的是它内部的 MemoryView 对象
    /// （断言 <c>Expected MemoryViewType.Byte</c>），浏览器也没有公开的 createMemoryView。
    /// 留着它正是为了让"JS→C# 有没有 MemoryView"有实测结论 —— 结论是<b>没有</b>，大块数据只能走 byte[]。
    /// </para>
    /// </summary>
    [JSExport]
    public static int CsFillSpan([JSMarshalAs<JSType.MemoryView>] Span<byte> buffer, int length)
    {
        int n = Math.Min(length, buffer.Length);
        for (int i = 0; i < n; i++) buffer[i] = (byte)(i & 0xFF);
        return n;
    }

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

    /// <summary>
    /// 反向的一条 byte[] 路线：C# 把数组传给 JS（封送时复制一次，JS 拿到的是副本），
    /// 与 <see cref="MakeArray"/> 的"JS 建数组交回来"正好相反，两档并列才看得出方向差异。
    /// </summary>
    [JSImport("bench.sendBytes", "main.js")]
    public static partial int SendBytes(byte[] data);

    // ---- 让 JS 连打 C# 并回报 "最快ms|最慢ms|错误"，用于测 JS→C# ----
    // 统一返回字符串：一次要带回三个值（耗时、抖动、是否可用），
    // 而源生成式 interop 不支持 JSType.Object，只能拼串过界。

    [JSImport("bench.callTickN", "main.js")]
    public static partial string CallTickN(int times);

    [JSImport("bench.callIntN", "main.js")]
    public static partial string CallIntN(int times);

    [JSImport("bench.callStringN", "main.js")]
    public static partial string CallStringN(int times, int textLength);

    [JSImport("bench.callBytesN", "main.js")]
    public static partial string CallBytesN(int times, int byteLength);

    /// <summary>
    /// 让 JS 连打 <see cref="CsFillSpan"/>；错误段为空表示这条路线走得通，否则带回被拒的原因。
    /// </summary>
    [JSImport("bench.callSpanN", "main.js")]
    public static partial string CallSpanN(int times, int byteLength);
}
