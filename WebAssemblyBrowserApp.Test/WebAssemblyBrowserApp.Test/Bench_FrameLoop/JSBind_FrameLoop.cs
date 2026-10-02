using System;
using System.Runtime.InteropServices.JavaScript;

/// <summary>
/// <b>Bench_FrameLoop 模块专属</b>的互操作绑定，对应 wwwroot/bench_frameloop.js。
/// 模拟引擎宿主那行 <c>host.Frame(ts, takeFrameData())</c> 的两种写法。
/// <para>
/// 两条路线都由 JS 侧整体计时（一次 fn 调用 = 一帧的工作量）：
/// A 一趟推由 JS 一次调用完成；B 的【第二趟】发生在本类的导出方法<b>内部</b>（C# 再回头调 JS），
/// 而 JS 侧照样只调一次函数，所以计到的是一帧的完整成本，两条口径一致。
/// </para>
/// </summary>
public static partial class JSBind_FrameLoop
{
    /// <summary>每帧复用的接收缓冲（不能每帧 new —— 那样测的是分配，不是跨界）。</summary>
    private static readonly byte[] FrameBuffer = new byte[4096];

    private static int _sink;           // 接住结果，免得被当成无用代码消除

    private static bool _verify;        // 只在抽验时打开：校验成本不该算进计时
    private static bool _pushOk = true;
    private static bool _pullOk = true;

    // ================= [JSExport]：两条路线的 C# 侧入口 =================

    /// <summary>
    /// 【A 一趟推】JS 把一帧的输入事件字节流随帧回调一起送进来：只过界一次。
    /// </summary>
    [JSExport]
    public static int Fl_Push(double timestampMs, byte[] events)
    {
        if (_verify && events.Length > 0)
            _pushOk = events[0] == 0 && events[^1] == (byte)((events.Length - 1) & 0xFF);

        _sink = events.Length;
        return events.Length;
    }

    /// <summary>
    /// 【B 两趟拉】JS 只推帧（带 timestamp、<b>不带数据</b>），C# 再回头调 JS 取 ——
    /// 第二趟是 C#→JS，走 MemoryView 零拷贝（C# 把缓冲借给 JS 直写）。
    /// <para>
    /// 第一趟照样带 <paramref name="timestampMs"/>：A 的 <c>Frame(ts, data)</c> 与 B 的 <c>Frame(ts)</c>
    /// 只差"带不带数据"，这才是要比的那一个变量 —— 若 B 连 ts 都不带，A 就白多付一个参数的封送。
    /// </para>
    /// </summary>
    [JSExport]
    public static int Fl_PullSpan(double timestampMs, int len)
    {
        int n = PullFill(FrameBuffer.AsSpan(0, len), len);   // 第二趟：C# → JS，零拷贝

        if (_verify && n > 0)
            _pullOk = FrameBuffer[0] == 0 && FrameBuffer[n - 1] == (byte)((n - 1) & 0xFF);

        _sink = n;
        return n;
    }

    /// <summary>无参数返回常量 int —— 一次跨界的单价（本模块的参照行，自己一份，不借别处的）。</summary>
    [JSExport]
    public static int Fl_Tick() => 0;

    [JSExport]
    public static void Fl_GcCollect() => GC.Collect();

    /// <summary>开启抽验（置位校验开关并清零结果）。抽验结束后须调 <see cref="Fl_VerifyEnd"/>。</summary>
    [JSExport]
    public static void Fl_VerifyBegin()
    {
        _verify = true;
        _pushOk = true;
        _pullOk = true;
    }

    /// <summary>关闭抽验并返回结果位：bit0 = 一趟推收到完整字节，bit1 = 两趟拉写进了复用缓冲。</summary>
    [JSExport]
    public static int Fl_VerifyEnd()
    {
        _verify = false;
        return (_pushOk ? 1 : 0) | (_pullOk ? 2 : 0);
    }

    // ================= [JSImport] =================

    /// <summary>B 的【第二趟】：C# 把缓冲借给 JS 直写（零拷贝）。</summary>
    [JSImport("pullFill", "bench_frameloop")]
    public static partial int PullFill([JSMarshalAs<JSType.MemoryView>] Span<byte> buffer, int length);

    /// <summary>参照行：连打 <see cref="Fl_Tick"/>（一次不带载荷的跨界）。</summary>
    [JSImport("callFrameTickN", "bench_frameloop")]
    public static partial string CallFrameTickN(int frames);

    /// <summary>【A】连打 <see cref="Fl_Push"/>。</summary>
    [JSImport("callFramePushN", "bench_frameloop")]
    public static partial string CallFramePushN(int frames, int byteLength);

    /// <summary>【B】连打 <see cref="Fl_PullSpan"/>（第二趟在 C# 内部发生）。</summary>
    [JSImport("callFramePullSpanN", "bench_frameloop")]
    public static partial string CallFramePullSpanN(int frames, int byteLength);

    /// <summary>
    /// 抽验（走<b>真实过界路径</b>）：由 JS 依次调用两条路线各一次，再把 C# 侧的校验结果读回来。
    /// 之所以不直接在 C# 里调自己 —— 那样跳过了封送，验的就不是"过界"这件事。
    /// </summary>
    [JSImport("verifyFrameOnce", "bench_frameloop")]
    public static partial int VerifyFrameOnce(int byteLength);
}
