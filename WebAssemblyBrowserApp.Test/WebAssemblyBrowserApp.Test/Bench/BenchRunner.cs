using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

/// <summary>
/// 对 JS 暴露的运行入口（<c>[JSExport]</c>）：总纲上的按钮经它驱动测试。
/// 结果仍由 <see cref="BenchKit"/> 写回页面的 #results / #status。
/// </summary>
public static partial class BenchRunner
{
    /// <summary>显示总纲（不跑任何测试）。</summary>
    [JSExport]
    public static void ShowMenu()
    {
        BenchKit.SetResults(BenchCatalog.RenderMenu());
        BenchKit.SetStatus("就绪 — 选择一个测试开始");
    }

    /// <summary>运行总纲中的第 index 个测试。</summary>
    [JSExport]
    public static async Task RunOne(int index)
    {
        IBenchModule? m = BenchCatalog.Find(index);
        if (m == null)
        {
            BenchKit.SetStatus("序号越界：" + index);
            return;
        }

        BenchKit.SetStatus("运行中：" + m.Name + " ...");
        await Task.Delay(16);           // 让浏览器先把状态渲染出来，再进入同步计时
        string html = await m.RunAsync();

        BenchKit.SetResults(BenchCatalog.RenderMenu() + html);
        BenchKit.SetStatus("完成 ✓ — " + m.Name);
    }

    /// <summary>按顺序运行全部测试。</summary>
    [JSExport]
    public static async Task RunAll()
    {
        var sb = new System.Text.StringBuilder(BenchCatalog.RenderMenu());

        for (int i = 0; i < BenchCatalog.Modules.Count; i++)
        {
            IBenchModule m = BenchCatalog.Modules[i];
            BenchKit.SetStatus("运行中（" + (i + 1) + "/" + BenchCatalog.Modules.Count + "）：" + m.Name + " ...");
            await Task.Delay(16);
            sb.Append(await m.RunAsync());
            BenchKit.SetResults(sb.ToString());   // 边跑边出结果，便于中途观察
        }

        BenchKit.SetStatus("全部完成 ✓");
    }
}
