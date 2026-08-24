using System.Runtime.InteropServices.JavaScript;

namespace WebAssemblyBrowserApp.Engine;

/// <summary>
/// Canvas 2D 渲染上下文封装。
/// 通过 [JSImport] 直接调用 main.js 中暴露的底层 canvas 命令。
/// </summary>
public static partial class Canvas2D
{
    public const int LogicalWidth = 800;
    public const int LogicalHeight = 600;

    /// <summary>初始化画布并做自适应缩放。</summary>
    [JSImport("engine.initCanvas", "main.js")]
    public static partial void Init(string selector, int width, int height);

    [JSImport("canvas.clear", "main.js")]
    public static partial void Clear(string color);

    [JSImport("canvas.fillRect", "main.js")]
    public static partial void FillRect(float x, float y, float w, float h, string color);

    [JSImport("canvas.strokeRect", "main.js")]
    public static partial void StrokeRect(float x, float y, float w, float h, string color, float lineWidth);

    [JSImport("canvas.roundedRect", "main.js")]
    public static partial void RoundedRect(float x, float y, float w, float h, float r, string color);

    [JSImport("canvas.fillCircle", "main.js")]
    public static partial void FillCircle(float x, float y, float r, string color);

    [JSImport("canvas.fillText", "main.js")]
    public static partial void FillText(string text, float x, float y, string font, string color, string align);

    [JSImport("canvas.line", "main.js")]
    public static partial void Line(float x1, float y1, float x2, float y2, string color, float lineWidth);

    [JSImport("canvas.save", "main.js")]
    public static partial void Save();

    [JSImport("canvas.restore", "main.js")]
    public static partial void Restore();

    [JSImport("canvas.translate", "main.js")]
    public static partial void Translate(float x, float y);

    [JSImport("canvas.rotate", "main.js")]
    public static partial void Rotate(float radians);

    [JSImport("canvas.alpha", "main.js")]
    public static partial void Alpha(float a);

    [JSImport("canvas.shadow", "main.js")]
    public static partial void Shadow(string color, float blur);

    [JSImport("canvas.noShadow", "main.js")]
    public static partial void NoShadow();

}
