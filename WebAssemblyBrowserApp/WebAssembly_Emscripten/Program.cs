using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

Console.WriteLine("[App] 启动：验证 Emscripten C++ 原生互操作 + WebGL2");

try
{
    int v = Native.NativeTest();
    Console.WriteLine($"[App] native_test() = {v} （期望 42）");
}
catch (Exception ex)
{
    Console.WriteLine($"[App] native_test 失败: {ex.GetType().Name}: {ex.Message}");
}

try
{
    int ok = Native.GfxInit("#game");
    Console.WriteLine($"[App] gfx_init() = {ok} （1 = WebGL2 就绪）");

    if (ok == 1)
    {
        Native.GfxClear(0.05f, 0.13f, 0.26f, 1f);
        Console.WriteLine("[App] gfx_clear() 已调用");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[App] gfx_init 失败: {ex.GetType().Name}: {ex.Message}");
}

// 保持运行时存活，便于查看控制台输出
await Task.Delay(TimeSpan.FromMinutes(5));

internal static class Native
{
    [DllImport("__Internal", EntryPoint = "native_test")]
    internal static extern int NativeTest();

    [DllImport("__Internal", EntryPoint = "gfx_init")]
    internal static extern int GfxInit([MarshalAs(UnmanagedType.LPUTF8Str)] string selector);

    [DllImport("__Internal", EntryPoint = "gfx_clear")]
    internal static extern void GfxClear(float r, float g, float b, float a);
}
