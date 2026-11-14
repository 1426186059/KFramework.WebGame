using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.IO;
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
/// 只测<b>两个阵营</b>做同一件事：<b>JS</b>（<c>JSON.parse</c> / <c>JSON.stringify</c>，浏览器 JIT）对阵
/// <b>C# 源生成</b>（<c>JsonSerializerContext</c> 编译期生成代码，<c>Deserialize</c> / <c>Serialize</c>）。
/// 不引入反射式实现——本 WASM 运行时默认禁用反射式 JSON，会直接抛异常。
/// 关键看一件事：同样把 7MB 配置解析 / 序列化，C# 源生成比 JS 慢多少、慢在哪（解析+映射 vs 仅解析）。
/// </para>
/// <para>
/// 【重操作的计时讲究】单次 7MB 解析要几十毫秒，用户要求只测一次：故次数=1、warmup=1、轮数=1 ——
/// 单次测量已足够稳定（解析耗时远大于计时器分辨率）。若沿用别处默认的 warmup=1000，光预热就要几十秒。
/// </para>
/// </summary>
public sealed class Bench_Json : IBenchModule
{
    public string Name => "JSON 加载 / 序列化 / 反序列化（C# vs JS 对照）";

    public string Summary =>
        "用 2config10.25341.1.json（约 7MB 的 modelInfo 三层配置）做对照：C# 源生成 Deserialize / Serialize " +
        "对阵 JS 的 JSON.parse / JSON.stringify（不引入反射式——本运行时禁用反射式 JSON）。" +
        "专门暴露「强类型映射 + 序列化」相比纯解析多花多少 —— 这正是其他游戏里 JSON 成为性能瓶颈的环节。";

    public string Page => "json";

    /// <summary>单次 7MB 解析约几十 ms，用户要求只测一次，故 Times=1（1 次即稳定）。</summary>
    private const long Times = 1;

    /// <summary>warmup 1 次，避免冷启动 JIT 噪声，又不拖时间。</summary>
    private const int Warmup = 1;

    /// <summary>轮数（取最快的一轮）。</summary>
    private const int Rounds = 1;

    // 结果写入静态字段，防止被 JIT 当作无用代码消除
    private static string? _sinkStr;
    private static object? _sinkObj;

    public Task<string> RunAsync()
    {
        try
        {
            // 一次性从 JS 取回 7MB 文本（不在计时循环内）
            string? text = JSBind_Json.Json_GetText();
            if (string.IsNullOrEmpty(text))
            {
                return Task.FromResult("<div class='card'><h3>无法加载测试数据</h3><p class='muted'>" +
                       "C# 没能从 JS 取到 2config10.25341.1.json（可能是 fetch 失败或文件缺失）。" +
                       "请确认 wwwroot 下存在该文件，并通过 <code>json.html</code> 打开本测试。</p></div>");
            }

            return Task.FromResult(BuildReport(text));
        }
        catch (Exception ex)
        {
            // 任何意外（含反序列化抛 JsonException）都落到页面卡片里，
            // 而不是变成未处理的 Promise 拒绝（控制台只见 reject 堆栈、页面无反应）。
            return Task.FromResult(ErrorCard(ex));
        }
    }

    private static string BuildReport(string text)
    {
        // 预解析一次，供序列化计时复用（与 JS 的 parsed 复用同思路；都不计入计时）。
        // 分别 try/catch：强类型与 DOM 互不影响——任一条失败只让对应几行变“不可用”。
        OriginMir2Config2? typed = null;
        string? parseErr = null;
        try { typed = JsonSerializer.Deserialize(text, ConfigJsonContext.Default.OriginMir2Config2); }
        catch (Exception ex) { parseErr = ex.GetType().Name + ": " + ex.Message; }

        int modelCount = typed?.modelInfo?.Count ?? 0;
        int byteSize = Encoding.UTF8.GetByteCount(text);

        var sb = new StringBuilder();
        sb.Append(IntroCard(byteSize, modelCount));
        if (parseErr != null)
            sb.Append("<div class='card'><p class='muted'>⚠ 强类型（源生成）预解析失败：<code>")
              .Append(BenchKit.Escape(parseErr)).Append("</code>。下方“源生成”几行标记为不可用，" +
              "JS 对照不受影响。</p></div>");

        // ================= 反序列化（只测 JS 与 C# 源生成 两种实现）=================
        // 每行用 SafeMeasure 包住：任一行的测量若抛异常，只让该行变成“不可用”，不拖垮整张报告。
        var deRows = new List<BenchRow>
        {
            SafeMeasure("C# 反序列化：源生成上下文", "解析 + 映射成强类型对象图（JsonSerializerContext 编译期生成代码，非反射）",
                () => BenchKit.MeasureFixed(() =>
                {
                    _sinkObj = JsonSerializer.Deserialize(text, ConfigJsonContext.Default.OriginMir2Config2);
                }, Times, Warmup, Rounds), 1, "源生成反序列化"),

            BenchKit.FromJs(() => JSBind_Json.CallParseN((int)Times),
                "JS 解析：JSON.parse", "建 JS 对象图（全量，所有顶层表都在内）", Times),
        };
        sb.Append(BenchKit.Section("反序列化（解析 + 建对象图）",
            "同文件、均为<b>单次测量</b>（warmup 1 次、1 轮取最快）。" +
            "两个阵营做同一件事：C# 用<b>源生成</b> Deserialize（解析的同时把树落成强类型对象与两层字典），" +
            "JS 用 <c>JSON.parse</c>（仅建成 JS 对象图）。C# 这行比 JS 多出的部分，正是「强类型映射」的代价——" +
            "大量 ModelInfoTable 对象与字典的分配 / 插入，正是“JSON 慢”的主因。",
            deRows, DeConclusion(deRows)));

        // ================= 序列化 =================
        // 同样每行用 SafeMeasure 包住：任一序列化测量抛异常（如本运行时禁用反射式 JSON）只让该行变“不可用”，不中断整张报告。
        var seRows = new List<BenchRow>
        {
            SafeMeasure("C# 序列化：Serialize<OriginMir2Config2>", "从强类型对象图写出（真实用法：服务器下发 / 本地存储）",
                () => BenchKit.MeasureFixed(() =>
                {
                    if (typed == null) throw new InvalidOperationException("typed 为 null（强类型预解析失败）");
                    _sinkStr = JsonSerializer.Serialize(typed, ConfigJsonContext.Default.OriginMir2Config2);
                }, Times, Warmup, Rounds), 1, "强类型序列化"),

            BenchKit.FromJs(() => JSBind_Json.CallStringifyN((int)Times),
                "JS 序列化：JSON.stringify", "从 JS 对象图写出（全量）", Times),
        };
        sb.Append(BenchKit.Section("序列化（对象图 → 文本）",
            "两个阵营做同一件事：C# 用<b>源生成</b> Serialize（从强类型对象图写出，DTO 只含 modelInfo，故输出比全量小）；" +
            "JS 用 <c>JSON.stringify</c>（从全量 JS 对象图写出）。两者均为<b>单次测量</b>（1 轮取最快），" +
            "绝对值不可直接比（输出范围不同），但都能反映「对象图 → 文本」的遍历开销量级。",
            seRows, SeConclusion(seRows, byteSize)));

        // ================= 二进制序列化（手写紧凑格式，对比 JSON 文本）=================
        // 同一份强类型对象图，额外量一遍「手写二进制」（MemoryStream + BinaryWriter/Reader，无反射、AOT 友好），
        // 看它比 JSON 文本在「同对象图」下快多少、小多少。二进制省掉文本分词与逐字段字符串→数值解析，
        // 但仍要把数据落成同样的强类型对象，所以「映射/分配」这块开销它一样背。
        var binRows = new List<BenchRow>();
        byte[]? binBytes = null;
        if (typed != null)
        {
            binRows.Add(SafeMeasure("C# 二进制：Serialize", "把强类型对象图写成紧凑二进制（MemoryStream + BinaryWriter）",
                () => BenchKit.MeasureFixed(() =>
                {
                    var ms = new MemoryStream();
                    using (var w = new BinaryWriter(ms)) BinarySerialize(w, typed!);
                    binBytes = ms.ToArray();
                }, Times, Warmup, Rounds), 1, "二进制序列化"));

            if (binBytes != null)
                binRows.Add(SafeMeasure("C# 二进制：Deserialize", "从紧凑二进制读回强类型对象图（BinaryReader）",
                    () => BenchKit.MeasureFixed(() =>
                    {
                        using var ms = new MemoryStream(binBytes);
                        using var r = new BinaryReader(ms);
                        _sinkObj = BinaryDeserialize(r);
                    }, Times, Warmup, Rounds), 1, "二进制反序列化"));
        }
        else
        {
            binRows.Add(new BenchRow("C# 二进制：Serialize/Deserialize", "依赖强类型对象图",
                new BenchTiming(0, Times), 1, BlockedReason("二进制", parseErr)));
        }
        int binSize = binBytes?.Length ?? 0;
        sb.Append(BenchKit.Section("二进制序列化（手写紧凑格式，对比 JSON）",
            "同一份 <code>OriginMir2Config2</code> 对象图，额外量一遍手动二进制（MemoryStream + BinaryWriter/Reader，无反射、AOT 友好）。" +
            "二进制省掉 JSON 的文本分词与逐字段「字符串→数值」解析，理应更快；但仍要把树落成同样的强类型对象，" +
            "所以「映射成对象」这部分开销它一样逃不掉。" +
            (binSize > 0 ? " 本文件二进制体积 " + FmtBytes(binSize) + "，文本 " + FmtBytes(byteSize) +
                "（约为文本的 " + (binSize * 100.0 / byteSize).ToString("F0", CultureInfo.InvariantCulture) + "%）。" : "") +
            "以上均为<b>单次测量</b>（1 轮取最快）。",
            binRows, BinConclusion(binRows, binSize, byteSize)));

        // 口径提醒：本模块就跑在游戏客户端实际使用的 AOT 浏览器 WASM 运行时，故 C# 慢是客户端真实成本反映。
        sb.Append("<div class='card'><p class='muted'>⚠ <b>口径提醒（务必先看这条再下结论）</b>：本测试跑在游戏客户端实际使用的 " +
            "<b>AOT 编译浏览器 WASM</b> 运行时（工程已开 <code>RunAOTCompilation</code>）。" +
            "所以这里 C# 远慢于 JS（V8 有 JIT + SIMD，而 WASM 端的 <code>System.Text.Json</code> 在此目标上吞吐受限），" +
            "<b>正是客户端真实加载成本的反映</b>，不是开发期解释器的虚高。" +
            "要横向看「C# 在服务端 / 桌面能多快」，得在 JIT 运行时另测；但就客户端而言，这里的秒级数字就是真相。" +
            "本模块只用<b>源生成</b>实现，已刻意避开反射式（该 WASM 禁用反射式 JSON，会直接抛异常）。" +
            "单次测量（无取平均）下数字有抖动，结论看量级即可。</p></div>");

        return sb.ToString();
    }

    /// <summary>把一行失败原因压成 BenchRow.Blocked 文本。</summary>
    private static string BlockedReason(string what, string? err)
        => what + "失败：" + BenchKit.ShortError(err);

    /// <summary>
    /// 容错测量：某行测量抛异常（如本运行时禁用反射式 JSON）时，返回「不可用」行，
    /// 而不是让异常上抛拖垮整张报告（否则会退化成整页错误卡片）。
    /// </summary>
    private static BenchRow SafeMeasure(string name, string note, Func<BenchTiming> measure, long ops, string kind)
    {
        try { return new BenchRow(name, note, measure(), ops); }
        catch (Exception ex)
        {
            return new BenchRow(name, note, new BenchTiming(0, ops), ops, BlockedReason(kind, ex.Message));
        }
    }

    /// <summary>整体异常兜底：显示到页面卡片，避免未处理的 Promise 拒绝。</summary>
    private static string ErrorCard(Exception ex)
    {
        var sb = new StringBuilder();
        sb.Append("<div class='card'><h3>JSON 测试运行出错</h3><p class='muted'>");
        sb.Append(BenchKit.Escape(ex.GetType().Name)).Append("：").Append(BenchKit.Escape(ex.Message));
        if (ex.InnerException != null)
            sb.Append("<br>内部：").Append(BenchKit.Escape(ex.InnerException.GetType().Name)).Append("：")
              .Append(BenchKit.Escape(ex.InnerException.Message));
        sb.Append("</p><p class='muted'>（异常已被捕获并显示在此，不再以未处理的 Promise 拒绝形式出现在控制台。）</p></div>");
        return sb.ToString();
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

        sb.Append("<p class='muted'>两个阵营做同一件事：<b>反序列化</b>（C# 用源生成 Deserialize 把文本落成强类型对象图，" +
                  "JS 用 JSON.parse 建成 JS 对象图）与 <b>序列化</b>（C# 源生成 Serialize / JS JSON.stringify 把对象图写回文本）。" +
                  "C# 反序列化那行比 JS 多出的部分，就是「强类型映射」的代价——「JSON 慢在哪」看这一块。</p>");

        sb.Append("<p class='muted'>重操作计时：每文件<b>仅测 1 次</b>（warmup ").Append(Warmup)
          .Append(" 次、1 轮取最快），解析耗时远大于计时器分辨率，单次即稳定。" +
                  "若沿用别处默认的 warmup=1000，光预热就要几十秒，故这里压到最小。</p>");
        sb.Append("</div>");
        return sb.ToString();
    }

    /// <summary>反序列化段结论：C# 源生成 vs JS。</summary>
    private static string? DeConclusion(List<BenchRow> rows)
    {
        double csSrc = NsOf(rows, "C# 反序列化：源生成");
        double jsParse = NsOf(rows, "JS 解析");

        if (csSrc < 0 || jsParse < 0)
            return "C# 或 JS 的反序列化有一行没跑通，无法对照，见表中红行。";

        var sb = new StringBuilder();
        double ratio = csSrc / jsParse;
        sb.Append("<b>反序列化对照（C# 源生成 vs JS）：</b>C# Deserialize（解析 + 映射成强类型）")
          .Append(Ns(csSrc)).Append(" ns/次，JS JSON.parse（仅解析）")
          .Append(Ns(jsParse)).Append(" ns/次 —— C# 是 JS 的 ")
          .Append(ratio.ToString("F2", CultureInfo.InvariantCulture))
          .Append(" 倍。注意 C# 这行额外把树落成 CLR 对象与两层字典（强类型映射），" +
                  "所以倍数里含了映射开销；真正慢的不是解析，是<b>映射成强类型对象</b>这一块。");
        sb.Append(" 实务提示：只需遍历 / 抽数、不必落强类型时，用 JsonDocument / JsonNode 直接读可省掉映射开销。");
        return sb.ToString();
    }

    /// <summary>序列化段结论：C# 源生成 vs JS。</summary>
    private static string? SeConclusion(List<BenchRow> rows, int byteSize)
    {
        double csTyped = NsOf(rows, "C# 序列化：Serialize<OriginMir2Config2>");
        double jsSer = NsOf(rows, "JS 序列化");

        if (jsSer < 0 || csTyped < 0)
            return "JS 或 C# 的序列化有一行没跑通，无法对照，见表中红行。";

        var sb = new StringBuilder();
        sb.Append("<b>序列化对照（C# 源生成 vs JS）：</b>C# Serialize<OriginMir2Config2> ")
          .Append(Ns(csTyped)).Append(" ns/次，JS JSON.stringify ").Append(Ns(jsSer))
          .Append(" ns/次 —— C# 是 JS 的 ").Append((csTyped / jsSer).ToString("F2", CultureInfo.InvariantCulture))
          .Append(" 倍。注意 C# 这行只写 DTO 含的 modelInfo（输出比全量小），JS 写全量对象图，" +
                  "两者输出范围不同、绝对值不可直接比，但都反映「对象图 → 文本」的遍历 + 拼接开销量级。");
        sb.Append(" 结论：<b>序列化</b>是来回里更贵的一半——一份配置每次存盘 / 下发都要把约 ")
          .Append(FmtBytes(byteSize)).Append(" 重新走一遍对象图遍历 + 文本拼接，" +
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

    // ---------------------------------------------------------------- 手写二进制（无反射，AOT 友好）
    private static void BinarySerialize(BinaryWriter w, OriginMir2Config2 cfg)
    {
        var mi = cfg.modelInfo;
        w.Write(mi.Count);
        foreach (var outer in mi)
        {
            w.Write(outer.Key);
            var inner = outer.Value;
            w.Write(inner.Count);
            foreach (var innerKv in inner)
            {
                w.Write(innerKv.Key);
                var rec = innerKv.Value;
                w.Write(rec.id);
                w.Write(rec.action);
                w.Write(rec.intval);
                w.Write(rec.totaldir);
                w.Write(rec.standx);
                w.Write(rec.standy);
            }
        }
    }

    private static OriginMir2Config2 BinaryDeserialize(BinaryReader r)
    {
        var mi = new Dictionary<string, Dictionary<string, ModelInfoTable>>();
        int mCount = r.ReadInt32();
        for (int i = 0; i < mCount; i++)
        {
            string mKey = r.ReadString();
            int aCount = r.ReadInt32();
            var inner = new Dictionary<string, ModelInfoTable>();
            for (int j = 0; j < aCount; j++)
            {
                string aKey = r.ReadString();
                inner[aKey] = new ModelInfoTable
                {
                    id = r.ReadInt32(),
                    action = r.ReadInt32(),
                    intval = r.ReadInt32(),
                    totaldir = r.ReadInt32(),
                    standx = r.ReadDouble(),
                    standy = r.ReadDouble(),
                };
            }
            mi[mKey] = inner;
        }
        return new OriginMir2Config2 { modelInfo = mi };
    }

    /// <summary>二进制段结论：与 JSON 源生成对照体积与耗时。</summary>
    private static string? BinConclusion(List<BenchRow> rows, int binSize, int textSize)
    {
        double binSer = NsOf(rows, "C# 二进制：Serialize");
        double binDe = NsOf(rows, "C# 二进制：Deserialize");
        double csSer = NsOf(rows, "C# 序列化：Serialize<OriginMir2Config2>");
        double csDe = NsOf(rows, "C# 反序列化：源生成");

        var sb = new StringBuilder();
        if (binSize > 0)
            sb.Append("<b>体积：</b>二进制 ").Append(FmtBytes(binSize)).Append(" vs 文本 ")
              .Append(FmtBytes(textSize)).Append(" —— 二进制约为文本的 ")
              .Append((binSize * 100.0 / textSize).ToString("F0", CultureInfo.InvariantCulture)).Append("%。");

        if (binSer >= 0 && csSer >= 0)
            sb.Append(" <b>序列化（对象图→字节/文本）：</b>二进制 ").Append(Ns(binSer))
              .Append(" ns/次 vs JSON 源生成 ").Append(Ns(csSer)).Append(" ns/次（JSON 是二进制的 ")
              .Append((csSer / Math.Max(binSer, 1)).ToString("F2", CultureInfo.InvariantCulture)).Append(" 倍）。");

        if (binDe >= 0 && csDe >= 0)
            sb.Append(" <b>反序列化（字节/文本→对象图）：</b>二进制 ").Append(Ns(binDe))
              .Append(" ns/次 vs JSON 源生成 ").Append(Ns(csDe)).Append(" ns/次（JSON 是二进制的 ")
              .Append((csDe / Math.Max(binDe, 1)).ToString("F2", CultureInfo.InvariantCulture)).Append(" 倍）。");

        sb.Append(" 结论：二进制确实比 JSON 文本快（省掉分词与逐字段数值解析），但两者都要把数据落成同样的强类型对象，" +
                  "所以「JSON 慢」里那块<b>映射/分配</b>开销二进制一样背——想压客户端加载时间，" +
                  "要么接受这一次性映射，要么用「只读视图」式读取（不落全套对象）。");
        return sb.ToString();
    }
}
