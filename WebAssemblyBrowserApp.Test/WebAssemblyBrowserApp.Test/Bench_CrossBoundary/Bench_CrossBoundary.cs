using System.Collections.Generic;
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
/// 载荷覆盖 int、string，以及 byte[] 的 16 / 256 / 2048 三档。全部结果按耗时升序排列，
/// 所以每个载荷下三行的先后顺序，直接回答"哪个方向更贵、贵多少倍"。
/// </para>
/// <para>
/// 这里 byte[] 统一用<b>byte[] 封送</b>这条对称的走法（C#→JS 取 JS 返回的数组、JS→C# 传数组进 C#），
/// 两边都含一次复制，才算是"同样的方法"在比。零拷贝的 MemoryView 单独在 <see cref="Bench_CsToJs"/> 里与拷贝路线对照。
/// </para>
/// </summary>
public sealed class Bench_CrossBoundary : IBenchModule
{
    public string Name => "跨界方向对比（C#→C# 基线 / C#→JS / JS→C#）";

    public string Summary =>
        "同一操作三种走法横向比：基线（C# 调自身）、C# 调 JS、JS 调 C#。载荷为 int、string、byte[]（16/256/2048）。" +
        "全部按耗时升序，看每个载荷下三行的倍数关系即可判断方向差异。";

    private const int Times = 20000;        // 标量：单次便宜，多打几次
    private const int TimesBytes = 2000;    // byte[]：单次较贵，相应减少次数

    public Task<string> RunAsync()
    {
        // 预热（首次跨界含 JIT 与绑定解析）
        BenchInterop.CallIntN(2000);
        BenchInterop.CallStringN(500, 16);
        BenchInterop.CallBytesN(200, 16);

        var rows = new List<BenchRow>();

        // ================= int =================
        rows.Add(Row("int — 基线 C#→C#", "C# 调用自身方法，零跨界",
            BenchKit.Measure(() => { for (int i = 0; i < Times; i++) BenchKit.LocalEchoInt(i); }, Times), Times));

        rows.Add(Row("int — C#→JS", "[JSImport] C# 主动调过去",
            BenchKit.Measure(() => { for (int i = 0; i < Times; i++) _ = BenchInterop.EchoInt(i); }, Times), Times));

        rows.Add(Row("int — JS→C#", "[JSExport] JS 主动调进来（由 JS 侧计时）",
            BenchInterop.CallIntN(Times), Times));

        // ================= string =================
        string text = BenchKit.MakeText(BenchKit.TextLength);

        rows.Add(Row("string — 基线 C#→C#", "C# 调用自身方法，零跨界",
            BenchKit.Measure(() => { for (int i = 0; i < Times; i++) BenchKit.LocalEchoString(text); }, Times), Times));

        rows.Add(Row("string(" + BenchKit.TextLength + ") — C#→JS", "[JSImport]，需编码转换",
            BenchKit.Measure(() => { for (int i = 0; i < Times; i++) _ = BenchInterop.EchoString(text); }, Times), Times));

        rows.Add(Row("string(" + BenchKit.TextLength + ") — JS→C#", "[JSExport]，需编码转换（由 JS 侧计时）",
            BenchInterop.CallStringN(Times, BenchKit.TextLength), Times));

        // ================= byte[]：三档 × 三个方向 =================
        foreach (int len in BenchKit.PayloadSizes)
        {
            var buffer = new byte[len];

            rows.Add(Row("byte[" + len + "] — 基线 C#→C#", "C# 调用自身方法，零跨界（且不复制）",
                BenchKit.Measure(() => { for (int i = 0; i < TimesBytes; i++) BenchKit.LocalEchoBytes(buffer); }, TimesBytes), TimesBytes));

            rows.Add(Row("byte[" + len + "] — C#→JS", "[JSImport] JS 建数组返回，C# 侧接收（一次复制）",
                BenchKit.Measure(() => { for (int i = 0; i < TimesBytes; i++) _ = BenchInterop.MakeArray(len); }, TimesBytes), TimesBytes));

            rows.Add(Row("byte[" + len + "] — JS→C#", "[JSExport] JS 传数组进 C#（一次复制，由 JS 侧计时）",
                BenchInterop.CallBytesN(TimesBytes, len), TimesBytes));
        }

        return Task.FromResult(BenchKit.Section(
            "跨界方向对比（同一操作，三种走法）",
            "基线 = C# 调用自身方法（零跨界）。标量各 " + BenchKit.Fmt(Times) + " 次，byte[] 各 " + BenchKit.Fmt(TimesBytes) + " 次。" +
            "按耗时升序排列 —— 每个载荷三行的相对 x 就是比基线慢多少倍",
            rows,
            "看点：① 基线应稳定排最前；② C#→JS 与 JS→C# 谁更贵 —— 这决定了引擎该把「批量下发」还是「事件推送」作为主路径；" +
            "③ byte[] 随长度增长的速度，决定是否值得为零拷贝单独保留一条 MemoryView 路径。"));
    }

    // 统一换算：C# 侧计时的用 Measure 结果；JS 侧计时的直接构造
    private static BenchRow Row(string name, string note, BenchTiming timing, long opsPerCall)
        => new(name, note, timing, opsPerCall);

    private static BenchRow Row(string name, string note, double jsMeasuredMs, int times)
        => new(name, note, new BenchTiming(jsMeasuredMs, times), 1);
}
