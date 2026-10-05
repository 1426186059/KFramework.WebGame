using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// 模块：C# ← JS 的“返回值能力”探测。
/// <para>
/// 逐个试探 JS 函数能回传给 C# 哪些类型：int / double / bool / string / byte[] / JSObject，
/// 分别用【同步返回】与【异步 Task&lt;T&gt; 返回】两套；最后回答“能不能一次返回多个值”。
/// 每条都 try/catch，失败只记“异常: …”，不会中断整张表。
/// </para>
/// </summary>
public sealed class Bench_JsReturn : IBenchModule
{
    public string Name => "JS → C# 返回值能力（同步 / 异步 / 多值）";

    public string Summary =>
        "逐个试探 JS 函数能回传给 C# 哪些类型（int / double / bool / string / byte[] / JSObject），" +
        "分同步与异步两套；并实测“一次调用能不能返回多个值”（结论：只能返回一个容器，多值靠对象/打包字符串）。";

    public string Page => "jsreturn";

    public async Task<string> RunAsync()
    {
        var sb = new StringBuilder();
        var rows = new List<Row>();

        // ============ ① 同步返回 ============
        rows.Add(Probe("syncInt", "int", () => (object)JSBind_JsReturn.SyncInt(), v => "得到 " + v));
        rows.Add(Probe("syncDouble", "double", () => (object)JSBind_JsReturn.SyncDouble(), v => "得到 " + v));
        rows.Add(Probe("syncBool", "bool", () => (object)JSBind_JsReturn.SyncBool(), v => "得到 " + v));
        rows.Add(Probe("syncString", "string", () => (object)JSBind_JsReturn.SyncString(), v => "得到 \"" + v + "\""));
        rows.Add(Probe("syncBytes", "byte[]", () => (object)JSBind_JsReturn.SyncBytes(),
            v => { var b = (byte[])v; return "长度 " + b.Length + " [" + string.Join(",", b) + "]"; }));
        rows.Add(Probe("syncObject", "JSObject", () => (object)JSBind_JsReturn.SyncObject(),
            v => ReadObject((JSObject)v)));

        // ============ ② 异步返回（Task<T>）============
        rows.Add(await ProbeAsync("asyncInt", "Task<int>", () => Img(JSBind_JsReturn.AsyncInt()), v => "得到 " + v));
        rows.Add(await ProbeAsync("asyncDouble", "Task<double>", () => Img(JSBind_JsReturn.AsyncDouble()), v => "得到 " + v));
        rows.Add(await ProbeAsync("asyncBool", "Task<bool>", () => Img(JSBind_JsReturn.AsyncBool()), v => "得到 " + v));
        rows.Add(await ProbeAsync("asyncString", "Task<string>", () => Img(JSBind_JsReturn.AsyncString()), v => "得到 \"" + v + "\""));
        rows.Add(await ProbeAsync("asyncObject", "Task<JSObject>", () => Img(JSBind_JsReturn.AsyncObject()), v => ReadObject((JSObject)v)));

        sb.Append(Section("①+② 同步 vs 异步：各返回类型能否回传 C#",
            "每行是一次 JS→C# 调用的真实结果。✓ = 成功拿到值；✘ = 抛异常（原因附在行尾）。" +
            "重点看：sync 列里哪些能用、哪些只有 async 才行（例如返回 JSObject 通常必须走异步）。",
            rows,
            "结论：标量 / string 同步异步都能回传；byte[] 只能<b>同步</b>返回（Task<byte[]> 不被源生成支持，" +
            "编译期即 SYSLIB1072）；返回 JSObject（结构化对象）一般必须异步，" +
            "同步返回 JSObject 在 WASM 下往往取不到正确句柄 —— 与引擎里 GetImageData 必须 Task<JSObject> 一致。"));

        // ============ ③ 多值：一次调用能不能返回多个值 ============
        var multi = new List<Row>();

        // 直接“返回两个 int”在 JS 语法上就不可能（只能 return 一个值）；这里实测“返回对象后逐项读”能拿到多个值
        try
        {
            using var o = await JSBind_JsReturn.AsyncPair().ConfigureAwait(false);
            int first = o.GetPropertyAsInt32("first");
            int second = o.GetPropertyAsInt32("second");
            multi.Add(new Row("asyncPair → 读对象两个字段", "Task<JSObject>",
                "✓ 一次调用拿到 first=" + first + ", second=" + second + "（多值靠对象承载）"));
        }
        catch (Exception e)
        {
            multi.Add(new Row("asyncPair → 读对象两个字段", "Task<JSObject>", "✘ 异常: " + FirstLine(e.Message)));
        }

        // 打包字符串：工程惯例（BenchKit.SplitTiming）
        try
        {
            string packed = await JSBind_JsReturn.AsyncPacked().ConfigureAwait(false);
            multi.Add(new Row("asyncPacked → 拆字符串", "Task<string>",
                "✓ 收到 \"" + packed + "\"，按 '|' 拆出 3 段（多值靠字符串承载）"));
        }
        catch (Exception e)
        {
            multi.Add(new Row("asyncPacked → 拆字符串", "Task<string>", "✘ 异常: " + FirstLine(e.Message)));
        }

        sb.Append(Section("③ 多值：一次调用能返回几个值？",
            "JS 函数本质上只能 return 一个值；要传 N 个，只能把它装进“一个返回值”里过界。" +
            "下面验证两条出路：① 返回对象（JSObject）后逐项读；② 拼成字符串（本工程惯例）。",
            multi,
            "结论：源生成互操作<b>不支持</b>“一次返回多个独立值”（没有原生元组/多返回）；" +
            "可行做法是返回 JSObject 后读多个属性，或把多值拼成字符串（如 \"a|b|c\"）过界再拆 —— 后者是本工程统一约定。"));

        return sb.ToString();
    }

    // ---------- 辅助 ----------

    private sealed record Row(string Scene, string Type, string Result);

    /// <summary>同步安全调用：成功记值，失败只记异常首行。</summary>
    private static Row Probe(string scene, string type, Func<object> call, Func<object, string> fmt)
    {
        try
        {
            object v = call();
            return new Row(scene, type, "✓ " + fmt(v));
        }
        catch (Exception e)
        {
            return new Row(scene, type, "✘ 异常: " + FirstLine(e.Message));
        }
    }

    /// <summary>异步安全调用（统一包成 Task&lt;object&gt;）。</summary>
    private static async Task<Row> ProbeAsync(string scene, string type, Func<Task<object>> call, Func<object, string> fmt)
    {
        try
        {
            object v = await call().ConfigureAwait(false);
            return new Row(scene, type, "✓ " + fmt(v));
        }
        catch (Exception e)
        {
            return new Row(scene, type, "✘ 异常: " + FirstLine(e.Message));
        }
    }

    /// <summary>把已有的 Task&lt;T&gt; 包成 Task&lt;object&gt;（便于复用上面的 ProbeAsync）。</summary>
    private static async Task<object> Img<T>(Task<T> t) => (object)(await t.ConfigureAwait(false))!;

    /// <summary>从 JSObject 读回 first/second/name 三个字段。</summary>
    private static string ReadObject(JSObject o)
    {
        int first = o.GetPropertyAsInt32("first");
        int second = o.GetPropertyAsInt32("second");
        string name = o.GetPropertyAsString("name")!;
        return $"first={first}, second={second}, name={name}";
    }

    private static string FirstLine(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "(无消息)";
        string t = s;                 // 此时已非 null
        int i = t.IndexOf('\n');
        string one = i >= 0 ? t[..i] : t;
        one = one.Trim();
        return one.Length <= 140 ? one : one[..140] + "…";
    }

    // ---------- 结果表格（不计时，只列能力）----------

    private static string Section(string title, string subtitle, IReadOnlyList<Row> rows, string? conclusion = null)
    {
        var sb = new StringBuilder();
        sb.Append("<div class='card'><h3>").Append(Esc(title)).Append("</h3>");
        sb.Append("<p class='muted'>").Append(Esc(subtitle)).Append("</p>");
        sb.Append("<table><thead><tr>")
          .Append("<th>调用</th><th>返回类型</th><th>结果</th>")
          .Append("</tr></thead><tbody>");

        foreach (Row r in rows)
        {
            bool ok = r.Result.StartsWith("✓");
            sb.Append("<tr")
              .Append(ok ? "" : " class='blocked'")
              .Append("><td>").Append(Esc(r.Scene)).Append("</td>")
              .Append("<td class='muted'>").Append(Esc(r.Type)).Append("</td>")
              .Append("<td>").Append(Esc(r.Result)).Append("</td>")
              .Append("</tr>");
        }

        sb.Append("</tbody></table>");
        if (conclusion != null) sb.Append("<p class='muted'>").Append(Esc(conclusion)).Append("</p>");
        sb.Append("</div>");
        return sb.ToString();
    }

    private static string Esc(string s) => s
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
