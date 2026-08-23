using System;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

Console.WriteLine("[dbg-min] 01: top-of-program");

try
{
    Console.WriteLine("[dbg-min] 02: before Task.Delay(1ms)");
    await Task.Delay(1);
    Console.WriteLine("[dbg-min] 03: after Task.Delay(1ms)");
}
catch (Exception e)
{
    Console.WriteLine("[dbg-min] EX: " + e.Message);
}

Console.WriteLine("[dbg-min] 04: before TCS");
await new TaskCompletionSource().Task;
Console.WriteLine("[dbg-min] 05: after TCS (unreachable)");

/// <summary>JS 探针：导出一个 Ping 函数用于确认 C# 侧完全 alive。</summary>
public static partial class Diagnostics
{
    [JSExport]
    public static string Ping(string input) => "pong:" + input;
}
