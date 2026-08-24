using System;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;
using WebAssemblyBrowserApp.Engine;
using WebAssemblyBrowserApp.Games;

public class Program
{
    public static async Task Main()
    {
        Console.WriteLine("[Engine] .NET 10 WebAssembly 2D 游戏引擎启动中…");

        GameEngine.Instance
            .RegisterScene(new MainMenuScene())
            .RegisterScene(new BreakoutScene())
            .RegisterScene(new TankScene())
            .RegisterScene(new XiYouJiScene())
            .Initialize("#game")
            .Start("main-menu");

        // 通知 JS 启动 requestAnimationFrame 主循环
        EngineLoop.Start();

        // 保持运行时存活：主循环由 JS 每帧调用 GameBridge.Tick 驱动
        await new TaskCompletionSource().Task;
    }
}

public static partial class GameBridge
{
    [JSExport]
    public static void Tick(float dt) => GameEngine.Instance.Tick(dt);
}

/// <summary>引擎启动桥：通知 JS 启动主循环。</summary>
public static partial class EngineLoop
{
    [JSImport("engine.startLoop", "main.js")]
    internal static partial void Start();
}
