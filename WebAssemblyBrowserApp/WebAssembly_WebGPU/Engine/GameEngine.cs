#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;

namespace WebAssemblyBrowserApp.Engine;

/// <summary>
/// 游戏引擎核心：场景管理、主循环调度、时间。
/// 主循环由 JS 的 requestAnimationFrame 驱动，每帧调用 GameBridge.Tick。
/// </summary>
public sealed partial class GameEngine
{
    public static GameEngine Instance { get; } = new();

    public const double Width = 960;
    public const double Height = 540;

    private readonly Dictionary<string, GameScene> _scenes = new();
    private readonly List<GameScene> _stack = new();

    /// <summary>上一帧到本帧的时间（秒），上限 0.05s 防止切页跳帧。</summary>
    public float DeltaTime { get; private set; }

    /// <summary>引擎累计运行时间（秒）。</summary>
    public double Time { get; private set; }

    public GameScene? Current => _stack.Count > 0 ? _stack[_stack.Count - 1] : null;
    public bool IsInitialized { get; private set; }

    private GameEngine() { }

    public GameEngine RegisterScene(GameScene scene)
    {
        _scenes[scene.Name] = scene;
        return this;
    }

    public GameEngine Start(string name)
    {
        _stack.Clear();
        _scenes[name].Enter();
        _stack.Add(_scenes[name]);
        return this;
    }

    public void Push(string name)
    {
        _scenes[name].Enter();
        _stack.Add(_scenes[name]);
    }

    public void Pop()
    {
        if (_stack.Count <= 1) return;
        var top = _stack[_stack.Count - 1];
        top.Exit();
        _stack.RemoveAt(_stack.Count - 1);
    }

    /// <summary>触发 WebGPU 设备初始化（异步，由 JS 在就绪后回调 CompleteInit）。</summary>
    public GameEngine Initialize(string canvasSelector = "#game")
    {
        Input.Init();
        Audio.Init();
        WebGPU.Init();
        return this;
    }

    /// <summary>JS 在 WebGPU 设备就绪后调用：真正开启渲染。</summary>
    [JSExport]
    public static void EngineReady()
    {
        Instance.IsInitialized = true;
        EngineLoop.Start();
    }

    /// <summary>由 JS 每帧调用一次。</summary>
    public void Tick(double rawDt)
    {
        if (!IsInitialized) return;
        DeltaTime = (float)Math.Min(Math.Max(rawDt, 0.0), 0.05);
        Time += DeltaTime;

        Current?.Update(DeltaTime);
        Current?.Render();
        WebGPU.Flush();   // 整帧图元一次性提交 GPU（跨边界 O(1)）

        Input.EndFrame();
    }
}
