using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// 模块：C# 调用 JS（<c>[JSImport]</c>）的跨界开销，按
/// <b>int / string / byte[] / MemoryView</b> 四类<b>分组</b>给出。
/// <para>
/// 每组都先给<b>基线</b>（C# 调用自身方法，零跨界），再给出跨界的数字 ——
/// 这样“跨界到底多付了多少倍”才是可比的；只给绝对值说明不了问题。
/// </para>
/// <para>
/// 分组而不是一张大表：四类载荷的成本结构完全不同（标量是固定成本主导、字节块随体积线性增长），
/// 混在一张表里按耗时排序，只会变成“所有标量行挤在最前、所有字节行吊在最后”，
/// 看不出同一类内部该比的那个倍数。
/// </para>
/// </summary>
public sealed class Bench_CsToJs : IBenchModule
{
    public string Name => "C# → JS 跨界（int / string / byte[] / MemoryView 四组对照）";

    public string Summary =>
        "C# 连续调用 JS 函数。四类各一组：int（含无载荷的纯跨界下限）、string（256 字符）、" +
        "byte[]（16/256/2048 三档，【传入 JS】与【JS 返回】两条路线并列）、" +
        "MemoryView（同三档，零拷贝直写 C# 缓冲）。每组都以 C# 调用自身方法为基线，末尾附一张四类混排总表。";

    public string Page => "cstojs";

    public Task<string> RunAsync()
    {
        var all = new List<BenchRow>();
        var sb = new StringBuilder();

        // ================= ① int =================
        // 组内三行跑同一个 BenchKit.Times（固定次数，不自适应）—— 这样"计时窗口(ms)"这一列才能横比
        var intRows = new List<BenchRow>
        {
            new("基线：C#→C# EchoInt", "C# 调用自身方法，零跨界 —— 成本参照",
                BenchKit.MeasureFixed(() => BenchKit.LocalEchoInt(1), BenchKit.Times), 1),

            new("C#→JS Noop()", "调用空的 JS 函数 —— 纯跨界下限（无载荷）",
                BenchKit.MeasureFixed(() => BenchInterop.Noop(), BenchKit.Times), 1),

            new("C#→JS EchoInt(int)", "一个 int 进、一个 int 出 —— 比 Noop 多出的就是标量封送",
                BenchKit.MeasureFixed(() => _ = BenchInterop.EchoInt(1), BenchKit.Times), 1),
        };

        all.AddRange(intRows);
        sb.Append(BenchKit.Section("① int — C# → JS",
            "基线 = C# 调用自身方法。Noop 是“过一次界”的地板价，EchoInt 与它的差即标量封送的成本",
            intRows,
            "看点：标量单次成本几乎全是固定开销，与数值大小无关 —— 所以引擎里“每个顶点问一次 JS”必然亏，该成批问一次。"));

        // ================= ② string =================
        string text = BenchKit.MakeText(BenchKit.TextLength);

        var stringRows = new List<BenchRow>
        {
            new("基线：C#→C# EchoString", "C# 调用自身方法，零跨界",
                BenchKit.MeasureFixed(() => BenchKit.LocalEchoString(text), BenchKit.Times), 1),

            new("C#→JS EchoString(" + BenchKit.TextLength + " 字符)", "托管字符串 → JS 字符串，需做一次编码转换并分配 JS 字符串",
                BenchKit.MeasureFixed(() => _ = BenchInterop.EchoString(text), BenchKit.Times), 1),
        };

        all.AddRange(stringRows);
        sb.Append(BenchKit.Section("② string — C# → JS",
            "载荷固定为 " + BenchKit.TextLength + " 个字符（不与字节块一样分档，免得表格臃肿）",
            stringRows,
            "看点：字符串过界必然重建一份 JS 字符串，成本随长度线性增长 —— 长文本（如整段 JSON）宁可走字节，也别直接当 string 传。"));

        // ================= ③ byte[]：传入 / 返回两条路线，按长度分档 =================
        var bytesRows = new List<BenchRow>();

        foreach (int len in BenchKit.PayloadSizes)
        {
            var buffer = new byte[len];

            bytesRows.Add(new BenchRow(
                "基线：C#→C# EchoBytes(" + len + " B)", "C# 调用自身方法，零跨界（且不复制）",
                BenchKit.MeasureFixed(() => BenchKit.LocalEchoBytes(buffer), BenchKit.TimesBytes), 1));

            bytesRows.Add(new BenchRow(
                "C#→JS SendBytes(" + len + " B) — 传入", "C# 把数组交给 JS：封送时复制一次，JS 拿到的是副本",
                BenchKit.MeasureFixed(() => _ = BenchInterop.SendBytes(buffer), BenchKit.TimesBytes), 1));

            bytesRows.Add(new BenchRow(
                "C#→JS MakeArray(" + len + " B) — 返回", "JS 新建数组交回 C#：一次分配 + 一次复制，C# 收成新 byte[]",
                BenchKit.MeasureFixed(() => _ = BenchInterop.MakeArray(len), BenchKit.TimesBytes), 1));
        }

        all.AddRange(bytesRows);
        sb.Append(BenchKit.Section("③ byte[] — C# → JS（传入 与 返回 两条路线）",
            "两条路线共用同一份源数据（见 main.js 的 spanSrc），唯一差别是“谁把字节交到对岸手里”；" +
            "按 " + string.Join(" / ", BenchKit.PayloadSizes) + " 分档，看成本是固定开销主导还是随体积线性增长",
            bytesRows,
            "看点：① 小档倍数很大（固定成本主导），大档逐渐被拷贝成本追平 —— 这条曲线决定“该不该为省一次跨界而合并批次”；" +
            "② 传入与返回哪条更贵，决定引擎该“推”还是“拉”数据。" + VerifyBytes()));

        // ================= ④ MemoryView：零拷贝直写 =================
        var spanRows = new List<BenchRow>();

        foreach (int len in BenchKit.PayloadSizes)
        {
            var buffer = new byte[len];

            spanRows.Add(new BenchRow(
                "基线：C#→C# FillSpan(" + len + " B)", "C# 自己往数组里写同样多的字节，零跨界",
                BenchKit.MeasureFixed(() => BenchKit.LocalFillSpan(buffer, len), BenchKit.TimesBytes), 1));

            spanRows.Add(new BenchRow(
                "C#→JS FillSpan(" + len + " B) — MemoryView", "零拷贝：C# 提供缓冲，JS 用 view.set() 直接写进托管内存",
                BenchKit.MeasureFixed(() => _ = BenchInterop.FillSpan(buffer, len), BenchKit.TimesBytes), 1));
        }

        all.AddRange(spanRows);
        sb.Append(BenchKit.Section("④ MemoryView — C# → JS（零拷贝）",
            "C# 侧声明 Span<byte> + JSMarshalAs<MemoryView>，JS 侧拿到的是托管内存上的视图（不是 TypedArray）",
            spanRows,
            "看点：与 ③ 组同长度的行直接比 —— 同样是“让 n 个字节过界”，MemoryView 省掉的是分配与那次额外复制。" +
            "注意视图只有 set / copyTo / slice，没有 [] 索引器。"));

        // ================= 总表：四类混排 =================
        sb.Append(BenchKit.Section("总表：四类 × 全部走法（按 ns/操作 升序）",
            "把四组并回一张表，专看跨类型的量级差：标量（固定成本）与字节块（随体积增长）差几个数量级。" +
            "各行总次数不同，故以 ns/操作 排序、也只比这一列",
            all,
            "用法：组内对比看上面四张分组卡（同一基线才好比）；跨类型的量级差看这张总表。"));

        return Task.FromResult(sb.ToString());
    }

    /// <summary>
    /// 抽验一次“字节到底有没有过界”，不计时。
    /// <para>
    /// 计时只说明快慢，说明不了对错 —— MemoryView 这一档早先就是“跑得飞快、其实一个字节都没写”
    /// （JS 侧误用 <c>view[i] = ...</c>，而视图没有索引器，那是给 JS 对象挂属性）。
    /// 三条结论直接写进 ③ 组的看点里，不必另开一张卡。
    /// </para>
    /// </summary>
    private static string VerifyBytes()
    {
        string fill = BenchKit.VerifyFillSpan(static (b, n) => BenchInterop.FillSpan(b, n));

        byte[] got = BenchInterop.MakeArray(256);
        bool arrayOk = got.Length == 256 && got[255] == 255;

        var send = new byte[] { 1, 2, 3, 4 };
        _ = BenchInterop.SendBytes(send);
        bool sendIntact = send[0] == 1 && send[3] == 4;

        return " 抽验：" + fill +
               "byte[] 返回长度/内容" + (arrayOk ? "正确 ✓" : "异常 ✘") +
               "；C# 传出 byte[] 后原数组" + (sendIntact ? "未被改动 ✓（复制语义）" : "被改动 ✘") + "。";
    }
}
