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

    public const float Width = 960;
    public const float Height = 540;

    private readonly Dictionary<string, GameScene> _scenes = new();
    private readonly List<GameScene> _stack = new();

    /// <summary>上一帧到本帧的时间（秒），上限 0.05s 防止切页跳帧。</summary>
    public float DeltaTime { get; private set; }

    /// <summary>引擎累计运行时间（秒）。</summary>
    public float Time { get; private set; }

    public GameScene? Current => _stack.Count > 0 ? _stack[_stack.Count - 1] : null;
    public bool IsInitialized { get; set; }

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

    /// <summary>初始化 WebGPU 画布、输入与音频（与 WebGL 同步模式一致）。</summary>
    public GameEngine Initialize(string canvasSelector = "#game")
    {
        // gpu.init() 内部会异步请求 WebGPU 设备，不阻塞 C# 主线。
        // 设备就绪前 submit() 静默跳过，主循环照常跑（几帧后设备 ready 就渲染了）。
        WebGPU.Init();
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
        WebGPU.Flush();   // 整帧图元一次性提交 GPU（跨边界 O(1)）

        Input.EndFrame();
    }

    /// <summary>JS 探针：导出内部状态（调试用）。</summary>
    [JSExport]
    public static string __dbg_state()
    {
        var self = Instance;
        return $"isInit={self.IsInitialized};time={self.Time:0.000f};dt={self.DeltaTime:0.000f};stack={self._stack.Count};current={self.Current?.Name ?? "<null>"};scenes={string.Join(",", self._scenes.Keys)}";
    }

    // 逐段定位 Tick 卡死点（从 01~08，逐一调，看哪一个挂）
    [JSExport] public static float __probe01_echo(float x) => x;
    [JSExport] public static int    __probe02_isInit()  => Instance.IsInitialized ? 1 : 0;
    [JSExport] public static string __probe03_checkInput(string code) => Input.IsKeyDown(code) ? "t" : "f";
    [JSExport] public static int    __probe04_updateOnly(float dt) { if (Instance.IsInitialized && Instance.Current != null) Instance.Current.Update((float)dt); return 1; }
    [JSExport] public static int    __probe05_renderOnly() { if (Instance.IsInitialized && Instance.Current != null) Instance.Current.Render(); return 1; }
    [JSExport] public static int    __probe06_flushOnly()  { if (Instance.IsInitialized) WebGPU.Flush(); return 1; }
    [JSExport] public static int    __probe07_endFrameOnly() { if (Instance.IsInitialized) Input.EndFrame(); return 1; }
    [JSExport] public static int    __probe08_measureTextOnly() {
        // 单独测 MeasureText —— 避免 Render 中 FillText 的字符串交互
        if (Instance.IsInitialized) _ = WebGPU.MeasureText("HELLO", "bold 16px system-ui");
        return 1;
    }
}
