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

/// <summary>一次计时的结果。</summary>
/// <param name="Ms"><b>最快一轮</b>的耗时（毫秒）—— 抖动的干扰最小，故用它代表真实成本</param>
/// <param name="Iters">实际迭代次数</param>
/// <param name="WorstMs">最慢一轮的耗时（毫秒）；与 Ms 的差就是抖动</param>
/// <param name="GcCount">计时期间实际发生的 GC 次数（三代合计）；非 0 说明这一行被回收拖过</param>
public sealed record BenchTiming(double Ms, long Iters, double WorstMs = 0, int GcCount = 0)
{
    /// <summary>换算成每次操作的纳秒数。</summary>
    public double NsPerOp(long opsTotal) => opsTotal > 0 ? Ms * 1_000_000.0 / opsTotal : 0;

    /// <summary>抖动 = 最慢一轮 − 最快一轮（毫秒）。为 0 表示没做多轮，抖动无从得知。</summary>
    public double JitterMs => WorstMs > Ms ? WorstMs - Ms : 0;
}

/// <summary>结果表格中的一行。</summary>
/// <param name="Name">方式名</param>
/// <param name="Note">说明</param>
/// <param name="Timing">计时结果</param>
/// <param name="OpsPerCall">单次 action 内的操作数（用于把 ms 折算成 ns/操作）</param>
/// <param name="Blocked">非空表示这条路线<b>走不通</b>（原因写在这里），耗时三列显示为 — 且不参与基线</param>
public sealed record BenchRow(string Name, string Note, BenchTiming Timing, long OpsPerCall, string? Blocked = null);

public static class BenchKit
{
    /// <summary>
    /// byte[] 的分档长度：小 / 中 / 大三档，用来看开销是<b>固定成本主导</b>还是随体积<b>线性增长</b>
    /// —— 这直接决定"该不该为省一次跨界而引入拷贝"。
    /// </summary>
    public static readonly int[] PayloadSizes = [16, 256, 2048];

    /// <summary>string 测试用的固定长度（基础类型不与 byte[] 一样分档，避免表格过于臃肿）。</summary>
    public const int TextLength = 256;

    /// <summary>
    /// <b>标量组</b>（int / string）的固定次数 —— 同一组里每一行都跑这么多，含 C# 侧的基线与 JS 侧的连打。
    /// 次数集中定义在这里，是为了让两个方向（C#→JS / JS→C#）的同类测试跑同一个数，页面之间也能对读。
    /// </summary>
    public const int Times = 20000;

    /// <summary><b>字节块组</b>（byte[] / MemoryView）的固定次数；单次较贵，故比标量组少。</summary>
    public const int TimesBytes = 2000;

    /// <summary>固定次数计时的轮数（取最快的一轮）。与 main.js 里 timeIt 的 rounds 一致。</summary>
    public const int Rounds = 5;

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
    /// 基线（MemoryView）：C# 自己往数组里写 <paramref name="length"/> 个字节，零跨界。
    /// 与跨界的 FillSpan 写的是<b>同样多、同样内容</b>的字节，两者之差才是"过一次界"的代价。
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void LocalFillSpan(byte[] data, int length)
    {
        int n = Math.Min(length, data.Length);
        for (int i = 0; i < n; i++) data[i] = (byte)(i & 0xFF);
    }

    /// <summary>
    /// <b>固定次数</b>计时：预热 <paramref name="warmup"/> 次（不计入）后，跑 <paramref name="rounds"/> 轮、
    /// 每轮恰好 <paramref name="times"/> 次，取<b>最快的一轮</b>并记下最慢的一轮。
    /// <para>
    /// 全程<b>不自适应放大</b> —— 快的方法不会因此自动跑更多次。
    /// 本工程<b>所有模块</b>（跨界四组、帧循环、反射）都必须用它：同组里<b>每一行都跑同一个 times</b>，
    /// 各行的"耗时(ms)"才能直接横比；用自适应时，最快的一行会被放大到几十万次、窗口反而最大
    /// （基线因此被排到末尾，整张表的结论都是反的）。
    /// </para>
    /// <para>
    /// 取最快轮而不是平均值，是因为浏览器里的干扰（GC、主线程被调度走、首次绑定解析）只会让某一轮<b>变慢</b>，
    /// 不会让它变快 —— 最快轮才是"没被干扰"的那一轮。只跑一轮时这种干扰无处可查，
    /// 于是出现过"2048 B 比 16 B 快 3 倍、且比空调用还便宜"这种不合物理的数字。
    /// </para>
    /// <para>
    /// 每轮开始前还会<b>强制 GC.Collect()</b>（不计入计时），让各轮都从"刚收完"的堆出发 ——
    /// 否则 GC 掉进哪一轮纯属随机。窗口内仍然新发生的回收记在 <see cref="BenchTiming.GcCount"/>，一并显示。
    /// </para>
    /// </summary>
    public static BenchTiming MeasureFixed(Action action, long times, int warmup = 1000, int rounds = Rounds)
    {
        for (int i = 0; i < warmup; i++) action();      // 预热：绑定解析与首次跨界都不该计入

        int gcBefore = TotalGcCount();
        double best = double.MaxValue, worst = 0;

        for (int r = 0; r < rounds; r++)
        {
            // 每轮开始前强制回收：让每一轮都从"刚收完"的堆出发。
            // 不做这件事，GC 就可能正好掉进某一轮里，把那一轮拖慢 —— 它落在哪一轮纯属随机，
            // 于是同一份代码两次跑出来的数字能对不上好几倍（2048B 比 16B 还快就是这么来的）。
            // 强制回收本身不计入计时（在 sw.Start 之前），但它挡不住窗口内新发生的回收，
            // 所以还要把窗口内的次数记下来一起显示（GcCount 列）。
            GC.Collect();

            var sw = Stopwatch.StartNew();
            for (long i = 0; i < times; i++) action();
            sw.Stop();

            double ms = (sw.ElapsedTicks / (double)Stopwatch.Frequency) * 1000.0;
            if (ms < best) best = ms;
            if (ms > worst) worst = ms;
        }

        return new BenchTiming(best, times, worst, TotalGcCount() - gcBefore);
    }

    /// <summary>三代回收次数合计，用来判断计时窗口里有没有真的发生过回收。</summary>
    public static int TotalGcCount() => GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);

    /// <summary>
    /// 生成一张结果卡片。列：方式 / 说明 / <b>总次数</b> / 耗时(ms) / <b>ns/操作</b>。
    /// <para>
    /// <b>同一组内所有行跑相同的次数</b>（固定次数、不自适应 —— 见 <see cref="MeasureFixed"/>），
    /// 所以组内的"耗时(ms)"可以直接横比；<b>跨组</b>（标量组 20,000 次 vs 字节块组 2,000 次）次数不同，
    /// 跨组只能比 ns/操作。总次数这一列就是用来当场确认"这几行次数是否一致"的。
    /// </para>
    /// <para>
    /// 行按 <b>ns/操作 升序</b>排列。<b>走不通</b>的行（<see cref="BenchRow.Blocked"/> 非空）一律沉到表尾：
    /// 它们没有真实耗时，混进排序会抢到第一名。
    /// </para>
    /// </summary>
    public static string Section(string title, string subtitle, IReadOnlyList<BenchRow> rows,
                                 string? conclusion = null)
    {
        List<BenchRow> ordered = rows
            .OrderBy(r => r.Blocked != null)
            .ThenBy(r => r.Timing.NsPerOp(r.Timing.Iters * r.OpsPerCall))
            .ToList();

        var sb = new StringBuilder();
        sb.Append("<div class='card'><h3>").Append(Escape(title)).Append("</h3>");
        sb.Append("<p class='muted'>").Append(Escape(subtitle)).Append("</p>");
        sb.Append("<table><thead><tr>")
          .Append("<th>方式</th><th>说明</th><th>总次数</th><th>耗时(ms)</th><th>ns/操作</th><th>抖动(ms)</th><th>GC(次)</th>")
          .Append("</tr></thead><tbody>");

        foreach (BenchRow r in ordered)
        {
            // 走不通的一行：不给数字，只给原因 —— 它压根没跑起来，没有"快慢"可言
            if (r.Blocked != null)
            {
                sb.Append("<tr class='blocked'>")
                  .Append("<td>").Append(Escape(r.Name)).Append("</td>")
                  .Append("<td class='muted'>").Append(Escape(r.Note))
                  .Append(" — <b>不可用：</b>").Append(Escape(r.Blocked)).Append("</td>")
                  .Append("<td>—</td><td>—</td><td>—</td><td>—</td><td>—</td>")
                  .Append("</tr>");
                continue;
            }

            long ops = r.Timing.Iters * r.OpsPerCall;
            sb.Append("<tr>")
              .Append("<td>").Append(Escape(r.Name)).Append("</td>")
              .Append("<td class='muted'>").Append(Escape(r.Note)).Append("</td>")
              .Append("<td>").Append(Fmt(ops)).Append("</td>")
              .Append("<td>").Append(r.Timing.Ms.ToString("F2", CultureInfo.InvariantCulture)).Append("</td>")
              .Append("<td>").Append(r.Timing.NsPerOp(ops).ToString("F1", CultureInfo.InvariantCulture)).Append("</td>")
              .Append("<td>").Append(Jitter(r.Timing)).Append("</td>")
              .Append("<td").Append(r.Timing.GcCount > 0 ? " class='gc'" : "").Append(">")
              .Append(r.Timing.GcCount.ToString(CultureInfo.InvariantCulture)).Append("</td>")
              .Append("</tr>");
        }

        sb.Append("</tbody></table>");
        if (conclusion != null) sb.Append("<p class='muted'>").Append(Escape(conclusion)).Append("</p>");
        sb.Append("</div>");
        return sb.ToString();
    }

    /// <summary>
    /// 拆开 JS 回报的 <c>"最快ms|最慢ms|错误"</c>（main.js 里各 <c>call*N</c> 的统一格式）。
    /// <para>
    /// 之所以不返回结构化对象：本工程的源生成式 interop 不支持 <c>JSType.Object</c>，
    /// 多值只能拼成字符串过界（引擎里 CreatePipeline 等也是这么做的）。
    /// </para>
    /// </summary>
    public static void SplitTiming(string? raw, out double ms, out double worst, out string error)
    {
        ms = -1;
        worst = 0;

        if (string.IsNullOrEmpty(raw))
        {
            error = "JS 未回报（绑定未找到或调用未执行）";
            return;
        }

        string[] part = raw.Split('|');

        if (part.Length > 0 && double.TryParse(part[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double a)) ms = a;
        if (part.Length > 1 && double.TryParse(part[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double b)) worst = b;

        error = part.Length > 2 ? part[2] : "";
    }

    /// <summary>
    /// 逐档抽验：MemoryView 是否真把字节写进了托管数组（不计时）。
    /// <para>
    /// 用途：某一档若"快得不合物理"（比空调用还便宜），先确认它到底写没写 ——
    /// 早先 fillSpan 误用 <c>view[i] = ...</c>（视图没有索引器），就是"跑得飞快、其实一个字节都没写"。
    /// </para>
    /// </summary>
    public static string VerifyFillSpan(Func<byte[], int, int> fill)
    {
        var sb = new StringBuilder("写入抽验 ");

        foreach (int len in PayloadSizes)
        {
            var buf = new byte[len];
            int n = fill(buf, len);
            bool ok = n == len && buf[0] == 0 && buf[len - 1] == (byte)((len - 1) & 0xFF);

            sb.Append(len).Append("B ").Append(ok ? "✓" : "✘").Append("；");
        }

        return sb.ToString();
    }

    /// <summary>
    /// 跑一次 JS 侧计时的调用，把回报（<c>"最快ms|最慢ms|错误"</c>）转成一行：错误段非空则记为"不可用"。
    /// 三个方向的模块都用它，免得每处各写一遍解析、再各漏一种情况。
    /// <para>
    /// 收的是委托而不是现成字符串，是为了能在调用<b>前后</b>各取一次 GC 计数 ——
    /// JS 连打期间发生的回收也在这边，照样能记下来。
    /// </para>
    /// </summary>
    public static BenchRow FromJs(Func<string> call, string name, string note, long times)
    {
        int gcBefore = TotalGcCount();
        string raw = call();
        int gc = TotalGcCount() - gcBefore;

        SplitTiming(raw, out double ms, out double worst, out string err);

        return string.IsNullOrEmpty(err)
            ? new BenchRow(name, note, new BenchTiming(ms, times, worst, gc), 1)
            : new BenchRow(name, note, new BenchTiming(0, times), 1, ShortError(err));
    }

    /// <summary>
    /// 抖动列：最慢一轮与最快一轮的差；没有多轮数据（WorstMs = 0）时显示 —，
    /// 免得把"未知"显示成"0.00"让人以为测得很稳。
    /// </summary>
    public static string Jitter(BenchTiming t)
        => t.WorstMs > 0 ? t.JitterMs.ToString("F2", CultureInfo.InvariantCulture) : "—";

    /// <summary>把 JS 的报错压成一行：原文可能很长，表格里只留前 120 个字符就够定位。</summary>
    public static string ShortError(string? error)
    {
        string s = (error ?? "").Replace("\n", " ").Trim();
        return s.Length <= 120 ? s : s[..120] + "…";
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
