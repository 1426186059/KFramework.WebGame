using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// 模块：C# 调用 JS（<c>[JSImport]</c>）的跨界开销。
/// <para>
/// 每组都先给<b>基线</b>（C# 调用自身方法，零跨界），再给出跨界的数字 ——
/// 这样"跨界到底多付了多少倍"才是可比的；只给绝对值说明不了问题。
/// </para>
/// <para>
/// byte[] 额外给出两条路线：<b>MemoryView</b>（C# 提供缓冲让 JS 直写，零拷贝）
/// 与 <b>byte[] 返回</b>（JS 建数组交给 C#，要复制一次并分配）。这正是引擎里
/// "每帧输入事件怎么过界"所面临的取舍。
/// </para>
/// </summary>
public sealed class Bench_CsToJs : IBenchModule
{
    public string Name => "C# → JS 跨界（含 C#→C# 基线）";

    public string Summary =>
        "C# 连续调用 JS 函数，按 int / string / byte[] 三组给出，每组都以 C# 调用自身方法为基线。" +
        "byte[] 额外对比 MemoryView 零拷贝与 byte[] 拷贝两条路线，并按 16/256/2048 分档。全部按耗时升序排列。";

    private const int Loop = 2000;

    public Task<string> RunAsync()
    {
        var rows = new List<BenchRow>();

        // ================= int =================
        rows.Add(new BenchRow(
            "基线：C#→C# EchoInt", "C# 调用自身方法，零跨界 —— 成本参照",
            BenchKit.Measure(() => { for (int i = 0; i < Loop; i++) BenchKit.LocalEchoInt(i); }, Loop), Loop));

        rows.Add(new BenchRow(
            "C#→JS Noop()", "调用空的 JS 函数 —— 纯跨界下限（无载荷）",
            BenchKit.Measure(() => { for (int i = 0; i < Loop; i++) BenchInterop.Noop(); }, Loop), Loop));

        rows.Add(new BenchRow(
            "C#→JS EchoInt(int)", "一个 int 进、一个 int 出",
            BenchKit.Measure(() => { for (int i = 0; i < Loop; i++) _ = BenchInterop.EchoInt(i); }, Loop), Loop));

        // ================= string =================
        string text = BenchKit.MakeText(BenchKit.TextLength);

        rows.Add(new BenchRow(
            "基线：C#→C# EchoString", "C# 调用自身方法，零跨界",
            BenchKit.Measure(() => { for (int i = 0; i < Loop; i++) BenchKit.LocalEchoString(text); }, Loop), Loop));

        rows.Add(new BenchRow(
            "C#→JS EchoString(" + BenchKit.TextLength + " 字符)", "托管字符串 → JS 字符串，需编码转换",
            BenchKit.Measure(() => { for (int i = 0; i < Loop; i++) _ = BenchInterop.EchoString(text); }, Loop), Loop));

        // ================= byte[]：基线 + 两条路线，按长度分档 =================
        foreach (int len in BenchKit.PayloadSizes)
        {
            var buffer = new byte[len];

            rows.Add(new BenchRow(
                "基线：C#→C# EchoBytes(" + len + " B)", "C# 调用自身方法，零跨界（且不复制）",
                BenchKit.Measure(() => { for (int i = 0; i < Loop; i++) BenchKit.LocalEchoBytes(buffer); }, Loop), Loop));

            rows.Add(new BenchRow(
                "C#→JS FillSpan(" + len + " B) — MemoryView", "零拷贝：C# 提供缓冲，JS 直接往里写",
                BenchKit.Measure(() => { for (int i = 0; i < Loop; i++) BenchInterop.FillSpan(buffer, len); }, Loop), Loop));

            rows.Add(new BenchRow(
                "C#→JS MakeArray(" + len + " B) — byte[]", "拷贝：JS 建数组交给 C#，一次复制 + 一次分配",
                BenchKit.Measure(() => { for (int i = 0; i < Loop; i++) _ = BenchInterop.MakeArray(len); }, Loop), Loop));
        }

        return Task.FromResult(BenchKit.Section(
            "C# → JS 单次跨界耗时（基线 = C# 调用自身方法）",
            "每种方式循环 " + BenchKit.Fmt(Loop) + " 次，全部按耗时升序；基线与同一载荷的跨界行可直接对比倍数",
            rows,
            "看点：① 基线（C#→C#）通常排在最前，它与跨界行的倍数就是跨界的代价；" +
            "② 体积小的时候倍数很大（固定成本主导），体积增大后拷贝成本线性上升；" +
            "③ byte[] 同长度下 MemoryView 若明显快于 byte[] 返回，说明零拷贝值得保留。"));
    }
}
