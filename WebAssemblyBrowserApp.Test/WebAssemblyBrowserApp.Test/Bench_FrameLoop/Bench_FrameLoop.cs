using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// 模块：帧循环里"输入事件怎么过界"的取舍 —— 对应引擎宿主那行
/// <c>host.Frame(timestamp, gameFrameData.takeFrameData())</c>。
/// <para>
/// 两条路线，均由 JS 侧整体计时（一次 fn 调用 = <b>一帧的工作量</b>）：
/// <list type="bullet">
///   <item><description><b>A 一趟推</b>：JS→C# 一次，把一帧的字节流随帧回调一起带进来。
///     （这个方向只能带 byte[] —— JS→C# 没有 MemoryView。）</description></item>
///   <item><description><b>B 两趟拉</b>：JS→C# 只推帧（不带数据），C# 再回头调 JS 取；
///     第二趟是 C#→JS，走 MemoryView 零拷贝（C# 把缓冲借给 JS 直写）。
///     第二趟发生在 C# 的导出方法<b>内部</b>，而 JS 侧照样只调一次函数，
///     所以计到的是"一帧"的完整成本，两条口径一致。</description></item>
/// </list>
/// 另有一行<b>参照</b>（JS→C# 一趟、不带载荷）= 一次跨界的单价，用来把"带数据"与"多一趟"分开看。
/// </para>
/// <para>
/// 测的是<b>过界成本</b>本身：两条路线都用预生成的数据（A 的字节流与 B 的写入源都是缓存好的），
/// 因此都不含"JS 侧聚合事件"的成本 —— 这部分在真实引擎里两条路线都得付，对称排除才比得干净。
/// </para>
/// <para>
/// 载荷按一帧输入事件的典型大小分四档，并包含 <b>0 B（这一帧没有输入）</b>：
/// 空帧时 A 与 B 的差就是<b>纯跨界次数</b>的差，能单独量出"多一趟要付多少"。
/// </para>
/// </summary>
public sealed class Bench_FrameLoop : IBenchModule
{
    public string Name => "帧循环：一趟推 vs 两趟拉（JS→C# 带 byte[] / C# 回头零拷贝取）";

    public string Summary =>
        "模拟每帧送输入事件：A 一趟推（JS→C# 顺带 byte[]）vs B 两趟拉（先推帧，C# 回头用 MemoryView 零拷贝取）。" +
        "按每帧 0 / 16 / 256 / 1024 字节分四档（0 B = 这一帧没有输入），逐档由数字判定哪种更省，并抽验数据确实过了界。";

    public string Page => "frameloop";

    /// <summary>
    /// 一帧输入事件的载荷分档。<b>0 是不可省的一档</b>：多数帧其实没有输入，
    /// 那时"分开取"白付的那一趟跨界就是纯亏损。
    /// </summary>
    private static readonly int[] FrameSizes = [0, 16, 256, 1024];

    /// <summary>帧数（一次 fn 调用 = 一帧）。与字节块组同量级，5 轮共 10,000 帧。</summary>
    private const int Frames = 2000;

    public Task<string> RunAsync()
    {
        // 预热：首次跨界含绑定解析；B 的第二趟还是嵌套跨界（JS→C#→JS），更要先跑通一次
        JSBind_FrameLoop.CallFramePushN(200, 16);
        JSBind_FrameLoop.CallFramePullSpanN(200, 16);

        var all = new List<BenchRow>();
        var sb = new StringBuilder();

        // 参照：一趟不带载荷的 JS→C# —— 一次跨界的单价
        BenchRow baseline = BenchKit.FromJs(() => JSBind_FrameLoop.CallFrameTickN(Frames),
            "参照：JS→C# 一趟（无载荷）", "一次跨界的单价 —— A/B 都是在它之上加东西", Frames);

        // 抽验：走真实过界路径（由 JS 调一次两条路线，再把 C# 侧的校验位读回来）。
        // 用开关式校验（平时不开），所以这几纳秒不会混进计时的数字里。
        int flags = JSBind_FrameLoop.VerifyFrameOnce(1024);
        bool pushOk = (flags & 1) != 0;
        bool pullOk = (flags & 2) != 0;

        // ns/帧 留档，供逐档判定
        var ns = new Dictionary<int, (double Push, double Pull)>();

        foreach (int len in FrameSizes)
        {
            BenchRow push = BenchKit.FromJs(() => JSBind_FrameLoop.CallFramePushN(Frames, len),
                "A 一趟推 " + len + " B", "JS→C# 一次，顺便把 " + len + " 字节带进来（该方向只能 byte[]）", Frames);

            BenchRow pull = BenchKit.FromJs(() => JSBind_FrameLoop.CallFramePullSpanN(Frames, len),
                "B 两趟拉·MemoryView " + len + " B", "JS→C# 推帧 + C#→JS 直写 C# 的缓冲（零拷贝）", Frames);

            ns[len] = (NsPer(push), NsPer(pull));

            // 卡里三行（含参照，便于就地对齐）；总表只收路线行 ——
            // 参照行若每档都收，总表就会出现四行一模一样的"参照"
            var rows = new List<BenchRow> { baseline, push, pull };
            all.Add(push);
            all.Add(pull);

            sb.Append(BenchKit.Section("每帧 " + len + " 字节 — 一趟推 vs 两趟拉",
                "一次 fn 调用 = 一帧的工作量；「ns/操作」这一列就是 ns/帧。" +
                "参照行是“一次跨界”的单价，A/B 与它的差分别是“带数据”与“多一趟”的代价",
                rows,
                Verdict(len, ns[len])));
        }

        all.Insert(0, baseline);

        sb.Append(BenchKit.Section("总表：全部档位（按 ns/帧 升序）",
            "跨档比较请看「ns/帧」；同一档内的比较看上面四张卡（参照行对齐才有意义）",
            all,
            Overall(ns) + " 数据抽验：一趟推 " + (pushOk ? "收到完整字节 ✓" : "异常 ✘") +
            "；两趟拉 " + (pullOk ? "写进复用缓冲 ✓" : "异常 ✘") + "。"));

        return Task.FromResult(sb.ToString());
    }

    private static double NsPer(BenchRow r) => r.Timing.NsPerOp(r.Timing.Iters * r.OpsPerCall);

    /// <summary>逐档判定：A（一趟推）与 B（两趟拉）谁更省、省多少。</summary>
    private static string Verdict(int len, (double Push, double Pull) t)
    {
        var inv = CultureInfo.InvariantCulture;

        if (t.Push <= t.Pull)
        {
            double save = t.Pull > 0 ? (t.Pull - t.Push) / t.Pull * 100.0 : 0;
            return "判定：每帧 " + len + " B 时 A 一趟推更省，比 B 两趟拉省 " + save.ToString("F0", inv) +
                   "% —— 多付一趟跨界的固定成本，没能被换来的好处抵掉。";
        }

        double loss = t.Push > 0 ? (t.Push - t.Pull) / t.Push * 100.0 : 0;
        return "判定：每帧 " + len + " B 时 B 两趟拉更省，比 A 一趟推省 " + loss.ToString("F0", inv) +
               "% —— 载荷大到让“少复制一次”的价值超过了“多过一次界”的代价。";
    }

    /// <summary>总判定：看"分开"有没有在任何一档翻盘；没有的话，合并就是无条件的赢。</summary>
    private static string Overall(Dictionary<int, (double Push, double Pull)> ns)
    {
        var sb = new StringBuilder("总判定：");
        bool allPush = true;

        foreach (int len in FrameSizes)
        {
            var t = ns[len];
            bool pushWins = t.Push <= t.Pull;
            if (!pushWins) allPush = false;

            sb.Append(len).Append("B ").Append(pushWins ? "合并✓" : "分开✓").Append("；");
        }

        sb.Append(allPush
            ? " 各档都是“一趟推”更省 —— 引擎里 host.Frame(ts, takeFrameData()) 这种合并写法是对的：" +
              "哪怕这一帧一个字节都没有（0 B 档），“分开”也要白付一趟跨界。"
            : " 大载荷档出现了“分开”反超 —— 只有当一帧的数据大到让复制成本盖过一次跨界时，" +
              "才值得把帧回调与取数据拆成两趟。");

        return sb.ToString();
    }
}
