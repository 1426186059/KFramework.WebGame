using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.JavaScript;
using System.Text;

// 基准测试的公共基础设施：计时、结果表格、页面输出、JS 宿主绑定。
// 每个测试模块都是独立的 .cs 文件，只依赖这里，互不纠缠。

/// <summary>一次计时的结果（耗时毫秒 + 实际迭代次数）。</summary>
public sealed record BenchTiming(double Ms, long Iters)
{
    /// <summary>换算成每次操作的纳秒数。</summary>
    public double NsPerOp(long opsTotal) => opsTotal > 0 ? Ms * 1_000_000.0 / opsTotal : 0;
}

/// <summary>结果表格中的一行。</summary>
/// <param name="Name">方式名</param>
/// <param name="Note">说明</param>
/// <param name="Timing">计时结果</param>
/// <param name="OpsPerCall">单次 action 内的操作数（用于把 ms 折算成 ns/操作）</param>
public sealed record BenchRow(string Name, string Note, BenchTiming Timing, long OpsPerCall);

public static class BenchKit
{
    /// <summary>
    /// byte[] 的分档长度：小 / 中 / 大三档，用来看开销是<b>固定成本主导</b>还是随体积<b>线性增长</b>
    /// —— 这直接决定"该不该为省一次跨界而引入拷贝"。
    /// </summary>
    public static readonly int[] PayloadSizes = [16, 256, 2048];

    /// <summary>string 测试用的固定长度（基础类型不与 byte[] 一样分档，避免表格过于臃肿）。</summary>
    public const int TextLength = 256;

    /// <summary>生成指定长度的字符串。</summary>
    public static string MakeText(int length) => new('x', length);

    // ================================================================
    // 基线：C# 调用【自身】方法（零跨界）
    // 所有跨界测试的参照系 —— 有了它，"跨界比不跨界慢多少倍"才是可比的。
    // ================================================================

    // 结果写入静态字段，防止被 JIT 当作无用代码消除
    private static int _sinkInt;
    private static string? _sinkString;
    private static byte[]? _sinkBytes;

    /// <summary>
    /// 基线（int）：C# 内部调用，不跨任何边界。
    /// <para>
    /// <c>NoInlining</c> 是必须的 —— 否则会被内联掉，测出来接近 0，就失去了作为基线的意义。
    /// </para>
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void LocalEchoInt(int value) => _sinkInt = value;

    /// <summary>基线（string）：C# 内部调用。</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void LocalEchoString(string text) => _sinkString = text;

    /// <summary>
    /// 基线（byte[]）：C# 内部调用，<b>不复制</b>。
    /// 跨界那条路径的复制发生在封送过程中，所以这里保持原样 —— 两者的差正是"跨界封送的成本"。
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void LocalEchoBytes(byte[] data) => _sinkBytes = data;

    /// <summary>
    /// 自适应校准计时：先 warmup，再以 2 倍放大迭代次数，直到耗时超过 targetMs 或达到上限。
    /// <para>
    /// 之所以不固定迭代次数：不同场景的单次开销可能相差几个数量级，
    /// 固定次数要么快到测不准、要么慢到让人以为卡死。
    /// </para>
    /// </summary>
    public static BenchTiming Measure(Action action, long opsPerAction = 1, int warmup = 3,
                                      int targetMs = 60, int maxIters = 1 << 18)
    {
        for (int i = 0; i < warmup; i++) action();

        int iters = 1;
        long ticks = 0;
        while (true)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iters; i++) action();
            sw.Stop();
            ticks = sw.ElapsedTicks;
            if (iters >= maxIters || sw.ElapsedMilliseconds >= targetMs) break;
            iters *= 2;
        }

        double ms = (ticks / (double)Stopwatch.Frequency) * 1000.0;
        return new BenchTiming(ms, iters);
    }

    /// <summary>
    /// 生成一张结果卡片。
    /// <para>
    /// 行会按<b>总耗时升序</b>排列，最快的一行排到最前并作为基线（"相对 x"列以它为 1.00），
    /// 这样一眼就能看出谁快谁慢，不必自己在几行数字里找最小值。
    /// </para>
    /// </summary>
    public static string Section(string title, string subtitle, IReadOnlyList<BenchRow> rows,
                                 string? conclusion = null)
    {
        // 按总耗时排序：最快者作基线
        List<BenchRow> ordered = rows.OrderBy(r => r.Timing.Ms).ToList();

        var sb = new StringBuilder();
        sb.Append("<div class='card'><h3>").Append(Escape(title)).Append("</h3>");
        sb.Append("<p class='muted'>").Append(Escape(subtitle)).Append("</p>");
        sb.Append("<table><thead><tr><th>方式</th><th>说明</th><th>耗时(ms)</th><th>ns/操作</th><th>相对 x</th></tr></thead><tbody>");

        if (ordered.Count > 0)
        {
            BenchRow baseline = ordered[0];
            double baselineNs = baseline.Timing.NsPerOp(baseline.Timing.Iters * baseline.OpsPerCall);

            foreach (BenchRow r in ordered)
            {
                double ns = r.Timing.NsPerOp(r.Timing.Iters * r.OpsPerCall);
                double x = ns > 0 ? baselineNs / ns : 0;
                sb.Append("<tr>")
                  .Append("<td>").Append(Escape(r.Name)).Append("</td>")
                  .Append("<td class='muted'>").Append(Escape(r.Note)).Append("</td>")
                  .Append("<td>").Append(r.Timing.Ms.ToString("F2", CultureInfo.InvariantCulture)).Append("</td>")
                  .Append("<td>").Append(ns.ToString("F1", CultureInfo.InvariantCulture)).Append("</td>")
                  .Append("<td>").Append(x.ToString("F2", CultureInfo.InvariantCulture)).Append("</td>")
                  .Append("</tr>");
            }
        }

        sb.Append("</tbody></table>");
        if (conclusion != null) sb.Append("<p class='muted'>").Append(Escape(conclusion)).Append("</p>");
        sb.Append("</div>");
        return sb.ToString();
    }

    public static string Escape(string s) => s
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    public static string Fmt(long n) => n.ToString("N0", CultureInfo.InvariantCulture);

    public static void SetResults(string html) => BenchHost.SetInnerHTML("#results", html);

    public static void SetStatus(string text) => BenchHost.SetInnerText("#status", text);
}

// 与 wwwroot/main.js 里 setModuleImports('main.js', { dom: ... }) 对应
internal static partial class BenchHost
{
    [JSImport("dom.setInnerHTML", "main.js")]
    internal static partial void SetInnerHTML(string selector, string html);

    [JSImport("dom.setInnerText", "main.js")]
    internal static partial void SetInnerText(string selector, string text);
}
