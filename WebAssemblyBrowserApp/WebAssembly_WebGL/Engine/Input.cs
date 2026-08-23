using System.Runtime.InteropServices.JavaScript;

namespace WebAssemblyBrowserApp.Engine;

/// <summary>
/// 键盘 / 鼠标 / 触摸输入。
/// 键名使用 KeyboardEvent.code（如 "ArrowLeft"、"KeyA"、"Space"）。
/// </summary>
public static partial class Input
{
    public const string ArrowLeft = "ArrowLeft";
    public const string ArrowRight = "ArrowRight";
    public const string ArrowUp = "ArrowUp";
    public const string ArrowDown = "ArrowDown";
    public const string Space = "Space";
    public const string Enter = "Enter";
    public const string KeyA = "KeyA";
    public const string KeyD = "KeyD";
    public const string KeyW = "KeyW";
    public const string KeyS = "KeyS";
    public const string KeyR = "KeyR";
    public const string Escape = "Escape";
    public const string Digit1 = "Digit1";
    public const string Digit2 = "Digit2";

    [JSImport("input.init", "main.js")]
    public static partial void Init();

    /// <summary>按键当前是否按住。</summary>
    [JSImport("input.isKeyDown", "main.js")]
    public static partial bool IsKeyDown(string code);

    /// <summary>按键是否在本帧内刚按下（每帧末自动清除）。</summary>
    [JSImport("input.isKeyPressed", "main.js")]
    public static partial bool IsKeyPressed(string code);

    [JSImport("input.mouseX", "main.js")]
    public static partial double MouseX();

    [JSImport("input.mouseY", "main.js")]
    public static partial double MouseY();

    [JSImport("input.isMouseDown", "main.js")]
    public static partial bool IsMouseDown();

    [JSImport("input.isMousePressed", "main.js")]
    public static partial bool IsMousePressed();

    /// <summary>清除本帧的“刚按下”状态，由引擎在每帧末调用。</summary>
    [JSImport("input.endFrame", "main.js")]
    public static partial void EndFrame();
}
