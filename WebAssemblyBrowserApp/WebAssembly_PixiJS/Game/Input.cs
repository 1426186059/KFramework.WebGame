using System.Runtime.InteropServices.JavaScript;

namespace PixiGame;

/// <summary>键盘 / 鼠标输入（JSImport 到 core/input.js）。</summary>
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

    [JSImport("input.init", "main.js")] public static partial void Init();

    /// <summary>当前帧是否按住该键。</summary>
    [JSImport("input.isKeyDown", "main.js")] public static partial bool IsKeyDown(string code);

    /// <summary>本帧是否刚按下（沿帧边界去重）。</summary>
    [JSImport("input.isKeyPressed", "main.js")] public static partial bool IsKeyPressed(string code);

    [JSImport("input.mouseX", "main.js")] public static partial float MouseX();
    [JSImport("input.mouseY", "main.js")] public static partial float MouseY();
    [JSImport("input.isMouseDown", "main.js")] public static partial bool IsMouseDown();
    [JSImport("input.isMousePressed", "main.js")] public static partial bool IsMousePressed();

    [JSImport("input.endFrame", "main.js")] public static partial void EndFrame();
}
