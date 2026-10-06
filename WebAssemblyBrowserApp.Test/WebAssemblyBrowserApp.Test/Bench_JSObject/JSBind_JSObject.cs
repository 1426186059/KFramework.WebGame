using System;
using System.Runtime.InteropServices.JavaScript;

/// <summary>
/// <b>Bench_JSObject 模块专属</b>的互操作绑定，对应 wwwroot/bench_jsobject.js。
/// <para>
/// 本模块专测 JSObject：创建（返回 JSObject / int 两版）、传参（吃 JSObject / int）、
/// 以及 JS→C# 接收（收 JSObject / int）。每个方向都和 int 对照组成对，差额即 JSObject 的跨边界成本。
/// </para>
/// <para>
/// [JSExport] 一律带 <c>Js_</c> 前缀（与 CrossBoundary 的 Cb_ 同规约），避免共享命名空间撞车。
/// </para>
/// </summary>
public static partial class JSBind_JSObject
{
    // ================= C# → JS：创建 =================
    [JSImport("joCreateObj", "bench_jsobject")]
    public static partial JSObject CreateObj();

    [JSImport("joCreateInt", "bench_jsobject")]
    public static partial int CreateInt();

    // ================= C# → JS：传参（复用已建好的句柄）=================
    [JSImport("joTakeObj", "bench_jsobject")]
    public static partial void TakeObj(JSObject obj);

    [JSImport("joTakeInt", "bench_jsobject")]
    public static partial void TakeInt(int id);

    // ================= JS → C#：接收 =================
    [JSExport]
    public static void Js_TakeObj(JSObject obj) { }

    [JSExport]
    public static void Js_TakeInt(int id) { }

    // ================= JS→C# 连打（JS 侧计时）=================
    [JSImport("joCallTakeObjN", "bench_jsobject")]
    public static partial string CallTakeObjN(int times);

    [JSImport("joCallTakeIntN", "bench_jsobject")]
    public static partial string CallTakeIntN(int times);

    [JSExport]
    public static void Js_GcCollect() => GC.Collect();
}
