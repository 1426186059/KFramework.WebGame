using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// 对 JS 暴露的运行入口（<c>[JSExport]</c>）：总纲上的按钮经它驱动测试。
/// 结果仍由 <see cref="BenchKit"/> 写回页面的 #results / #status。
/// </summary>
public static partial class BenchRunner
{
    /// <summary>
    /// 当前页面的文件名（由 main.js 从 <c>location.pathname</c> 取得，不含扩展名）。
    /// 它决定：进入某个测试的独立页面后，自动运行哪个模块；总纲页（index）则不自动运行。
    /// </summary>
    [JSImport("currentPage", "main.js")]
    private static partial string CurrentPage();

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
        await RunModule(m);
    }

    /// <summary>按顺序运行全部测试。</summary>
    [JSExport]
    public static async Task RunAll()
    {
        var sb = new StringBuilder(BenchCatalog.RenderMenu());

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

    /// <summary>
    /// 程序入口调用：处在某个测试的独立页面上就自动运行它；处在总纲页则只显示目录。
    /// </summary>
    public static async Task RunCurrentPageAsync()
    {
        IBenchModule? m = BenchCatalog.FindByPage(CurrentPage());
        if (m == null)
        {
            ShowMenu();
            return;
        }

        await RunModule(m);
    }

    private static async Task RunModule(IBenchModule m)
    {
        BenchKit.SetStatus("运行中：" + m.Name + " ...");
        await Task.Delay(16);           // 让浏览器先把状态渲染出来，再进入同步计时
        string html = await m.RunAsync();

        BenchKit.SetResults(html);
        BenchKit.SetStatus("完成 ✓ — " + m.Name);
    }
}
