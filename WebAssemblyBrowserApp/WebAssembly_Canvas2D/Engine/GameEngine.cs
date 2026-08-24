#nullable enable
using System;
using System.Collections.Generic;

namespace WebAssemblyBrowserApp.Engine;

/// <summary>
/// 游戏引擎核心：场景管理、主循环调度、时间。
/// 主循环由 JS 的 requestAnimationFrame 驱动，每帧调用 GameBridge.Tick。
/// </summary>
public sealed class GameEngine
{
    public static GameEngine Instance { get; } = new();

    public const float Width = Canvas2D.LogicalWidth;
    public const float Height = Canvas2D.LogicalHeight;

    private readonly Dictionary<string, GameScene> _scenes = new();
    private readonly List<GameScene> _stack = new();

    /// <summary>上一帧到本帧的时间（秒），上限 0.05s 防止切页跳帧。</summary>
    public float DeltaTime { get; private set; }

    /// <summary>引擎累计运行时间（秒）。</summary>
    public float Time { get; private set; }

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

    /// <summary>初始化画布、输入与音频。</summary>
    public GameEngine Initialize(string canvasSelector = "#game")
    {
        Canvas2D.Init(canvasSelector, Canvas2D.LogicalWidth, Canvas2D.LogicalHeight);
        Input.Init();
        Audio.Init();
        IsInitialized = true;
        return this;
    }

    /// <summary>由 JS 每帧调用一次。</summary>
    public void Tick(float rawDt)
    {
        if (!IsInitialized) return;
        DeltaTime = (float)MathF.Min(MathF.Max(rawDt, 0.0f), 0.05f);
        Time += DeltaTime;

        Current?.Update(DeltaTime);
        Current?.Render();

        Input.EndFrame();
    }
}
