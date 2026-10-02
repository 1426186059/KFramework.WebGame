using System.Threading.Tasks;

// 入口：行为由"当前所在的 HTML 页面"决定 ——
//   处在某个测试的独立页面（如 reflection.html）→ 自动运行该模块；
//   处在 index.html（总纲页）→ 只显示目录，等用户点选。
// 页面名由 main.js 的 bench.currentPage() 从 location.pathname 取得。
await BenchRunner.RunCurrentPageAsync();

// 保持运行时存活（浏览器 WASM 单线程事件循环）
while (true)
{
    await Task.Delay(1000);
}
