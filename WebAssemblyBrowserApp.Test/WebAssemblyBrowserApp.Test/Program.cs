using System;
using System.Threading.Tasks;

// 入口：先渲染总纲，之后由页面上的按钮驱动（经 BenchRunner 的 [JSExport]）。
// 这样新增测试不必改动本文件 —— 在 BenchCatalog 里登记即可。
BenchRunner.ShowMenu();

// 保持运行时存活（浏览器 WASM 单线程事件循环）
while (true)
{
    await Task.Delay(1000);
}
