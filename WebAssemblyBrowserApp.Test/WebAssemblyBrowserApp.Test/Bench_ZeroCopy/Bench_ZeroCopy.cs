using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// 模块：<b>零拷贝 vs 非零拷贝</b>，把"省掉那一次 memcpy 到底值多少"测成数字，
/// 并据此算出<b>多大的数据才值得用零拷贝</b>。
/// <para>
/// 场景取引擎里最典型的一个：C# 手里有一块 <c>byte[]</c>（纹理 / 帧数据 / 顶点），JS 要<b>读</b>它。
/// 把这份数据交到 JS 手上，有两条路：
/// <list type="bullet">
/// <item><b>零拷贝</b>：<see cref="GCHandle"/> 钉住数组，把（地址, 长度）交给 JS，
/// JS 用公开 API <c>runtime.localHeapViewU8().buffer</c> 建出指向同一块内存的视图 —— 零 memcpy。</item>
/// <item><b>非零拷贝</b>：C# 把 <c>Span&lt;byte&gt;</c> 传过去（运行时封送成 MemoryView），
/// JS 用 <c>copyTo</c> 或 <c>slice</c> 拷一份 —— 一次 memcpy（slice 还多一次分配）。</item>
/// </list>
/// </para>
/// <para>
/// 【为什么只测一个体积档】
/// 早先是 4KB→4MB 四档，结果小档完全没法看：每次调用都在 JS 侧 <c>new</c> 一个视图，
/// 几千次下来喂给 V8 的新生代回收，<b>抖动比要测的差异还大好几倍</b> ——
/// 4KB 档两行相差 200 ns，抖动却是 2500～4150 ns，那是根本分辨不出来。
/// 而"拷贝成本随体积线性增长"这件事属于 memcpy 的<b>物理性质</b>（它是 O(n) 的），
/// 本就不必靠多档去验证。所以只留一个大档：差异远超噪声、JS 垃圾也少，一次测干净。
/// 至于"多大的数据才值得"，改由<b>临界体积</b>直接回答，比罗列四档更清楚。
/// </para>
/// <para>
/// 【JS 侧只做 O(1) 的动作】
/// 若让 JS 拿到后把整块求和，那个 O(n) 的 JS 循环会把 memcpy 的 O(n) 成本完全淹没
/// （memcpy 高度优化，JS 逐字节循环比它慢得多），几条路线会测出几乎一样快 ——
/// 那是"看着跑通了、其实没测到点子上"。所以各路线在 JS 侧<b>只读首末各一个字节</b>。
/// </para>
/// </summary>
public sealed class Bench_ZeroCopy : IBenchModule
{
    public string Name => "零拷贝 vs 非零拷贝（性能对比）";

    public string Summary =>
        "C# 有一块 4MB 的 byte[] 交给 JS 读：零拷贝（pin + 公开 API 建视图，含缓存视图版）对比" +
        "非零拷贝（MemoryView 的 copyTo / slice），另配三条基线把「跨界」「memcpy」的价钱单独量出来。" +
        "据此把成本拆成固定部分（pin + 建视图）与随体积线性增长的部分（memcpy），" +
        "算出【多大的数据才值得用零拷贝】。";

    public string Page => "zerocopy";

    /// <summary>只测这一个体积档（4 MB）。理由见类注释：小档是噪声重灾区，而线性与否无需多档验证。</summary>
    private const int PayloadBytes = 4 * 1024 * 1024;

    /// <summary>
    /// 每条跑多少次。<b>取 300 而不是 30</b>，是为了让最快的那条（零拷贝约 3 μs/次）
    /// 每轮也能有 ~1 ms —— 浏览器的 <c>performance.now()</c> 精度约 100 μs，
    /// 每轮太短就会被量化掉，测出来的数不可信（30 次 × 3 μs 只有 100 μs，正好卡在精度上）。
    /// </summary>
    private const long Times = 300;

    /// <summary>预热次数（首次跨界与绑定解析不计入）。</summary>
    private const int Warmup = 20;

    // 结果写入静态字段，防止被 JIT 当作无用代码消除
    private static int _sink;

    public Task<string> RunAsync()
    {
        var data = new byte[PayloadBytes];
        Fill(data);

        // C# 侧自己算出的期望校验值 —— 各路线返回的必须和它一致，否则就是那条读错了数据
        int expect = Peek(data);

        GCHandle pin = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            nint ptr = pin.AddrOfPinnedObject();

            var rows = new List<BenchRow>
            {
                // ---- 参照系 ----
                Row("① 基线：C# 自己读", "不跨界、不拷贝 —— 参照系",
                    () => { _sink = Peek(data); }, expect, expect),

                Row("② 基线：空跨界调用", "什么都不做立刻返回 —— 过一次界的底噪，下面每条都含它",
                    () => { _sink = JSBind_ZeroCopy.PeekNoop(); },
                    SafePeek(JSBind_ZeroCopy.PeekNoop), 7),

                // ---- 零拷贝三档：视图建不建、pin 钉几次 ----
                Row("③ 零拷贝：每次建视图", "pin 复用，但每次调用都 new 一个 TypedArray",
                    () => { _sink = JSBind_ZeroCopy.PeekZeroCopy(ptr, PayloadBytes); },
                    SafePeek(() => JSBind_ZeroCopy.PeekZeroCopy(ptr, PayloadBytes)), expect),

                Row("④ 零拷贝：缓存视图", "pin 复用 + 指针不变就复用视图 —— 零拷贝且零分配",
                    () => { _sink = JSBind_ZeroCopy.PeekZeroCopyCached(ptr, PayloadBytes); },
                    SafePeek(() => JSBind_ZeroCopy.PeekZeroCopyCached(ptr, PayloadBytes)), expect),

                Row("⑤ 零拷贝：每次都 pin", "每次都要 Alloc/Free —— 逐帧传数据的真实成本",
                    () =>
                    {
                        GCHandle h = GCHandle.Alloc(data, GCHandleType.Pinned);
                        try { _sink = JSBind_ZeroCopy.PeekZeroCopy(h.AddrOfPinnedObject(), PayloadBytes); }
                        finally { h.Free(); }
                    },
                    SafePeek(() =>
                    {
                        GCHandle h = GCHandle.Alloc(data, GCHandleType.Pinned);
                        try { return JSBind_ZeroCopy.PeekZeroCopy(h.AddrOfPinnedObject(), PayloadBytes); }
                        finally { h.Free(); }
                    }), expect),

                // ---- 非零拷贝 ----
                Row("⑥ 非零拷贝：MemoryView.copyTo",
                    "拷进复用缓冲（一次分配到最大尺寸，之后只覆盖、按有效长度取用）—— 1 次 memcpy",
                    () => { _sink = JSBind_ZeroCopy.PeekCopyTo(data); },
                    SafePeek(() => JSBind_ZeroCopy.PeekCopyTo(data)), expect),

                Row("⑦ 非零拷贝：MemoryView.slice", "每次新分配一份副本 —— 1 次 memcpy + 1 次分配",
                    () => { _sink = JSBind_ZeroCopy.PeekSlice(data); },
                    SafePeek(() => JSBind_ZeroCopy.PeekSlice(data)), expect),

                // ---- 解释性基线 ----
                Row("⑧ 基线：JS 内部 memcpy", "dst.set(src)，不碰 WASM 内存 —— 拷贝本身的价钱",
                    () => { _sink = JSBind_ZeroCopy.PeekJsMemcpy(PayloadBytes); },
                    SafePeek(() => JSBind_ZeroCopy.PeekJsMemcpy(PayloadBytes)), expect),
            };

            var sb = new StringBuilder();
            sb.Append(IntroCard());
            sb.Append(BenchKit.Section(
                "体积 " + FmtBytes(PayloadBytes),
                "每条跑 " + BenchKit.Fmt(Times) + " 次 × " + BenchKit.Rounds + " 轮，取最快的一轮；" +
                "同次数、同体积，可直接横比 ns/操作。",
                rows, TierConclusion(rows)));
            sb.Append(BreakdownCard(rows));

            return Task.FromResult(sb.ToString());
        }
        finally
        {
            if (pin.IsAllocated) pin.Free();
        }
    }

    /// <summary>开头一张说明卡片：讲清测的是什么、以及两个测量上的讲究。</summary>
    private static string IntroCard()
    {
        var sb = new StringBuilder();
        sb.Append("<div class='card'><h3>零拷贝 vs 非零拷贝</h3>");
        sb.Append("<p class='muted'>C# 有一块 ").Append(FmtBytes(PayloadBytes))
          .Append(" 的 <code>byte[]</code> 交给 JS 读。<b>零拷贝</b>：GCHandle 钉住 + 公开 API ")
          .Append("<code>localHeapViewU8().buffer</code> 建视图，不搬数据；<b>非零拷贝</b>：")
          .Append("<code>Span&lt;byte&gt;</code> 封送成 MemoryView，再 <code>copyTo</code>")
          .Append("（拷进复用缓冲）或 <code>slice()</code>（每次新分配）。两条路的差别就是那一次 memcpy。</p>");

        sb.Append("<p class='muted'>两个测量上的讲究。其一，各路线在 JS 侧<b>只读首末各一个字节</b>（O(1)）：")
          .Append("若改成整块求和，那个 O(n) 的 JS 循环会把 memcpy 的 O(n) 成本完全淹没，")
          .Append("几条路线会测出几乎一样快 —— 那是看着跑通了、其实没测到点子上。")
          .Append("其二，<b>只测一个体积档</b>：小档每次调用都在 JS 侧 new 一个视图，")
          .Append("几千次下来喂给 V8 的新生代回收，抖动比要测的差异还大好几倍，根本分辨不出来；")
          .Append("而拷贝成本随体积线性增长是 memcpy 的物理性质，无需多档验证。</p>");

        sb.Append("<p class='muted'>每行都会<b>对账校验值</b>（首字节 | 末字节&lt;&lt;8），")
          .Append("读到的数据不对会在说明里标出来 —— 否则会出现“跑得飞快、其实读的是别的内存”。</p>");
        sb.Append("</div>");
        return sb.ToString();
    }

    /// <summary>
    /// 把两种做法<b>各自多付了多少</b>摆成对照：copyTo 多付 memcpy，零拷贝多付建视图与 pin。
    /// <para>
    /// 刻意做成<b>两边对照</b>而不是只罗列"零拷贝的固定成本" —— 后者读起来像在说零拷贝有代价，
    /// 可它那点成本跟 memcpy 比根本不是一个量级，摆在一起才能一眼看出这是一边倒。
    /// </para>
    /// <para>
    /// 末尾仍给出临界体积（外推的两线交点），但只作附注：那个门槛极小，
    /// 引擎里没有哪块值得传的数据会落在它之下。
    /// </para>
    /// </summary>
    private static string BreakdownCard(List<BenchRow> rows)
    {
        var sb = new StringBuilder();
        sb.Append("<div class='card'><h3>两边各多付了多少</h3>");

        double cached = NsOf(rows, "④");   // 零拷贝且零分配
        double copy = NsOf(rows, "⑥");     // 非零拷贝（纯 memcpy + 复用缓冲）
        double view = NsOf(rows, "③");     // 每次建视图
        double eachPin = NsOf(rows, "⑤");  // 每次都 pin

        if (cached < 0 || copy < 0)
        {
            sb.Append("<p class='muted'>零拷贝或 copyTo 有一条没跑通，无法分解 —— 见表中红行的原因。</p></div>");
            return sb.ToString();
        }

        double copyCost = copy - cached;                       // 那一次 memcpy 的价钱
        double jitter = Math.Max(JitterNsOf(rows, "④"), JitterNsOf(rows, "⑥"));
        bool reliable = copyCost > jitter * 2;                 // 差异至少是抖动的 2 倍才敢下结论

        double nsPerByte = copyCost / PayloadBytes;

        // 两种做法各有各的增量成本，必须【两边对照】着摆。
        // 早先只罗列"零拷贝的固定成本"，读起来像在说零拷贝有代价 —— 那是误导：
        // 它那点成本跟 memcpy 比根本不是一个量级，摆在一起一眼就能看出来。
        double viewCost = view >= 0 ? view - cached : 0;

        // pin/unpin 常常小到测不出（甚至因噪声算出负值）。这时不要显示负数，
        // 那既难看也是假信号 —— 直接写"测不出来"，并按 0 计入。
        double jitterPin = Math.Max(JitterNsOf(rows, "⑤"), JitterNsOf(rows, "③"));
        double pinRaw = (eachPin >= 0 && view >= 0) ? eachPin - view : 0;
        bool pinMeasurable = pinRaw > jitterPin * 2;
        double pinCost = pinMeasurable ? pinRaw : 0;

        double copyExtra = copyCost;                        // copyTo 比零拷贝多付的
        double zeroExtra = Math.Max(viewCost, 0) + pinCost; // 零拷贝比 copyTo 多付的
        double net = copyExtra - zeroExtra;                 // 改用零拷贝净省下的

        sb.Append("<table><thead><tr><th>项</th><th>怎么算出来的</th><th>ns/次</th></tr></thead><tbody>");
        sb.Append(Row2("<b>copyTo 比零拷贝多付</b>", "⑥ copyTo − ④ 零拷贝缓存视图", Ns(copyExtra)));
        sb.Append(Row2("零拷贝比 copyTo 多付（建视图）", "③ 每次建视图 − ④ 缓存视图",
            viewCost > 0 ? Ns(viewCost) : "≈ 0（低于噪声）"));
        sb.Append(Row2("零拷贝比 copyTo 多付（pin / unpin）", "⑤ 每次都 pin − ③ 每次建视图",
            pinMeasurable ? Ns(pinCost) : "≈ 0（低于噪声，测不出来）"));
        sb.Append(Row2("<b>净收益：改用零拷贝省下</b>", "两边相抵", Ns(net)));
        sb.Append("</tbody></table>");

        sb.Append("<p class='muted'><b>每字节的拷贝成本：</b>").Append(nsPerByte.ToString("F4"))
          .Append(" ns/字节（≈ ").Append(Bandwidth(nsPerByte)).Append("）—— 拿它可外推到任意体积。</p>");

        if (!reliable)
        {
            sb.Append("<p class='muted'><b>⚠ 拷贝成本 ").Append(Ns(copyCost)).Append(" ns 与抖动 ")
              .Append(Ns(jitter)).Append(" ns 处在同一个量级</b> —— 这个差值撑不起下面的判定，" +
                  "以下仅供参考。</p></div>");
            return sb.ToString();
        }

        // 主判定：把两边各多付多少直接摆在一起比，别再让人去"权衡"
        sb.Append("<p class='muted'><b>这不是权衡，是一边倒。</b>copyTo 比零拷贝多付 ")
          .Append(Ns(copyExtra)).Append(" ns，零拷贝只比 copyTo 多付 ").Append(Ns(zeroExtra))
          .Append(" ns");
        if (zeroExtra > 0)
            sb.Append(" —— 差 <b>").Append((copyExtra / zeroExtra).ToString("F0")).Append(" 倍</b>");
        sb.Append("，净省 ").Append(Ns(net)).Append(" ns（约 ")
          .Append((net / copy * 100).ToString("F1")).Append("%）。零拷贝的增量成本只占 copyTo 的 ")
          .Append((zeroExtra / copy * 100).ToString("F1"))
          .Append("%，两者<b>根本不在同一个量级</b>，没有任何权衡余地 —— 该用零拷贝就用。</p>");

        double critical = nsPerByte > 0 ? zeroExtra / nsPerByte : 0;

        sb.Append("<p class='muted'>至于理论上的翻转点：把 memcpy 按每字节 ")
          .Append(nsPerByte.ToString("F4")).Append(" ns 线性外推（memcpy 是 O(n) 的，属物理性质，" +
              "不必靠多档验证），与零拷贝那笔不随体积变的 ").Append(Ns(zeroExtra))
          .Append(" ns 相交，得<b>临界体积 ≈ ").Append(FmtBytes((int)critical)).Append("</b>。" +
              "这个数字的意义在于它有多<b>小</b> —— 引擎里任何一块值得传的数据（纹理、帧缓冲、顶点）" +
              "都以 KB～MB 计，全部远超它，所以实务上<b>不存在需要权衡的场景</b>。</p>");

        sb.Append("<p class='muted'><b>但“快”和“能用”是两回事</b> —— 上面算的全是快慢，" +
                  "零拷贝在工程上还有三条硬约束，违反它们不出性能问题、出<b>正确性</b>问题：" +
                  "① 视图<b>用完即弃</b>，别存字段、别跨 await（官方注释原话：Don't store the reference, " +
                  "don't use it after await）；② 堆一旦增长（memory.grow），旧 buffer 被 detach，" +
                  "视图随之<b>静默失效</b> —— 读写既不抛错也不生效；③ pin 会阻碍 GC 压缩，" +
                  "别长时间钉住大数组。三条都不推翻上面的性能结论，只决定该怎么用它。</p>");

        sb.Append("</div>");
        return sb.ToString();
    }

    /// <summary>主表下方的简短结论。</summary>
    private static string? TierConclusion(List<BenchRow> rows)
    {
        double cached = NsOf(rows, "④");
        double copy = NsOf(rows, "⑥");
        double slice = NsOf(rows, "⑦");
        double view = NsOf(rows, "③");

        if (cached < 0 || copy < 0)
            return "零拷贝或 copyTo 有一条走不通，见表中红行。";

        var sb = new StringBuilder();

        // 先把结论说死：这不是"各有千秋"，是压倒性
        double pct = (copy - cached) / copy * 100.0;
        sb.Append("<b>零拷贝压倒性胜出：</b>").Append(Ns(cached)).Append(" ns/次 vs copyTo ")
          .Append(Ns(copy)).Append(" ns/次，差 ").Append((copy / cached).ToString("F1"))
          .Append(" 倍，省下 ").Append(pct.ToString("F1")).Append("%。");

        // 扣掉两者都含的跨界底噪再看 —— 这个净差才是"省掉 memcpy"的真实收益
        double noop = NsOf(rows, "②");
        if (noop >= 0 && cached > noop)
        {
            double netZero = cached - noop;
            double netCopy = copy - noop;
            sb.Append(" 扣掉每条都有的跨界底噪（").Append(Ns(noop)).Append(" ns）后更夸张：")
              .Append("零拷贝只多花 ").Append(Ns(netZero)).Append(" ns，copyTo 要多花 ")
              .Append(Ns(netCopy)).Append(" ns —— <b>净差 ")
              .Append((netCopy / netZero).ToString("F0")).Append(" 倍</b>。")
              .Append("换句话说，跨界调用本身（").Append(Ns(noop))
              .Append(" ns）已经是零拷贝的几乎全部成本，memcpy 那 ").Append(Ns(copy - cached))
              .Append(" ns 是纯亏。");
        }

        if (view >= 0)
            sb.Append(" 零拷贝若每次重建视图是 ").Append(Ns(view)).Append(" ns/次，比缓存视图多 ")
              .Append(Ns(view - cached)).Append(" ns —— 那是每个 TypedArray 对象的分配代价，")
              .Append("比起 memcpy 便宜三个数量级。");

        if (slice >= 0)
            sb.Append(" <b>slice() 是灾难：</b>").Append(Ns(slice)).Append(" ns/次（")
              .Append(Ms(slice)).Append(" ms），是 copyTo 的 ").Append((slice / copy).ToString("F1"))
              .Append(" 倍 —— 每次新分配 ").Append(FmtBytes(PayloadBytes))
              .Append(" 的代价。逐帧用（一帧 16.7 ms 预算）根本吃不消，大数据量下应视为禁用。");

        return sb.ToString();
    }

    /// <summary>跑一次并把计时结果收成一行；抛错则收成"走不通"的红行。</summary>
    private static BenchRow Row(string name, string note, Action action, int? check, int expect)
    {
        BenchTiming timing;
        try
        {
            timing = BenchKit.MeasureFixed(action, Times, Warmup);
        }
        catch (Exception e)
        {
            // 走不通的一行不给数字 —— 它压根没跑起来，没有"快慢"可言
            return new BenchRow(name, note, new BenchTiming(0, 0), 1, ShortError(e));
        }

        // 对账：这条路线读到的是不是这块数据
        string full = note;
        if (check.HasValue)
        {
            full += check.Value == expect
                ? "（校验 ✓）"
                : "（校验 ✘：读到 " + check.Value + "，期望 " + expect + "）";
        }

        return new BenchRow(name, full, timing, 1);
    }

    // ---------------------------------------------------------------- 小工具

    /// <summary>取某一行的 ns/操作；该行走不通则返回 -1。</summary>
    private static double NsOf(List<BenchRow> rows, string prefix)
    {
        var r = rows.FirstOrDefault(x => x.Name.StartsWith(prefix, StringComparison.Ordinal));
        return r == null || r.Blocked != null || r.Timing.Iters <= 0 ? -1 : r.Timing.NsPerOp(r.Timing.Iters);
    }

    /// <summary>某一行的抖动，折算成 ns/操作 —— 判断"差异是不是在噪声里"就靠它。</summary>
    private static double JitterNsOf(List<BenchRow> rows, string prefix)
    {
        var r = rows.FirstOrDefault(x => x.Name.StartsWith(prefix, StringComparison.Ordinal));
        return r == null || r.Blocked != null || r.Timing.Iters <= 0
            ? 0
            : r.Timing.JitterMs * 1_000_000.0 / r.Timing.Iters;
    }

    private static string Row2(string name, string how, string value)
        => "<tr><td>" + name + "</td><td class='muted'>" + how + "</td><td>" + value + "</td></tr>";

    private static string Ns(double v) => v.ToString("F1", CultureInfo.InvariantCulture);

    /// <summary>ns → ms 文本，用来判断"逐帧（16.7 ms 预算）还吃得下吗"。</summary>
    private static string Ms(double ns) => (ns / 1_000_000.0).ToString("F2", CultureInfo.InvariantCulture);

    /// <summary>由 ns/字节 反推带宽（GB/s），用来判断这个数字符不符合物理。</summary>
    private static string Bandwidth(double nsPerByte)
        => nsPerByte > 0 ? (1.0 / nsPerByte).ToString("F1") + " GB/s" : "—";

    private static string FmtBytes(int n)
        => n >= 1024 * 1024 ? (n / 1024 / 1024) + " MB"
            : n >= 1024 ? (n / 1024) + " KB"
                : n + " B";

    /// <summary>校验用的取值：首字节 | (末字节&lt;&lt;8)。JS 侧用的是同一个算法。</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Peek(byte[] data) => data[0] | (data[^1] << 8);

    private static void Fill(byte[] data)
    {
        for (int i = 0; i < data.Length; i++) data[i] = (byte)(i & 0xFF);
    }

    private static int? SafePeek(Func<int> f)
    {
        try { return f(); } catch { return null; }
    }

    private static string ShortError(Exception e)
    {
        string s = e.Message.Replace("\n", " ").Trim();
        return s.Length > 90 ? s[..90] + "…" : s;
    }
}
