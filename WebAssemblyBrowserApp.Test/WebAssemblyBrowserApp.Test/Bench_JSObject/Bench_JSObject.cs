using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// 模块：专测 JSObject 的跨边界代价。
/// <para>
/// 起因：Texture2D.Handle 当前是 JSObject（WebGL 的 WebGLTexture 对象），而 WebGPU 后端用 int 句柄。
/// 两个问题要回答：
/// <list type="number">
///   <item><description>创建 / 传参时，JSObject 比 int 贵多少 —— 决定 WebGL 能不能、值不值得换成 int 句柄。</description></item>
///   <item><description>JSObject 会不会严重拖性能 —— 关键看「每帧新建」与「建一次复用」的差距：创建才是大头，复用几乎免费。</description></item>
/// </list>
/// 每条都配一个 int 对照组（同样的跨界次数、同样的参数位置），差额即 JSObject 特有的「托管分配 + 跨边界句柄表」成本。
/// </para>
/// </summary>
public sealed class Bench_JSObject : IBenchModule
{
    public string Name => "JSObject 跨边界代价（对照 int 句柄）";

    public string Summary =>
        "Texture2D.Handle 当前是 JSObject（WebGL 纹理对象），WebGPU 用 int。本页把它和 int 摆在一起比：" +
        "在「创建 / 复用传参 / JS→C# 接收 / 每帧新建 vs 建一次复用」四个场景测 ns/操作，" +
        "差额即 JSObject 的托管分配与句柄表成本；并对比最差用法与正常用法看创建是否真的大头。";

    public string Page => "jsobject";

    public Task<string> RunAsync()
    {
        // 预热（首次跨界含 JIT 与绑定解析）
        _ = JSBind_JSObject.CreateObj();
        _ = JSBind_JSObject.CreateInt();
        JSBind_JSObject.TakeObj(JSBind_JSObject.CreateObj());
        JSBind_JSObject.TakeInt(JSBind_JSObject.CreateInt());

        var all = new List<BenchRow>();
        var sb = new StringBuilder();

        // ================= ① 创建 =================
        var createRows = new List<BenchRow>
        {
            Row("创建 int 句柄（基线）", "[JSImport] JS 返回 int —— WebGPU createTexture 的同款路径",
                BenchKit.MeasureFixed(() => _ = JSBind_JSObject.CreateInt(), BenchKit.Times), 1),

            Row("创建 JSObject", "[JSImport] JS 返回一个对象、C# 收成 JSObject（含托管分配 + 句柄表登记）",
                BenchKit.MeasureFixed(() => _ = JSBind_JSObject.CreateObj(), BenchKit.Times), 1),
        };
        all.AddRange(createRows);
        sb.Append(BenchKit.Section("① 创建句柄：JSObject vs int",
            "相当于 createTexture。两条都只跨一次界，差额就是 JSObject 多出来的「托管 JSObject 分配 + JS 句柄表登记」。" +
            "次数固定 " + BenchKit.Fmt(BenchKit.Times) + " 次，耗时(ms) 可直接横比。",
            createRows,
            "看点：JSObject 创建比 int 慢多少 —— 这是「每帧都新建句柄」会付的单价。"));

        // ================= ② 复用句柄传参 =================
        JSObject reuseObj = JSBind_JSObject.CreateObj();
        int reuseId = JSBind_JSObject.CreateInt();

        var takeRows = new List<BenchRow>
        {
            Row("传参 int 句柄（基线）", "[JSImport] 把 int 句柄传给 JS —— 对应 bindTexture(target, id)",
                BenchKit.MeasureFixed(() => JSBind_JSObject.TakeInt(reuseId), BenchKit.Times), 1),

            Row("传参 JSObject", "[JSImport] 把 JSObject 句柄传给 JS —— 对应 bindTexture(target, texture)（句柄表解析）",
                BenchKit.MeasureFixed(() => JSBind_JSObject.TakeObj(reuseObj), BenchKit.Times), 1),
        };
        all.AddRange(takeRows);
        sb.Append(BenchKit.Section("② 复用句柄传参：JSObject vs int",
            "句柄已建好，只测「每次调 JS 时把它传过去」的代价（一帧里每个纹理 bind 一次）。" +
            "差额是 JSObject 过界时「按句柄查活对象表」与 int 直接拷贝的差距。",
            takeRows,
            "看点：复用场景下 JSObject 的传参成本 —— 若与 int 接近，说明「建一次、长期复用」并不会拖累。"));

        // ================= ③ JS → C# 接收 =================
        var recvRows = new List<BenchRow>
        {
            BenchKit.FromJs(() => JSBind_JSObject.CallTakeIntN(BenchKit.Times),
                "JS→C# 收 int 句柄", "[JSExport] 收一个 int（由 JS 侧计时）", BenchKit.Times),

            BenchKit.FromJs(() => JSBind_JSObject.CallTakeObjN(BenchKit.Times),
                "JS→C# 收 JSObject", "[JSExport] 收一个 JSObject（由 JS 侧计时，含跨界解析）", BenchKit.Times),
        };
        all.AddRange(recvRows);
        sb.Append(BenchKit.Section("③ JS→C# 接收：JSObject vs int",
            "反向：JS 主动把句柄塞进 C#。JS 侧连打并计时。",
            recvRows,
            "看点：双向都要收 JSObject 时，反向解析的额外成本。"));

        // ================= ④ 真实用法对比：最差（每帧新建） vs 正常（建一次复用）=================
        var usageRows = new List<BenchRow>
        {
            Row("int — 每帧新建 + 传参", "createInt + takeInt 放进同一次迭代（最差：每帧都造新句柄）",
                BenchKit.MeasureFixed(() => { int x = JSBind_JSObject.CreateInt(); JSBind_JSObject.TakeInt(x); }, BenchKit.Times), 1),

            Row("JSObject — 每帧新建 + 传参", "createObj + takeObj 放进同一次迭代（最差：每帧都 new JSObject）",
                BenchKit.MeasureFixed(() => { JSObject x = JSBind_JSObject.CreateObj(); JSBind_JSObject.TakeObj(x); }, BenchKit.Times), 1),

            Row("int — 建一次复用", "创建一次，循环里只 takeInt（正常：句柄常驻）",
                BenchKit.MeasureFixed(() => JSBind_JSObject.TakeInt(reuseId), BenchKit.Times), 1),

            Row("JSObject — 建一次复用", "创建一次，循环里只 takeObj（正常：句柄常驻）",
                BenchKit.MeasureFixed(() => JSBind_JSObject.TakeObj(reuseObj), BenchKit.Times), 1),
        };
        all.AddRange(usageRows);
        sb.Append(BenchKit.Section("④ 真实用法：每帧新建 vs 建一次复用",
            "把「创建」与「传参」合起来看真实代价。上半两行模拟最差写法（每次迭代都新建句柄），" +
            "下半两行模拟正常写法（句柄建一次后长期复用）。次数固定 " + BenchKit.Fmt(BenchKit.Times) + " 次。",
            usageRows,
            "看点：JSObject 会不会严重拖性能？看「每帧新建 JSObject」与「JSObject 建一次复用」的差 —— " +
            "若后者与 int 同量级、前者明显更贵，结论就是：复用无忧，创建才是唯一要规避的。"));

        // ================= 总表 =================
        sb.Append(BenchKit.Section("总表：四类场景的 JSObject 与 int（按 ns/操作 升序）",
            "把四组并回一张表，专看 JSObject 相对 int 的增量。各行总次数相同，故以 ns/操作 排序、也只比这一列。",
            all,
            "用法：判断「JSObject 贵在哪」看 ① 与 ④ 上半；判断「复用安不安全」看 ② 与 ④ 下半。"));

        return Task.FromResult(sb.ToString());
    }

    private static BenchRow Row(string name, string note, BenchTiming timing, long opsPerCall)
        => new(name, note, timing, opsPerCall);
}
