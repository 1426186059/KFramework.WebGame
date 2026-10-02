using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// 基准测试总纲：登记所有测试模块，并渲染成可点选的条目。
/// <para>
/// 新增一个测试只需两步：① 写一个实现 <see cref="IBenchModule"/> 的独立 .cs；
/// ② 在 <see cref="Modules"/> 里登记一行。总纲与运行入口都不必改动。
/// </para>
/// </summary>
public static class BenchCatalog
{
    public static readonly IReadOnlyList<IBenchModule> Modules = new IBenchModule[]
    {
        // 反射合成一个模块：它内部会分场景输出多张结果表，不必拆成多个条目
        new Bench_Reflection(),
        // 跨界方向各一个模块，重点覆盖 int / string / byte[]，byte[] 按长度分档，均以 C#→C# 为基线
        new Bench_JsToCs(),
        new Bench_CsToJs(),
        // 横向对比：同一操作在 基线 / C#→JS / JS→C# 三种走法下的耗时
        new Bench_CrossBoundary(),
    };

    /// <summary>按序号取模块，越界返回 null。</summary>
    public static IBenchModule? Find(int index)
        => index >= 0 && index < Modules.Count ? Modules[index] : null;

    /// <summary>渲染总纲（条目列表）。每个条目是一个按钮，点击由 JS 侧转交 C# 运行。</summary>
    public static string RenderMenu()
    {
        var sb = new StringBuilder();
        sb.Append("<div class='card'><h3>测试总纲</h3>");
        sb.Append("<p class='muted'>点条目单独运行，或用上方按钮运行全部。");
        sb.Append("每个场景先 warmup，再自适应放大迭代到约 60ms 后计时；ns/操作 为单次开销，相对 x 以每组第一行为基线。</p>");
        sb.Append("<ol class='menu'>");
        for (int i = 0; i < Modules.Count; i++)
        {
            IBenchModule m = Modules[i];
            sb.Append("<li><button class='run' data-bench='").Append(i.ToString()).Append("'>运行</button>")
              .Append("<b>").Append(BenchKit.Escape(m.Name)).Append("</b>")
              .Append("<div class='muted'>").Append(BenchKit.Escape(m.Summary)).Append("</div></li>");
        }
        sb.Append("</ol></div>");
        return sb.ToString();
    }
}
