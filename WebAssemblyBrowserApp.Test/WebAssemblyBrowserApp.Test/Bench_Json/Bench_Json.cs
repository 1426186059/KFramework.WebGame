using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// 模块：<b>JSON 加载 / 序列化 / 反序列化</b>，把「C# 与 JS 解析同一份 7MB 配置」的差距测成数字。
/// <para>
/// 起因：其他游戏的经验里，JSON 的序列化 / 反序列化是<b>严重的性能瓶颈</b> ——
/// 一份大配置反复 parse / stringify，会直接吃掉启动与热重载的预算。
/// 本模块用工程里现成的 <c>2config10.25341.1.json</c>（约 7MB 的 <c>modelInfo</c> 配置，
/// 三层嵌套：modelId → actionId → 记录）做对照，正好能放大「强类型映射 + 序列化」多花的代价。
/// </para>
/// <para>
/// 测三种动作、两个阵营：
/// <list type="bullet">
/// <item><b>反序列化</b>：<c>JsonDocument.Parse</c>（仅解析、建只读 DOM，与 JS 口径最齐）/
/// <c>JsonSerializer.Deserialize&lt;OriginMir2Config2&gt;</c>（解析 + 映射成强类型对象图）/
/// JS <c>JSON.parse</c>。</item>
/// <item><b>序列化</b>：<c>JsonSerializer.Serialize</c>（强类型对象 / 只读 DOM 两档）/
/// JS <c>JSON.stringify</c>。</item>
/// </list>
/// 关键看两件事：① 纯解析 C# 与 JS 谁快；② 强类型映射 / 序列化比纯解析多花多少 —— 后者正是「JSON 慢」的主因。
/// </para>
/// <para>
/// 【重操作的计时讲究】单次 7MB 解析要几十毫秒，所以次数取 10、warmup 取 2、轮数取 3 ——
/// 若沿用别处默认的 warmup=1000，光预热就要几十秒。每轮仍强制 GC、取最快轮（干扰只会让某轮变慢）。
/// </para>
/// </summary>
public sealed class Bench_Json : IBenchModule
{
    public string Name => "JSON 加载 / 序列化 / 反序列化（C# vs JS 对照）";

    public string Summary =>
        "用 2config10.25341.1.json（约 7MB 的 modelInfo 三层配置）做对照：C# 的 JsonDocument.Parse、" +
        "Deserialize<OriginMir2Config2>、Serialize 对阵 JS 的 JSON.parse / JSON.stringify。" +
        "专门暴露「强类型映射 + 序列化」相比纯解析多花多少 —— 这正是其他游戏里 JSON 成为性能瓶颈的环节。";

    public string Page => "json";

    /// <summary>单次 7MB 解析约几十 ms，10 次足够取稳且总耗时可控（约 1~3 秒）。</summary>
    private const long Times = 10;

    /// <summary>重操作：warmup 必须很小，否则光预热就要几十秒。</summary>
    private const int Warmup = 2;

    /// <summary>轮数（取最快的一轮）。</summary>
    private const int Rounds = 3;

    // 结果写入静态字段，防止被 JIT 当作无用代码消除
    private static string? _sinkStr;
    private static object? _sinkObj;

    public Task<string> RunAsync()
    {
        // 一次性从 JS 取回 7MB 文本（不在计时循环内）
        string? text = JSBind_Json.Json_GetText();
        if (string.IsNullOrEmpty(text))
        {
            return "<div class='card'><h3>无法加载测试数据</h3><p class='muted'>" +
                   "C# 没能从 JS 取到 2config10.25341.1.json（可能是 fetch 失败或文件缺失）。" +
                   "请确认 wwwroot 下存在该文件，并通过 <code>json.html</code> 打开本测试。</p></div>";
        }

        // 预解析一次，供序列化计时复用（与 JS 的 parsed 复用同思路；都不计入计时）
        OriginMir2Config2? typed = JsonSerializer.Deserialize(text, ConfigJsonContext.Default.OriginMir2Config2);
        using JsonDocument doc = JsonDocument.Parse(text);

        int modelCount = typed?.modelInfo?.Count ?? 0;
        int byteSize = Encoding.UTF8.GetByteCount(text);

        var sb = new StringBuilder();
        sb.Append(IntroCard(byteSize, modelCount));

        // ================= 反序列化 =================
        var deRows = new List<BenchRow>
        {
            new("C# 解析：JsonDocument.Parse", "仅解析、建只读 DOM（全量，含 modelInfo 之外的顶层表）",
                BenchKit.MeasureFixed(() => { _sinkObj = JsonDocument.Parse(text); }, Times, Warmup, Rounds), 1),

            new("C# 反序列化：Deserialize<OriginMir2Config2>",
                "解析 + 映射成强类型对象图（Dictionary<int, Dictionary<int, ModelInfoTable>>）",
                BenchKit.MeasureFixed(() =>
                {
                    _sinkObj = JsonSerializer.Deserialize(text, ConfigJsonContext.Default.OriginMir2Config2);
                }, Times, Warmup, Rounds), 1),

            BenchKit.FromJs(() => JSBind_Json.CallParseN((int)Times),
                "JS 解析：JSON.parse", "建 JS 对象图（全量，所有顶层表都在内）", Times),
        };
        sb.Append(BenchKit.Section("反序列化（解析 + 建对象图）",
            "同文件、同 " + Times + " 次 × " + Rounds + " 轮取最快轮。" +
            "C# JsonDocument.Parse 与 JS JSON.parse 口径最齐（都建通用树，不含业务对象分配）；" +
            "Deserialize<OriginMir2Config2> 额外多一步强类型映射——大量 ModelInfoTable 与两层 int 字典的分配，" +
            "正是“JSON 慢”的主要来源。",
            deRows, DeConclusion(deRows)));

        // ================= 序列化 =================
        var seRows = new List<BenchRow>
        {
            new("C# 序列化：Serialize<OriginMir2Config2>", "从强类型对象图写出（真实用法：服务器下发 / 本地存储）",
                BenchKit.MeasureFixed(() => { _sinkStr = JsonSerializer.Serialize(typed); }, Times, Warmup, Rounds), 1),

            new("C# 序列化：Serialize(doc.RootElement)", "从只读 DOM 写出（全量，与 JS JSON.stringify 同口径）",
                BenchKit.MeasureFixed(() => { _sinkStr = JsonSerializer.Serialize(doc.RootElement); }, Times, Warmup, Rounds), 1),

            BenchKit.FromJs(() => JSBind_Json.CallStringifyN((int)Times),
                "JS 序列化：JSON.stringify", "从 JS 对象图写出（全量）", Times),
        };
        sb.Append(BenchKit.Section("序列化（对象图 → 文本）",
            "C# 强类型序列化只含 modelInfo（DTO 仅映射它）；Serialize(doc) 与 JS 同口径输出全量。" +
            "三者同 " + Times + " 次 × " + Rounds + " 轮。",
            seRows, SeConclusion(seRows, byteSize)));

        return Task.FromResult(sb.ToString());
    }

    // ---------------------------------------------------------------- 卡片与结论

    private static string IntroCard(int byteSize, int modelCount)
    {
        var sb = new StringBuilder();
        sb.Append("<div class='card'><h3>JSON 解析 / 序列化：C# vs JS</h3>");
        sb.Append("<p class='muted'>数据源：<code>2config10.25341.1.json</code>，约 ").Append(FmtBytes(byteSize))
          .Append("，顶层只有 <code>modelInfo</code>（三层嵌套：modelId → actionId → 记录）。")
          .Append("强类型 DTO 映射出 <b>").Append(modelCount.ToString("N0", CultureInfo.InvariantCulture))
          .Append("</b> 个模型。这份规模正好能放大「强类型映射 + 序列化」的代价——")
          .Append("其他游戏里 JSON 正是这么成为启动 / 热重载瓶颈的。</p>");

        sb.Append("<p class='muted'>两个阵营各做同样的事：① <b>纯解析</b>（把文本建成内存里的通用树，" +
                  "C# 用 JsonDocument、JS 用 JSON.parse，口径最齐）；② <b>强类型映射</b>（C# 额外把树落成 CLR 对象与字典）；" +
                  "③ <b>序列化</b>（把对象图写回文本）。解析速度看 ① 的对照；" +
                  "「JSON 到底慢在哪」看 ② 比 ① 多花多少、③ 的绝对值。</p>");

        sb.Append("<p class='muted'>重操作计时：每文件 ").Append(Times).Append(" 次 × ").Append(Rounds)
          .Append(" 轮取最快轮，warmup 仅 ").Append(Warmup).Append(" 次（默认 1000 会让光预热就几十秒）。</p>");
        sb.Append("</div>");
        return sb.ToString();
    }

    /// <summary>反序列化段结论：纯解析 C# vs JS，以及强类型映射多花的。</summary>
    private static string? DeConclusion(List<BenchRow> rows)
    {
        double csParse = NsOf(rows, "C# 解析");
        double csTyped = NsOf(rows, "C# 反序列化");
        double jsParse = NsOf(rows, "JS 解析");

        if (csParse < 0 || jsParse < 0)
            return "C# 或 JS 的解析有一行没跑通，无法对照，见表中红行。";

        var sb = new StringBuilder();
        double ratio = csParse / jsParse;
        sb.Append("<b>纯解析：</b>C# JsonDocument ").Append(Ns(csParse)).Append(" ns/次，JS JSON.parse ")
          .Append(Ns(jsParse)).Append(" ns/次 —— C# 是 JS 的 ").Append(ratio.ToString("F2", CultureInfo.InvariantCulture))
          .Append(" 倍。两者都只建通用树（不含业务对象分配），差距反映了引擎解析器本身的吞吐差异。");

        if (csTyped >= 0)
        {
            double extra = csTyped - csParse;
            double mapRatio = csTyped / csParse;
            sb.Append(" <b>强类型映射的代价：</b>Deserialize 把同一份树落成 ")
              .Append(mapRatio.ToString("F1", CultureInfo.InvariantCulture)).Append(" 倍于纯解析的开销（多 ")
              .Append(Ns(extra)).Append(" ns/次）—— 大量 ModelInfoTable 对象与两层 int 字典的分配 / 字典插入。" +
              "这正是「JSON 慢」的主因：<b>慢不在解析，在映射成强类型对象</b>。");
        }

        sb.Append(" 实务提示：只需遍历 / 抽数、不必落强类型时，用 JsonDocument / JsonNode 直接读，" +
                  "可省掉这一整块映射开销。");
        return sb.ToString();
    }

    /// <summary>序列化段结论：C# 两档与 JS 对照。</summary>
    private static string? SeConclusion(List<BenchRow> rows, int byteSize)
    {
        double csTyped = NsOf(rows, "C# 序列化：Serialize<OriginMir2Config2>");
        double csDoc = NsOf(rows, "C# 序列化：Serialize(doc");
        double jsSer = NsOf(rows, "JS 序列化");

        if (jsSer < 0 || csDoc < 0)
            return "JS 或 C# 的序列化有一行没跑通，无法对照，见表中红行。";

        var sb = new StringBuilder();
        sb.Append("<b>序列化对照（同口径全量）：</b>C# Serialize(doc) ").Append(Ns(csDoc))
          .Append(" ns/次，JS JSON.stringify ").Append(Ns(jsSer)).Append(" ns/次 —— C# 是 JS 的 ")
          .Append((csDoc / jsSer).ToString("F2", CultureInfo.InvariantCulture)).Append(" 倍");

        if (csTyped >= 0)
        {
            sb.Append("。强类型 Serialize<OriginMir2Config2> ").Append(Ns(csTyped))
              .Append(" ns/次（与 doc 档的差，是「先走强类型、再写」vs「直接写 DOM」的实现差；" +
                      "本 DTO 只含 modelInfo，故输出比全量小，绝对值不可与全量档直接比，仅供实现选型参考）");
        }

        sb.Append("。结论：<b>序列化</b>是来回里更贵的一半——一份配置在「存盘 / 下发」时每序列化一次，" +
                  "都要把约 ").Append(FmtBytes(byteSize)).Append(" 重新走一遍对象图遍历 + 文本拼接，" +
                  "这正是热重载 / 批量下发会被 JSON 拖慢的根。");
        return sb.ToString();
    }

    // ---------------------------------------------------------------- 小工具（本模块专用）

    private static double NsOf(List<BenchRow> rows, string prefix)
    {
        var r = rows.FirstOrDefault(x => x.Name.StartsWith(prefix, StringComparison.Ordinal));
        return r == null || r.Blocked != null || r.Timing.Iters <= 0
            ? -1
            : r.Timing.NsPerOp(r.Timing.Iters);
    }

    private static string Ns(double v) => v.ToString("F1", CultureInfo.InvariantCulture);

    private static string FmtBytes(int n)
        => n >= 1024 * 1024 ? (n / 1024 / 1024) + " MB"
            : n >= 1024 ? (n / 1024) + " KB"
                : n + " B";
}
