using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// 基准测试总纲：登记所有测试模块，并渲染成可点选的条目。
/// <para>
/// 每个模块对应一个<b>独立 HTML 页面</b>（文件名取自 <see cref="IBenchModule.Page"/>），
/// 总纲负责列出条目并给出跳转链接；进入某个页面后会自动运行对应的模块。
/// </para>
/// <para>
/// 新增一个测试只需三步：① 写一个实现 <see cref="IBenchModule"/> 的独立 .cs（放在自己的文件夹里）；
/// ② 在 <see cref="Modules"/> 里登记一行；③ 在 wwwroot 下加一个同名 HTML。
/// </para>
/// </summary>
public static class BenchCatalog
{
    public static readonly IReadOnlyList<IBenchModule> Modules = new IBenchModule[]
    {
        // 反射合成一个模块：它内部会分场景输出多张结果表，不必拆成多个条目
        new Bench_Reflection(),
        // 跨界方向各一个模块：都按 int / string / byte[] / MemoryView 四类分组，后两类按长度分档，均以 C#→C# 为基线
        new Bench_JsToCs(),
        new Bench_CsToJs(),
        // 横向对比：同一操作在 基线 / C#→JS / JS→C# 三种走法下的耗时（同样四类分组）
        new Bench_CrossBoundary(),
        // 帧循环的取舍：一帧的输入事件是"随帧回调一起推进来"还是"C# 回头去取"（引擎宿主那行的依据）
        new Bench_FrameLoop(),
        // MemoryView 在 JS 侧究竟有哪几种写法、每种是不是真动到托管内存 —— 不计时，只做行为探测。
        // 起因是"slice() 拿到的是零拷贝视图"这个猜测：副本还是视图，跑一遍就知道。
        new Bench_MemoryView(),
        // 另一条零拷贝路线：GCHandle 钉住数组 + 把裸地址交给 JS 建 TypedArray。
        // 前提是 JS 拿得到 WASM 的 memory.buffer —— .NET 不像 Emscripten 那样把堆挂全局，实测到底行不行。
        new Bench_HeapView(),
        // 按 dotnet/runtime 源码找出来的一整套【公开】内存 API：
        // localHeapViewU8() 系列、setHeapU8/getHeapU8 系列、以及 create() 返回值上挂着的 Module。
        // ⑨ 那句"全局扫不到裸堆"其实只是候选名单没扫到 getDotnetRuntime —— 本模块把它们逐个实测，
        // 并回答 HeapView 那条 _unsafe_create_view 绕道能不能换成公开 API。
        new Bench_RuntimeApi(),
        // 前面几页回答的是"能不能零拷贝"，这一页回答"值不值"：
        // 把那一次 memcpy 的价钱量成数字，按 4KB→4MB 分档看它是固定成本还是随体积线性增长。
        new Bench_ZeroCopy(),
    };

    /// <summary>按序号取模块，越界返回 null。</summary>
    public static IBenchModule? Find(int index)
        => index >= 0 && index < Modules.Count ? Modules[index] : null;

    /// <summary>
    /// 按独立页面的文件名取模块（忽略大小写、允许带 .html 后缀）。
    /// 找不到返回 null —— 总纲页（index）就属于这种情况，此时只显示目录、不自动运行。
    /// </summary>
    public static IBenchModule? FindByPage(string? page)
    {
        if (string.IsNullOrWhiteSpace(page)) return null;

        string key = page.Trim();
        if (key.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            key = key[..^5];

        foreach (IBenchModule m in Modules)
        {
            if (string.Equals(m.Page, key, StringComparison.OrdinalIgnoreCase)) return m;
        }
        return null;
    }

    /// <summary>渲染总纲（条目列表）。每条给出：就地运行按钮 + 进入独立页面的链接。</summary>
    public static string RenderMenu()
    {
        var sb = new StringBuilder();
        sb.Append("<div class='card'><h3>测试总纲</h3>");
        sb.Append("<p class='muted'>每个测试都有独立页面，点标题进入；也可直接点「运行」在本页查看结果。");
        sb.Append("<b>同一组里所有行跑相同的固定次数</b>（先 warmup 再计时，次数不自适应）：" +
                  "标量组 " + BenchKit.Fmt(BenchKit.Times) + " 次、字节块组 " + BenchKit.Fmt(BenchKit.TimesBytes) + " 次。" +
                  "所以组内的「耗时(ms)」可直接横比，跨组请比「ns/操作」（它是唯一通用判据，行也按它升序排）。</p>");
        sb.Append("<p class='muted'>抗干扰：每轮开始前<b>强制 GC.Collect()</b>，跑 " + BenchKit.Rounds +
                  " 轮取<b>最快的一轮</b>（干扰只会让某轮变慢，不会让它变快）。" +
                  "「抖动(ms)」= 最慢轮 − 最快轮；「GC(次)」= 计时窗口里实际发生的回收次数（橙色即被拖过，该行数字别当真）。</p>");
        sb.Append("<ol class='menu'>");

        for (int i = 0; i < Modules.Count; i++)
        {
            IBenchModule m = Modules[i];
            sb.Append("<li>")
              .Append("<button class='run' data-bench='").Append(i.ToString()).Append("'>运行</button>")
              .Append("<a href='").Append(BenchKit.Escape(m.Page)).Append(".html'><b>")
              .Append(BenchKit.Escape(m.Name)).Append("</b></a>")
              .Append("<div class='muted'>").Append(BenchKit.Escape(m.Summary)).Append("</div>")
              .Append("</li>");
        }

        sb.Append("</ol></div>");
        return sb.ToString();
    }
}
