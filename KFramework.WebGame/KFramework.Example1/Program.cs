namespace MirGame;

internal static class Program
{
    // 例子1 现在是「引擎功能测试」容器：启动后进入测试总屏，
    // 各测试模块见 Tests/ 目录（一个模块一个文件夹）。
    private static async Task Main(string[] args)
    {
        var game = new Example1Game();
        await game.RunAsync();
    }
}
