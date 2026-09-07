using System;
using System.Threading.Tasks;

Console.WriteLine("ReflectionBenchmark start");

if (args.Length == 1 && args[0] == "start")
    await ReflectionBenchmark.RunAsync();

// 保持运行时存活（浏览器 WASM 单线程事件循环）
while (true)
{
    await Task.Delay(1000);
}
