using System;
using System.Runtime.InteropServices.JavaScript;

/// <summary>
/// <b>Bench_CsToJs 模块专属</b>的互操作绑定（C# → JS 四类），对应 wwwroot/bench_cstojs.js。
/// <para>
/// 各测试模块的跨界入口一律自带一份、不共用：共用一个绑定类会让"改一个模块"牵动全部，
/// 而且 [JSExport] 的方法名在同一棵导出树里必须互不相同 —— 本模块只有 [JSImport]，故无需前缀。
/// </para>
/// </summary>
public static partial class JSBind_CsToJs
{
    /// <summary>调用空的 JS 函数 —— 纯跨界下限（无载荷）。</summary>
    [JSImport("noop", "bench_cstojs")]
    public static partial void Noop();

    [JSImport("echoInt", "bench_cstojs")]
    public static partial int EchoInt(int value);

    [JSImport("echoString", "bench_cstojs")]
    public static partial string EchoString(string text);

    /// <summary>
    /// C# 提供缓冲（MemoryView），JS 直接往里写 —— <b>零拷贝</b>路径。
    /// JS 侧拿到的是托管内存上的视图（不是 TypedArray），只有 set / copyTo / slice。
    /// </summary>
    [JSImport("fillSpan", "bench_cstojs")]
    public static partial int FillSpan([JSMarshalAs<JSType.MemoryView>] Span<byte> buffer, int length);

    /// <summary>
    /// 由 JS 新建数组并返回，C# 收成 byte[] —— <b>拷贝</b>路径（一次分配 + 过界时一次复制）。
    /// 与 <see cref="FillSpan"/> 同长度并列即可量化"零拷贝值多少"。
    /// </summary>
    [JSImport("makeArray", "bench_cstojs")]
    public static partial byte[] MakeArray(int length);

    /// <summary>
    /// 反向的一条 byte[] 路线：C# 把数组传给 JS（封送时复制一次，JS 拿到副本）。
    /// 与 <see cref="MakeArray"/> 正好相反，两档并列才看得出方向差异。
    /// </summary>
    [JSImport("sendBytes", "bench_cstojs")]
    public static partial int SendBytes(byte[] data);
}
