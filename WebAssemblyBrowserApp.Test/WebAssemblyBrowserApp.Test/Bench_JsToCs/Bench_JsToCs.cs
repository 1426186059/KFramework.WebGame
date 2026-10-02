using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// 模块：JS 调用 C#（<c>[JSExport]</c>）的跨界开销。
/// <para>
/// 计时只能在调用方（JS）侧做，所以这里是"让 JS 连打 N 次并回报耗时"。
/// 同样以 <b>C# 调用自身方法</b>为基线 —— 两个方向的基线一致，横向对比才有意义。
/// </para>
/// </summary>
public sealed class Bench_JsToCs : IBenchModule
{
    public string Name => "JS → C# 跨界（含 C#→C# 基线）";

    public string Summary =>
        "JS 连续调用 C# 的导出方法并回报耗时。按 int / string / byte[] 三组给出，每组都以 C# 调用自身方法为基线；" +
        "byte[] 按 16/256/2048 分档。全部按耗时升序排列。";

    private const int Times = 20000;        // 标量类：单次便宜，多打几次
    private const int TimesBytes = 2000;    // byte[]：单次较贵，相应减少次数

    public Task<string> RunAsync()
    {
        // 预热：首次跨界含 JIT 与绑定解析，不应计入稳定态
        BenchInterop.CallTickN(2000);
        BenchInterop.CallIntN(2000);
        BenchInterop.CallStringN(500, 16);
        BenchInterop.CallBytesN(200, 16);

        var rows = new List<BenchRow>();

        // ================= int =================
        // 基线在 C# 侧计时（调用方是 C#），与下面 JS 侧计时的行并列展示
        rows.Add(new BenchRow(
            "基线：C#→C# EchoInt", "C# 调用自身方法，零跨界 —— 成本参照",
            BenchKit.Measure(() => { for (int i = 0; i < Times; i++) BenchKit.LocalEchoInt(i); }, Times), Times));

        rows.Add(Row("JS→C# CsTick()", "无参数、返回常量 int —— 纯跨界下限",
            BenchInterop.CallTickN(Times), Times));

        rows.Add(Row("JS→C# CsEchoInt(int)", "一个 int 进、一个 int 出",
            BenchInterop.CallIntN(Times), Times));

        // ================= string =================
        string text = BenchKit.MakeText(BenchKit.TextLength);

        rows.Add(new BenchRow(
            "基线：C#→C# EchoString", "C# 调用自身方法，零跨界",
            BenchKit.Measure(() => { for (int i = 0; i < Times; i++) BenchKit.LocalEchoString(text); }, Times), Times));

        rows.Add(Row("JS→C# CsEchoString(" + BenchKit.TextLength + " 字符)",
            "JS 字符串 → 托管字符串，需编码转换",
            BenchInterop.CallStringN(Times, BenchKit.TextLength), Times));

        // ================= byte[]：基线 + 跨界，按长度分档 =================
        foreach (int len in BenchKit.PayloadSizes)
        {
            var buffer = new byte[len];

            rows.Add(new BenchRow(
                "基线：C#→C# EchoBytes(" + len + " B)", "C# 调用自身方法，零跨界（且不复制）",
                BenchKit.Measure(() => { for (int i = 0; i < TimesBytes; i++) BenchKit.LocalEchoBytes(buffer); }, TimesBytes), TimesBytes));

            rows.Add(Row("JS→C# CsEchoBytes(" + len + " B)",
                "ArrayBuffer → 托管 byte[]，封送时复制一次",
                BenchInterop.CallBytesN(TimesBytes, len), TimesBytes));
        }

        return Task.FromResult(BenchKit.Section(
            "JS → C# 单次跨界耗时（基线 = C# 调用自身方法）",
            "标量各连打 " + BenchKit.Fmt(Times) + " 次，byte[] 各连打 " + BenchKit.Fmt(TimesBytes) + " 次；" +
            "基线行由 C# 侧计时，跨界行由 JS 侧计时，两者都是「单次调用的耗时」，可直接比",
            rows,
            "看点：基线（C#→C#）应排最前；它与 JS→C# 各行的倍数就是 JS 调进来的代价。" +
            "这个方向每帧必然发生（帧回调本身就是 JS→C#），所以它的绝对开销决定了该不该合并调用。"));
    }

    // 计时发生在 JS 侧，这里只是把回报的毫秒换算成统一的行格式
    private static BenchRow Row(string name, string note, double ms, int times)
        => new(name, note, new BenchTiming(ms, times), 1);
}
