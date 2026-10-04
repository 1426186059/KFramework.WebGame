using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// 模块：<b>每帧跨 JS 调用 N 次，要付多少钱</b>。
/// <para>
/// 起因是马里奥那个反直觉的结果：把 GPU 上传改成零拷贝（按 Bench_ZeroCopy 的实测能省 98.7%）
/// 之后帧数<b>几乎没变</b>。原因一算就明白 —— 马里奥每帧只传几十 KB 顶点（≈几 μs），
/// 而一次<b>空</b>跨界调用就要 3333 ns，每帧跨几十上百次界（≈上百 μs）。
/// 也就是说在这个场景里，<b>"跨了多少次界"远比"拷了多少字节"更决定帧率</b>。
/// </para>
/// <para>
/// 本模块把这件事量成一条曲线：N 从 0 到 1,000,000，测每帧耗时与占 60fps 预算的比例，
/// 并直接给出"跨界把预算吃光时 N 是多少"—— 那就是每帧跨界的天花板。
/// 页面带滑块，可随时改 N 重测，便于边拖边观察。
/// </para>
/// <para>
/// 调用体一律是<b>空的</b>（<c>noop</c>）：不带负载，测的纯粹是跨界这个动作的固定成本 × 次数。
/// 若要比方向与数据类型（每次带负载），那是 Bench_CrossBoundary 的活。
/// </para>
/// </summary>
public sealed class Bench_CrossCall : IBenchModule
{
    public string Name => "跨 JS 调用频率（每帧 N 次的代价）";

    public string Summary =>
        "只测【频率】：每帧连续做 N 次空跨界调用（0 → 1,000,000，页面滑块可调），" +
        "量出每帧耗时、每次跨界的固定成本、以及占 60fps 预算的比例 —— " +
        "回答「每帧跨多少次界会开始掉帧」。依据：一次空跨界 ≈ 拷贝 47 KB，" +
        "所以频率常常比数据量更决定帧率。";

    public string Page => "crosscall";

    /// <summary>N 的上限。再大就不是"每帧"能承受的量级了。</summary>
    private const int MaxN = 1_000_000;

    /// <summary>60fps 的帧预算（毫秒）。</summary>
    private const double FrameBudgetMs = 1000.0 / 60.0;

    /// <summary>全套预设档位：跨六个数量级，用来找"每帧跨界的天花板"落在哪。</summary>
    private static readonly int[] Presets = [0, 1, 10, 100, 1000, 5000, 10000, 50000, 100000, 1000000];

    /// <summary>自动进入页面时只跑这几个小档 —— 快，不至于一进页面就卡住。</summary>
    private static readonly int[] QuickSet = [0, 10, 100, 1000];

    public Task<string> RunAsync()
    {
        var sb = new StringBuilder();
        sb.Append(IntroCard());
        sb.Append(BuildTable("小档速览（0 → 1000）", QuickSet, withConclusion: false));
        return Task.FromResult(sb.ToString());
    }

    /// <summary>
    /// <c>[JSExport]</c>：供页面滑块 / 数值框调用，测指定的每帧次数。
    /// <para>先 <c>await Task.Delay(16)</c> 是为了让浏览器先把"运行中…"渲染出来 ——
    /// 大 N 的计算是同步的，不先让一帧就会整个页面卡住看不到提示。</para>
    /// </summary>
    [JSExport]
    public static async Task<string> CcRun(int count)
    {
        int n = Math.Clamp(count, 0, MaxN);
        await Task.Delay(16);
        return SingleCard(n);
    }

    /// <summary><c>[JSExport]</c>：供页面按钮调用，跑全套预设档位（大档较慢）。</summary>
    [JSExport]
    public static async Task<string> CcRunAll()
    {
        await Task.Delay(16);
        return BuildTable("全套预设档位（0 → 1,000,000）", Presets, withConclusion: true);
    }

    // ---------------------------------------------------------------- 测量

    /// <summary>
    /// 测"一帧做 N 次跨界"的耗时。
    /// <para>
    /// 这里 <c>times = 1</c>：<b>一轮就是一帧</b>，帧内做 N 次跨界。
    /// 轮数按 N 递减 —— N 越大单轮越贵，轮数过多会把页面拖死，
    /// 但轮数少了抖动就无从得知，故在表格里如实标出轮数。
    /// </para>
    /// </summary>
    private static BenchTiming MeasureFrame(int count)
    {
        if (count <= 0) return new BenchTiming(0, 0);

        return BenchKit.MeasureFixed(() =>
        {
            for (int i = 0; i < count; i++) JSBind_CrossCall.Noop();
        }, 1, WarmupFor(count), RoundsFor(count));
    }

    private static int RoundsFor(int count)
        => count <= 1000 ? BenchKit.Rounds   // 5 轮，取最快
            : count <= 100000 ? 3
                : 1;                          // 百万次只能跑一轮

    private static int WarmupFor(int count) => count <= 100000 ? 2 : 0;

    // ---------------------------------------------------------------- 渲染

    private static string IntroCard()
    {
        var sb = new StringBuilder();
        sb.Append("<div class='card'><h3>跨 JS 调用频率</h3>");
        sb.Append("<p class='muted'>每帧连续做 <b>N 次空跨界调用</b>（就是 <code>C# → JS → C#</code> " +
                  "什么都不做立刻返回），量 N 与每帧耗时的关系。调用体刻意不带负载，" +
                  "测的纯粹是<b>跨界这个动作的固定成本 × 次数</b>。</p>");
        sb.Append("<p class='muted'><b>为什么要测这个：</b>Bench_ZeroCopy 实测一次空跨界 ≈ ")
          .Append("3333").Append(" ns，而拷贝 4 MB 才 287 μs —— 换算下来，")
          .Append("<b>一次空跨界 ≈ 拷贝 47 KB</b>。引擎每帧传的数据常常只有几十 KB（拷贝成本几 μs），")
          .Append("却可能跨几十上百次界（上百 μs）。所以在这个量级上，")
          .Append("<b>“每帧跨了多少次界”比“每帧拷了多少字节”更决定帧率</b> —— ")
          .Append("这也是零拷贝在马里奥上看不出提升的原因。</p>");
        sb.Append("<p class='muted'>用上面的滑块或数值框改 N，点「测这个次数」即可重测；")
          .Append("点「跑全部预设档位」出 0 → 1,000,000 的完整曲线（大档较慢）。</p>");
        sb.Append("</div>");
        return sb.ToString();
    }

    /// <summary>单个 N 的结果卡片（滑块调用时返回这个）。</summary>
    private static string SingleCard(int n)
    {
        BenchTiming t = MeasureFrame(n);
        double perCall = n > 0 ? t.Ms * 1_000_000.0 / n : 0;
        double pct = t.Ms / FrameBudgetMs * 100.0;

        var sb = new StringBuilder();
        sb.Append("<div class='card'><h3>每帧 ").Append(BenchKit.Fmt(n)).Append(" 次跨 JS 调用</h3>");
        sb.Append("<table><thead><tr><th>项</th><th>值</th></tr></thead><tbody>");
        sb.Append(Row2("每帧跨界次数", BenchKit.Fmt(n) + " 次"));
        sb.Append(Row2("每帧耗时", FmtMs(t.Ms) + "（最快轮）"));
        sb.Append(Row2("每次跨界", n > 0 ? perCall.ToString("F1", CultureInfo.InvariantCulture) + " ns" : "—"));
        sb.Append(Row2("占 60fps 预算（16.67 ms）", pct.ToString("F2", CultureInfo.InvariantCulture) + " %"));
        sb.Append(Row2("只算跨界的帧率上限", FpsCeiling(t.Ms)));
        sb.Append(Row2("抖动", t.WorstMs > 0 ? FmtMs(t.JitterMs) : "—（只跑一轮）"));
        sb.Append(Row2("轮数", RoundsFor(n) + " 轮取最快，预热 " + WarmupFor(n) + " 次"));
        sb.Append("</tbody></table>");

        sb.Append("<p class='muted'>").Append(Verdict(n, t.Ms, pct)).Append("</p>");
        sb.Append("</div>");
        return sb.ToString();
    }

    /// <summary>多档对比表。</summary>
    private static string BuildTable(string title, int[] counts, bool withConclusion)
    {
        var sb = new StringBuilder();
        sb.Append("<div class='card'><h3>").Append(title).Append("</h3>");
        sb.Append("<table><thead><tr><th>每帧跨界次数</th><th>每帧耗时</th><th>每次跨界</th>" +
                  "<th>占 60fps 预算</th><th>只算跨界的帧率上限</th><th>抖动</th></tr></thead><tbody>");

        var rows = new List<(int N, double Ms, double PerCall)>();
        foreach (int n in counts)
        {
            BenchTiming t = MeasureFrame(n);
            double perCall = n > 0 ? t.Ms * 1_000_000.0 / n : 0;
            rows.Add((n, t.Ms, perCall));

            double pct = t.Ms / FrameBudgetMs * 100.0;
            sb.Append("<tr>")
              .Append("<td>").Append(BenchKit.Fmt(n)).Append("</td>")
              .Append("<td>").Append(FmtMs(t.Ms)).Append("</td>")
              .Append("<td>").Append(n > 0 ? perCall.ToString("F1", CultureInfo.InvariantCulture) + " ns" : "—").Append("</td>")
              .Append("<td").Append(pct > 100 ? " class='gc'" : "").Append(">")
              .Append(pct.ToString("F2", CultureInfo.InvariantCulture)).Append(" %</td>")
              .Append("<td>").Append(FpsCeiling(t.Ms)).Append("</td>")
              .Append("<td>").Append(t.WorstMs > 0 ? FmtMs(t.JitterMs) : "—").Append("</td>")
              .Append("</tr>");
        }

        sb.Append("</tbody></table>");

        if (withConclusion) sb.Append("<p class='muted'>").Append(TableConclusion(rows)).Append("</p>");
        else sb.Append("<p class='muted'>这是进入页面时自动跑的小档。")
                .Append("要看完整曲线（含 10 万 / 100 万）请点上方「跑全部预设档位」。</p>");

        sb.Append("</div>");
        return sb.ToString();
    }

    /// <summary>全套跑完后给出"天花板"：跨界把 16.67 ms 吃光时，N 是多少。</summary>
    private static string TableConclusion(List<(int N, double Ms, double PerCall)> rows)
    {
        var sb = new StringBuilder();

        // 找出最后一个"还在预算内"的档，与第一个"超出预算"的档，在两者间线性插值
        int lastOk = -1, firstOver = -1;
        foreach (var r in rows)
        {
            if (r.N <= 0) continue;
            if (r.Ms <= FrameBudgetMs) lastOk = r.N;
            else if (firstOver < 0) firstOver = r.N;
        }

        // 用有实测值的档算平均单次成本（取每次跨界最稳的中段档位）
        var mid = rows.FindAll(r => r.N >= 100 && r.N <= 100000 && r.PerCall > 0);
        double avg = mid.Count > 0 ? mid.SumOf(r => r.PerCall) / mid.Count : 0;

        sb.Append("<b>每次跨界的固定成本：</b>")
          .Append(avg > 0 ? avg.ToString("F1", CultureInfo.InvariantCulture) + " ns（取 100 ~ 100000 各档平均）" : "—")
          .Append("。按这个单价，60fps 的 16.67 ms 预算大约够每帧跨 ")
          .Append(avg > 0 ? BenchKit.Fmt((int)(FrameBudgetMs * 1_000_000.0 / avg)) + " 次" : "—")
          .Append(" —— 那就是每帧跨界的<b>天花板</b>。");

        if (firstOver > 0)
            sb.Append(" 实测里第一个把预算吃光的档位是 <b>").Append(BenchKit.Fmt(firstOver))
              .Append(" 次/帧</b>（超过它就必然掉到 60fps 以下，还没算渲染与逻辑本身的开销）。");
        else if (lastOk > 0)
            sb.Append(" 实测各档都在预算内（最大档 ").Append(BenchKit.Fmt(lastOk))
              .Append(" 次/帧也没吃光预算）。");

        sb.Append(" <b>怎么用这个数：</b>引擎每帧的跨界次数（顶点上传、状态设置、矩阵、输入 poll…）")
          .Append("加总后若接近天花板，减次数比优化单次负载更划算 —— ")
          .Append("典型手段是把多次调用合并成一次批处理，或像马里奥那样做跨帧短路去重。");

        return sb.ToString();
    }

    private static string Verdict(int n, double ms, double pct)
    {
        if (n <= 0) return "N = 0：一帧不做任何跨界，用来对照测量本身的开销。";

        if (pct < 1) return "只占预算的 " + pct.ToString("F2") + " % —— 这个频率下跨界成本可以忽略，瓶颈在别处。";
        if (pct < 10) return "占预算 " + pct.ToString("F1") + " % —— 有成本但不构成瓶颈。";
        if (pct <= 100) return "占预算 " + pct.ToString("F1") + " % —— 跨界已经成为主要开销之一，值得想办法减少次数。";
        return "占预算 " + pct.ToString("F0") + " % —— <b>光跨界就超出一整帧预算</b>，这个频率下不可能跑到 60fps。";
    }

    private static string Row2(string name, string value)
        => "<tr><td>" + name + "</td><td>" + value + "</td></tr>";

    private static string FmtMs(double ms)
    {
        if (ms >= 1000) return (ms / 1000.0).ToString("F2", CultureInfo.InvariantCulture) + " s";
        if (ms >= 1) return ms.ToString("F2", CultureInfo.InvariantCulture) + " ms";
        if (ms > 0) return (ms * 1000.0).ToString("F1", CultureInfo.InvariantCulture) + " μs";
        return "0";
    }

    private static string FpsCeiling(double ms)
        => ms > 0 ? (1000.0 / ms).ToString("F0", CultureInfo.InvariantCulture) + " fps" : "∞";
}

/// <summary>给 List 补一个求和（避免为此引入额外的 using）。</summary>
internal static class CrossCallLinq
{
    public static double SumOf<T>(this List<T> list, Func<T, double> selector)
    {
        double s = 0;
        foreach (T item in list) s += selector(item);
        return s;
    }
}
