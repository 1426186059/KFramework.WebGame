using System.Runtime.InteropServices.JavaScript;
using PixiGame;
using PixiDemo;

// 独立工程：不依赖 WebEngineCommon，C# 直接操作原生 PixiJS 对象模型。
var app = GameApp.Instance;
app.Register(new MainMenuGame());
app.Register(new ContraGame());
app.Register(new TankBattleGame());
app.Initialize("#game");
await app.WaitReadyAsync();   // PixiJS app.init() 完成、Stage 句柄 0 注册后才允许场景挂载对象
app.Start("main-menu");

EngineLoop.Start();

// 挂起 Main 永不返回 → .NET runtime 保持活跃，rAF 持续驱动 TickBridge
var suspend = new TaskCompletionSource();
await suspend.Task;

public static partial class EngineLoop
{
    [JSImport("engine.startLoop", "main.js")]
    internal static partial void Start();
}
