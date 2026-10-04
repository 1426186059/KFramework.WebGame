using System.Runtime.InteropServices.JavaScript;

/// <summary>
/// <b>Bench_CrossCall 模块专属</b>的互操作绑定，对应 wwwroot/bench_crosscall.js。
/// <para>
/// 本模块只测<b>频率</b>：每帧连续做 N 次跨界调用，量 N 与每帧耗时的关系。
/// 调用体刻意是<b>空的</b>（<see cref="Noop"/>），不带任何负载 ——
/// 这样测出来的就是"过一次界"这个动作本身的固定成本，
/// 与 Bench_CrossBoundary（比方向、比数据类型、每次都带负载）正好互补。
/// </para>
/// <para>
/// 为什么值得单独测：Bench_ZeroCopy 实测一次空跨界 ≈ 3333 ns，
/// 而拷贝 4 MB 才 287 μs —— 换算下来，<b>一次空跨界 ≈ 拷贝 47 KB</b>。
/// 引擎每帧传的数据往往只有几十 KB（拷贝成本几 μs），却可能跨几十上百次界（上百 μs）。
/// 所以"每帧跨了多少次界"常常比"每帧拷了多少字节"更决定帧率。
/// </para>
/// </summary>
public static partial class JSBind_CrossCall
{
    /// <summary>
    /// 空跨界调用：什么都不做，立刻返回。
    /// 测的就是"过一次界"这个动作本身，不含任何数据处理。
    /// </summary>
    [JSImport("noop", "bench_crosscall")]
    public static partial int Noop();
}
