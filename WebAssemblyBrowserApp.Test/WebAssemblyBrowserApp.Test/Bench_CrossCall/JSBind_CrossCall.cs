using System;
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

    /// <summary>
    /// <c>[JSExport]</c>：供页面滑块 / 数值框调用，测指定的每帧次数。
    /// <para>注意：本工程 [JSExport] 源码生成不支持 <c>Task&lt;T&gt;</c> 返回值（与 JSImport 的 SYSLIB1072 同理，
    /// 仓库内也没有其它 [JSExport] 返回 Task&lt;T&gt; 的先例），故同步返回 string
    /// （与 JsToCs_String / Cb_String 同款）。调用前的"运行中…"提示由 JS 侧（bench_crosscall.js）在 await 前设置。</para>
    /// </summary>
    [JSExport]
    public static string CcRun(int count)
    {
        int n = Math.Clamp(count, 0, Bench_CrossCall.MaxN);
        return Bench_CrossCall.SingleCard(n);
    }

    /// <summary><c>[JSExport]</c>：供页面按钮调用，跑全套预设档位（大档较慢）。</summary>
    [JSExport]
    public static string CcRunAll()
    {
        return Bench_CrossCall.BuildTable("全套预设档位（0 → 1,000,000）", Bench_CrossCall.Presets, withConclusion: true);
    }
}
