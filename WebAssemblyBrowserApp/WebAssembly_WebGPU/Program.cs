using System;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;
using WebAssemblyBrowserApp.Engine;
using WebAssemblyBrowserApp.Games;

Console.WriteLine("[Engine] .NET 10 WebAssembly + WebGPU 2D 游戏引擎启动中…");

GameEngine.Instance
    .RegisterScene(new BreakoutScene())
    .Initialize("#game");   // 触发 JS 异步初始化 WebGPU 设备

// 设备就绪后，JS 会回调 GameEngine.EngineReady()（启动场景 + rAF 主循环）

// 保持运行时存活：主循环由 JS 每帧调用 GameBridge.Tick 驱动
await new TaskCompletionSource().Task;

/// <summary>由 JS 每帧调用的导出桥（全局命名空间，导出名为 GameBridge.Tick）。</summary>
public static partial class GameBridge
{
    [JSExport]
    public static void Tick(double dt) => GameEngine.Instance.Tick(dt);
}

/// <summary>引擎启动桥：通知 JS 启动主循环。</summary>
public static partial class EngineLoop
{
    [JSImport("engine.startLoop", "main.js")]
    internal static partial void Start();
}
