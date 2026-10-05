using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

/// <summary>
/// <b>Bench_JsReturn 模块专属</b>的互操作绑定，对应 wwwroot/bench_jsreturn.js。
/// 每个绑定只返回一个值，分【同步返回】与【异步 Task&lt;T&gt; 返回】两套，
/// 用来实测 C# ← JS 方向到底能收下哪些类型。
/// </summary>
public static partial class JSBind_JsReturn
{
    // —— 标量：同步 / 异步 ——
    [JSImport("syncInt", "bench_jsreturn")]      public static partial int SyncInt();
    [JSImport("asyncInt", "bench_jsreturn")]     public static partial Task<int> AsyncInt();

    [JSImport("syncDouble", "bench_jsreturn")]   public static partial double SyncDouble();
    [JSImport("asyncDouble", "bench_jsreturn")]  public static partial Task<double> AsyncDouble();

    [JSImport("syncBool", "bench_jsreturn")]     public static partial bool SyncBool();
    [JSImport("asyncBool", "bench_jsreturn")]    public static partial Task<bool> AsyncBool();

    [JSImport("syncString", "bench_jsreturn")]   public static partial string SyncString();
    [JSImport("asyncString", "bench_jsreturn")]  public static partial Task<string> AsyncString();

    // —— byte[]（只能同步返回；Task<byte[]> 不被源生成支持，编译期即 SYSLIB1072）——
    [JSImport("syncBytes", "bench_jsreturn")]    public static partial byte[] SyncBytes();

    // —— JSObject（返回单个 JS 对象）——
    [JSImport("syncObject", "bench_jsreturn")]   public static partial JSObject SyncObject();
    [JSImport("asyncObject", "bench_jsreturn")]  public static partial Task<JSObject> AsyncObject();

    // —— “多值”的两条出路 ——
    [JSImport("asyncPair", "bench_jsreturn")]    public static partial Task<JSObject> AsyncPair();
    [JSImport("asyncPacked", "bench_jsreturn")]  public static partial Task<string> AsyncPacked();
}
