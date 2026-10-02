using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// 模块：JS 调用 C#（<c>[JSExport]</c>）的跨界开销，按
/// <b>int / string / byte[] / MemoryView</b> 四类<b>分组</b>给出。
/// <para>
/// 计时只能在调用方（JS）侧做，所以这里是"让 JS 连打 N 次并回报耗时"。
/// 同样以 <b>C# 调用自身方法</b>为基线 —— 两个方向的基线一致，横向对比才有意义。
/// </para>
/// <para>
/// 四类并不都能走通：<b>MemoryView 在 JS→C# 方向没有对应物</b>（C# 侧可以声明
/// <c>Span&lt;byte&gt;</c>，但 JS 给不出运行时要的 MemoryView 对象）。
/// 这一格照样摆进表里、并标明"不可用 + 原因" —— 空白会让人以为没测，写清楚才有对照价值。
/// </para>
/// </summary>
public sealed class Bench_JsToCs : IBenchModule
{
    public string Name => "JS → C# 跨界（int / string / byte[] / MemoryView 四组对照）";

    public string Summary =>
        "JS 连续调用 C# 的导出方法并回报耗时。四类各一组：int（含无参数下限）、string（256 字符）、" +
        "byte[]（16/256/2048）、MemoryView（同三档 —— 实测走不通，表里按“不可用”标出原因）。" +
        "每组都以 C# 调用自身方法为基线，末尾附一张四类混排总表。";

    public string Page => "jstocs";

    public Task<string> RunAsync()
    {
        // 预热：首次跨界含 JIT 与绑定解析，不应计入稳定态
        JSBind_JsToCs.CallTickN(2000);
        JSBind_JsToCs.CallIntN(2000);
        JSBind_JsToCs.CallStringN(500, 16);
        JSBind_JsToCs.CallBytesN(200, 16);
        JSBind_JsToCs.CallSpanN(1, 16);      // 这一格会抛错，但首次调用仍会解析绑定；JS 侧已 try/catch

        var all = new List<BenchRow>();
        var sb = new StringBuilder();

        // ================= ① int =================
        var intRows = new List<BenchRow>
        {
            // 基线在 C# 侧计时（调用方是 C#）；与下面 JS 侧计时的行跑【同一个 BenchKit.Times】，
            // 否则同一张表里的毫秒数根本不能横比
            new("基线：C#→C# EchoInt", "C# 调用自身方法，零跨界 —— 成本参照",
                BenchKit.MeasureFixed(() => BenchKit.LocalEchoInt(1), BenchKit.Times), 1),

            BenchKit.FromJs(() => JSBind_JsToCs.CallTickN(BenchKit.Times),
                "JS→C# CsTick()", "无参数、返回常量 int —— 纯跨界下限", BenchKit.Times),

            BenchKit.FromJs(() => JSBind_JsToCs.CallIntN(BenchKit.Times),
                "JS→C# CsEchoInt(int)", "一个 int 进、一个 int 出 —— 与 CsTick 的差即标量封送", BenchKit.Times),
        };

        all.AddRange(intRows);
        sb.Append(BenchKit.Section("① int — JS → C#",
            "基线由 C# 侧计时、跨界行由 JS 侧计时，两行跑同一个 " + BenchKit.Fmt(BenchKit.Times) + " 次，可直接比",
            intRows,
            "看点：这个方向每帧必然发生（帧回调本身就是 JS→C#），所以它的地板价决定了该不该合并调用。"));

        // ================= ② string =================
        string text = BenchKit.MakeText(BenchKit.TextLength);

        var stringRows = new List<BenchRow>
        {
            new("基线：C#→C# EchoString", "C# 调用自身方法，零跨界",
                BenchKit.MeasureFixed(() => BenchKit.LocalEchoString(text), BenchKit.Times), 1),

            BenchKit.FromJs(() => JSBind_JsToCs.CallStringN(BenchKit.Times, BenchKit.TextLength),
                "JS→C# CsEchoString(" + BenchKit.TextLength + " 字符)",
                "JS 字符串 → 托管字符串，需做一次编码转换并分配托管字符串", BenchKit.Times),
        };

        all.AddRange(stringRows);
        sb.Append(BenchKit.Section("② string — JS → C#",
            "载荷固定为 " + BenchKit.TextLength + " 个字符",
            stringRows,
            "看点：文本从 DOM 回传（如输入框的值）走的就是这条路 —— 高频回传时值得先攒一批再送。"));

        // ================= ③ byte[] =================
        var bytesRows = new List<BenchRow>();

        foreach (int len in BenchKit.PayloadSizes)
        {
            var buffer = new byte[len];

            bytesRows.Add(new BenchRow(
                "基线：C#→C# EchoBytes(" + len + " B)", "C# 调用自身方法，零跨界（且不复制）",
                BenchKit.MeasureFixed(() => BenchKit.LocalEchoBytes(buffer), BenchKit.TimesBytes), 1));

            bytesRows.Add(BenchKit.FromJs(() => JSBind_JsToCs.CallBytesN(BenchKit.TimesBytes, len),
                "JS→C# CsEchoBytes(" + len + " B)",
                "Uint8Array → 托管 byte[]，封送时复制一次，C# 每收到一份新数组（可长期持有）", BenchKit.TimesBytes));
        }

        all.AddRange(bytesRows);
        sb.Append(BenchKit.Section("③ byte[] — JS → C#",
            "按 " + string.Join(" / ", BenchKit.PayloadSizes) + " 分档，看固定成本与拷贝成本谁主导",
            bytesRows,
            "看点：这是 JS→C# 方向传大块数据的唯一路线（见 ④ 组），" +
            "所以它的绝对开销决定了“一帧能收多少字节”。" +
            "注：裸 ArrayBuffer 传不进去，必须给 Uint8Array（运行时只认 Array / TypedArray）。"));

        // ================= ④ MemoryView：实测走不通 =================
        var spanRows = new List<BenchRow>();

        foreach (int len in BenchKit.PayloadSizes)
        {
            var buffer = new byte[len];

            spanRows.Add(new BenchRow(
                "基线：C#→C# FillSpan(" + len + " B)", "C# 自己往数组里写同样多的字节，零跨界",
                BenchKit.MeasureFixed(() => BenchKit.LocalFillSpan(buffer, len), BenchKit.TimesBytes), 1));

            // JS 侧回报 "最快|最慢|错误"：错误为空说明支持；否则这一格记为"不可用"并带上原因
            spanRows.Add(BenchKit.FromJs(() => JSBind_JsToCs.CallSpanN(BenchKit.TimesBytes, len),
                "JS→C# CsFillSpan(" + len + " B) — MemoryView",
                "JS 传 Uint8Array 给 C# 的 Span<byte> 参数（由 JS 侧计时）", BenchKit.TimesBytes));
        }

        all.AddRange(spanRows);
        sb.Append(BenchKit.Section("④ MemoryView — JS → C#（实测不可用）",
            "C# 侧入口：JSBind_JsToCs.CsFillSpan（参数 Span<byte> + JSMarshalAs<MemoryView>）；" +
            "JS 侧只能给出 Uint8Array",
            spanRows,
            "结论：这个方向没有 MemoryView —— 运行时要求的是它内部的 MemoryView 对象" +
            "（断言 Expected MemoryViewType.Byte），而浏览器端没有公开的 createMemoryView，给 Uint8Array 会被直接拒绝。" +
            "于是 JS→C# 要传大块数据，只有 ③ 组那条 byte[]（每次一份新数组，业务层可长期持有）。" +
            "反方向（C#→JS）的 MemoryView 是可用的，见 Bench_CsToJs 的 ④ 组 —— 两个方向并不对称。"));

        // ================= 总表：四类混排 =================
        sb.Append(BenchKit.Section("总表：四类 × 全部走法（按 ns/操作 升序，不可用行沉底）",
            "把四组并回一张表，专看跨类型的量级差。各行总次数不同，故以 ns/操作 排序、也只比这一列",
            all,
            "用法：组内对比看上面四张分组卡（同一基线才好比）；跨类型的量级差看这张总表。"));

        return Task.FromResult(sb.ToString());
    }
}
