using System.Runtime.InteropServices.JavaScript;
using PixiJS;

namespace PixiGame;

/// <summary>
/// 独立小引擎核心：场景管理、主循环调度、时间。
/// 主循环由 JS requestAnimationFrame 驱动，每帧调用 [JSExport] TickBridge。
/// </summary>
public sealed partial class GameApp
{
    public static GameApp Instance { get; } = new();

    private readonly Dictionary<string, GameScene> _scenes = new();
    private GameScene? _current;

    public float DeltaTime { get; private set; }
    public float Time { get; private set; }
    public bool IsInitialized { get; private set; }
    public GameScene? Current => _current;

    private GameApp() { }

    public GameApp Register(GameScene scene)
    {
        _scenes[scene.Name] = scene;
        return this;
    }

    public GameApp Start(string name)
    {
        if (_current is not null) _current.Exit();
        _current = _scenes[name];
        _current.Enter();
        return this;
    }

    public void Initialize(string selector = "#game")
    {
        PixiApp.Instance.Initialize(selector);
        Input.Init();
        Audio.Init();
        IsInitialized = true;
    }

    /// <summary>等待 PixiJS app 初始化完成（Stage/句柄 0 注册完毕）后再启动场景。</summary>
    public async Task WaitReadyAsync()
    {
        await PixiApi.WaitReady();
        // 等待帧循环已启动（确保 Enter 时对 stage 的改动不会被首帧前的清空覆盖）
    }

    public void Tick(float dt)
    {
        if (!IsInitialized) return;
        DeltaTime = MathF.Min(MathF.Max(dt, 0f), 0.05f);
        Time += DeltaTime;

        _current?.Update(DeltaTime);
        _current?.Render();
        PixiApp.Instance.Render();   // 一次 app.render()

        Input.EndFrame();
    }

    /// <summary>由 JS 每帧调用。</summary>
    [JSExport]
    public static void TickBridge(float dt) => Instance.Tick(dt);

    /// <summary>供 JS 在 URL 参数 ?scene=X 时绕过主菜单直接进入指定场景（用于定位"黑屏"是切换逻辑还是目标场景本身）。</summary>
    [JSExport]
    public static void StartStatic(string name) => Instance.Start(name);

    [JSExport]
    public static string DbgState()
    {
        var a = GameApp.Instance;
        return $"init={a.IsInitialized};time={a.Time:0.00f};dt={a.DeltaTime:0.000f};scene={a.Current?.Name ?? "<null>"};scenes={string.Join(",", a._scenes.Keys)}";
    }
}
