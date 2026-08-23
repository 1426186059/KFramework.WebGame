#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;

namespace WebAssemblyBrowserApp.Engine;

/// <summary>
/// 游戏引擎核心：场景管理、主循环调度、时间。
/// 主循环由 JS 的 requestAnimationFrame 驱动，每帧调用 GameBridge.Tick。
/// 渲染通过 WebGPU 层（gpu.*）在 GPU 上完成。
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
        Console.WriteLine("[dbg] S1: _stack.Clear →");
        _stack.Clear();
        Console.WriteLine("[dbg] S2: scene.Enter → (name=" + name + ")");
        _scenes[name].Enter();
        Console.WriteLine("[dbg] S3: scene.Enter ←");
        _stack.Add(_scenes[name]);
        Console.WriteLine("[dbg] S4: done, stack.Count=" + _stack.Count);
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

    /// <summary>初始化 WebGPU 画布、输入与音频（与 WebGL 同步模式一致）。</summary>
    public GameEngine Initialize(string canvasSelector = "#game")
    {
        // gpu.init() 内部会异步请求 WebGPU 设备，不阻塞 C# 主线。
        // 设备就绪前 submit() 静默跳过，主循环照常跑（几帧后设备 ready 就渲染了）。
        Console.WriteLine("[dbg] 01: WebGPU.Init →");
        WebGPU.Init();
        Console.WriteLine("[dbg] 02: WebGPU.Init ←");
        Console.WriteLine("[dbg] 03: Input.Init →");
        Input.Init();
        Console.WriteLine("[dbg] 04: Input.Init ←");
        Console.WriteLine("[dbg] 05: Audio.Init →");
        Audio.Init();
        Console.WriteLine("[dbg] 06: Audio.Init ←");
        IsInitialized = true;
        return this;
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

    /// <summary>JS 探针：导出内部状态（调试用）。</summary>
    [JSExport]
    public static string __dbg_state()
    {
        var self = Instance;
        return $"isInit={self.IsInitialized};time={self.Time:0.000};dt={self.DeltaTime:0.000};stack={self._stack.Count};current={self.Current?.Name ?? "<null>"};scenes={string.Join(",", self._scenes.Keys)}";
    }
}
