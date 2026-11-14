using System.Runtime.InteropServices.JavaScript;

/// <summary>
/// <b>Bench_Json 模块专属</b>的互操作绑定，对应 wwwroot/bench_json.js。
/// <para>
/// 本模块测的是「7MB 配置文件的 JSON 解析 / 序列化」在 C# 与 JS 两侧的差距，
/// 因此数据要两边都能拿到：JS 在模块顶层把文件 <c>fetch</c> 下来缓存，
/// C# 用 <see cref="Json_GetText"/> 一次性取回文本（<b>不在计时循环内</b>）。
/// </para>
/// <para>
/// 计时只能在调用方侧做：解析 / 序列化这两种操作<b>没有跨界</b>，
/// 所以 JS 侧自己连打 <c>JSON.parse</c> / <c>JSON.stringify</c> 并回报耗时，
/// C# 侧则各自在 C# 里计时 —— 两边跑<b>同一个次数</b>，毫秒数才可直接横比。
/// </para>
/// </summary>
public static partial class JSBind_Json
{
    /// <summary>C# 从 JS 一次性取回 7MB 配置文本（文件由 JS 在顶层 await 拉取）。</summary>
    [JSImport("Json_GetText", "bench_json")]
    public static partial string? Json_GetText();

    /// <summary>JS 侧连打 <c>JSON.parse</c> 并回报「最快ms|最慢ms|错误」。</summary>
    [JSImport("CallParseN", "bench_json")]
    public static partial string CallParseN(int times);

    /// <summary>JS 侧连打 <c>JSON.stringify</c> 并回报「最快ms|最慢ms|错误」。</summary>
    [JSImport("CallStringifyN", "bench_json")]
    public static partial string CallStringifyN(int times);
}
