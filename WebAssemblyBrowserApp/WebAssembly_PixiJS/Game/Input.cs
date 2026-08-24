using System.Runtime.InteropServices.JavaScript;

namespace PixiGame;

/// <summary>
/// 键盘 / 鼠标输入。JSImport 到 Pixi 桥 pixiApi.input*：
/// 鼠标 / 触摸走 Pixi 的 EventSystem（FederatedPointerEvent）；
/// 键盘 Pixi 无封装，用 DOM 键盘事件（统一在 Pixi 桥内管理）。
/// </summary>
public static partial class Input
{
    // 常用键码常量
    public const string Enter = "Enter";
    public const string Space = "Space";
    public const string Escape = "Escape";
    public const string ArrowLeft = "ArrowLeft";
    public const string ArrowRight = "ArrowRight";
    public const string ArrowUp = "ArrowUp";
    public const string ArrowDown = "ArrowDown";
    public const string KeyW = "KeyW";
    public const string KeyA = "KeyA";
    public const string KeyS = "KeyS";
    public const string KeyD = "KeyD";
    public const string KeyJ = "KeyJ";
    public const string KeyZ = "KeyZ";
    public const string KeyX = "KeyX";
    public const string ControlLeft = "ControlLeft";

    [JSImport("pixiApi.inputInit", "main.js")] public static partial void Init();

    /// <summary>当前帧是否按住该键。</summary>
    [JSImport("pixiApi.inputIsKeyDown", "main.js")] public static partial bool IsKeyDown(string code);

    /// <summary>本帧是否刚按下（沿帧边界去重）。</summary>
    [JSImport("pixiApi.inputIsKeyPressed", "main.js")] public static partial bool IsKeyPressed(string code);

    [JSImport("pixiApi.inputMouseX", "main.js")] public static partial float MouseX();
    [JSImport("pixiApi.inputMouseY", "main.js")] public static partial float MouseY();
    [JSImport("pixiApi.inputIsMouseDown", "main.js")] public static partial bool IsMouseDown();
    [JSImport("pixiApi.inputIsMousePressed", "main.js")] public static partial bool IsMousePressed();

    [JSImport("pixiApi.inputEndFrame", "main.js")] public static partial void EndFrame();
}
