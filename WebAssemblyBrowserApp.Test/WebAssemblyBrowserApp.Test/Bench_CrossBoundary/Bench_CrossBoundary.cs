using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// 模块：跨界方向横向对比。
/// <para>
/// 把<b>同一个回声操作</b>的三种走法摆在一起：
/// <list type="number">
///   <item><description><b>基线</b>：C# 调用自身方法（零跨界）—— 衡量"不跨界要花多少"</description></item>
///   <item><description><b>C# → JS</b>：<c>[JSImport]</c>，C# 主动调过去</description></item>
///   <item><description><b>JS → C#</b>：<c>[JSExport]</c>，JS 主动调进来</description></item>
/// </list>
/// 载荷按 <b>int / string / byte[] / MemoryView</b> 四类分组，byte[] 与 MemoryView 再按 16 / 256 / 2048 分档。
/// 每组内按 <b>ns/操作</b> 升序 —— 所以每个载荷下三行的先后顺序，直接回答"哪个方向更贵、贵多少倍"。
/// （注意<b>跨组</b>次数不同：标量 20,000 次、字节块 2,000 次，跨组只比 ns/操作。）
/// </para>
/// <para>
/// byte[] 组统一用<b>byte[] 封送</b>这条对称的走法（C#→JS 取 JS 返回的数组、JS→C# 传数组进 C#），
/// 两边都含一次复制，才算是"同样的方法"在比。零拷贝的 MemoryView 单独成组 —— 它只在 C#→JS 方向存在，
/// 摆出来正是为了让"两个方向并不对称"这件事看得见。
/// </para>
/// </summary>
public sealed class Bench_CrossBoundary : IBenchModule
{
    public string Name => "跨界方向对比（基线 / C#→JS / JS→C#，四类分组）";

    public string Summary =>
        "同一操作三种走法横向比：基线（C# 调自身）、C# 调 JS、JS 调 C#。按 int / string / byte[] / MemoryView 四类分组，" +
        "后两类按 16/256/2048 分档。每组按 ns/操作 升序，看三行的倍数即可判断方向差异；" +
        "MemoryView 组的 JS→C# 一行标明不可用 —— 那个方向没有这条路。";

    public string Page => "crossboundary";

    public Task<string> RunAsync()
    {
        // 预热（首次跨界含 JIT 与绑定解析）
        BenchInterop.CallIntN(2000);
        BenchInterop.CallStringN(500, 16);
        BenchInterop.CallBytesN(200, 16);
        BenchInterop.CallSpanN(1, 16);

        var all = new List<BenchRow>();
        var sb = new StringBuilder();

        // ================= ① int =================
        // 三行都是同一个 BenchKit.Times：基线（C# 侧计时）、C#→JS（C# 侧计时）、JS→C#（JS 侧计时，次数由这里传过去）
        var intRows = new List<BenchRow>
        {
            Row("int — 基线 C#→C#", "C# 调用自身方法，零跨界",
                BenchKit.MeasureFixed(() => BenchKit.LocalEchoInt(1), BenchKit.Times), 1),

            Row("int — C#→JS", "[JSImport] C# 主动调过去",
                BenchKit.MeasureFixed(() => _ = BenchInterop.EchoInt(1), BenchKit.Times), 1),

            BenchKit.FromJs(() => BenchInterop.CallIntN(BenchKit.Times),
                "int — JS→C#", "[JSExport] JS 主动调进来（由 JS 侧计时）", BenchKit.Times),
        };

        all.AddRange(intRows);
        sb.Append(BenchKit.Section("① int — 方向对比",
            "同一操作三种走法；基线由 C# 侧计时，另两行分别由各自的调用方计时。" +
            "三行跑同一个 " + BenchKit.Fmt(BenchKit.Times) + " 次，耗时(ms) 可直接比",
            intRows,
            "看点：两个跨界方向谁更贵 —— 这决定了引擎该把“批量下发”还是“事件推送”作为主路径。"));

        // ================= ② string =================
        string text = BenchKit.MakeText(BenchKit.TextLength);

        var stringRows = new List<BenchRow>
        {
            Row("string — 基线 C#→C#", "C# 调用自身方法，零跨界",
                BenchKit.MeasureFixed(() => BenchKit.LocalEchoString(text), BenchKit.Times), 1),

            Row("string(" + BenchKit.TextLength + ") — C#→JS", "[JSImport]，需编码转换",
                BenchKit.MeasureFixed(() => _ = BenchInterop.EchoString(text), BenchKit.Times), 1),

            BenchKit.FromJs(() => BenchInterop.CallStringN(BenchKit.Times, BenchKit.TextLength),
                "string(" + BenchKit.TextLength + ") — JS→C#", "[JSExport]，需编码转换（由 JS 侧计时）", BenchKit.Times),
        };

        all.AddRange(stringRows);
        sb.Append(BenchKit.Section("② string — 方向对比",
            "载荷固定为 " + BenchKit.TextLength + " 个字符",
            stringRows,
            "看点：字符串两个方向都要重建一份对方的字符串，差值主要来自两边的分配策略。"));

        // ================= ③ byte[]（对称走法：两边都含一次复制）=================
        var bytesRows = new List<BenchRow>();

        foreach (int len in BenchKit.PayloadSizes)
        {
            var buffer = new byte[len];

            bytesRows.Add(Row("byte[" + len + "] — 基线 C#→C#", "C# 调用自身方法，零跨界（且不复制）",
                BenchKit.MeasureFixed(() => BenchKit.LocalEchoBytes(buffer), BenchKit.TimesBytes), 1));

            bytesRows.Add(Row("byte[" + len + "] — C#→JS", "[JSImport] JS 建数组返回，C# 侧接收（一次复制）",
                BenchKit.MeasureFixed(() => _ = BenchInterop.MakeArray(len), BenchKit.TimesBytes), 1));

            bytesRows.Add(BenchKit.FromJs(() => BenchInterop.CallBytesN(BenchKit.TimesBytes, len),
                "byte[" + len + "] — JS→C#", "[JSExport] JS 传数组进 C#（一次复制，由 JS 侧计时）", BenchKit.TimesBytes));
        }

        all.AddRange(bytesRows);
        sb.Append(BenchKit.Section("③ byte[] — 方向对比（对称走法）",
            "按 " + string.Join(" / ", BenchKit.PayloadSizes) + " 分档；两边都含一次复制，才算拿“同样的方法”在比",
            bytesRows,
            "看点：随长度增长的速度决定“一帧能过界多少字节”；若 C#→JS 明显更贵，就该让 JS 主动推。"));

        // ================= ④ MemoryView（只有 C#→JS 这半边）=================
        var spanRows = new List<BenchRow>();

        foreach (int len in BenchKit.PayloadSizes)
        {
            var buffer = new byte[len];

            spanRows.Add(Row("MemoryView " + len + " B — 基线 C#→C#", "C# 自己写同样多的字节，零跨界",
                BenchKit.MeasureFixed(() => BenchKit.LocalFillSpan(buffer, len), BenchKit.TimesBytes), 1));

            spanRows.Add(Row("MemoryView " + len + " B — C#→JS", "[JSImport] 零拷贝：C# 备好缓冲，JS 直写",
                BenchKit.MeasureFixed(() => _ = BenchInterop.FillSpan(buffer, len), BenchKit.TimesBytes), 1));

            spanRows.Add(BenchKit.FromJs(() => BenchInterop.CallSpanN(BenchKit.TimesBytes, len),
                "MemoryView " + len + " B — JS→C#", "[JSExport] 由 JS 侧计时", BenchKit.TimesBytes));
        }

        all.AddRange(spanRows);
        sb.Append(BenchKit.Section("④ MemoryView — 方向对比（半边缺席）",
            "C#→JS 是零拷贝（JS 直写托管内存）；JS→C# 一行如实标为不可用",
            spanRows,
            "看点：两个方向不对称 —— C# 能把缓冲借给 JS 写，JS 却造不出运行时所需的 MemoryView 对象。" +
            "所以“让 JS 直接填 C# 的缓冲”是可行的（如每帧输入事件），反过来则只能靠 byte[] 复制。" +
            BenchKit.VerifyFillSpan(static (b, n) => BenchInterop.FillSpan(b, n))));

        // ================= 总表 =================
        sb.Append(BenchKit.Section("总表：四类 × 三种走法（按 ns/操作 升序，不可用行沉底）",
            "把四组并回一张表，专看跨类型的量级差。各行总次数不同，故以 ns/操作 排序、也只比这一列",
            all,
            "用法：判断方向差异看上面四张分组卡（同组基线对齐才有意义）；判断量级差看这张总表。"));

        return Task.FromResult(sb.ToString());
    }

    // C# 侧计时的行统一走这里（opsPerCall = 1：次数全在 BenchKit.Times 里）
    private static BenchRow Row(string name, string note, BenchTiming timing, long opsPerCall)
        => new(name, note, timing, opsPerCall);
}
