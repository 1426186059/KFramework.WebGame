using KFramework.MonoGame;
using WebGame.Mir2.MonoGame.Client;

internal static class MainProgram
{
    // 例子1 现在是「引擎功能测试」容器：启动后进入测试总屏，
    // 各测试模块见 Tests/ 目录（一个模块一个文件夹）。
    private static async Task Main(string[] args)
    {
        GraphicsDevice device = await GraphicsDevice.CreateAsync("#game", antialias: true, preferWebGpu: true);
        var game = new MirGame(device);
        await game.RunAsync();
    }
}
