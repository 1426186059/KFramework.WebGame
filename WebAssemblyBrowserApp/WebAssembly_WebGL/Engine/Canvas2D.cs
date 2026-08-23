using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

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
    public static partial void FillRect(double x, double y, double w, double h, string color);

    [JSImport("canvas.strokeRect", "main.js")]
    public static partial void StrokeRect(double x, double y, double w, double h, string color, double lineWidth);

    [JSImport("canvas.roundedRect", "main.js")]
    public static partial void RoundedRect(double x, double y, double w, double h, double r, string color);

    [JSImport("canvas.fillCircle", "main.js")]
    public static partial void FillCircle(double x, double y, double r, string color);

    [JSImport("canvas.fillText", "main.js")]
    public static partial void FillText(string text, double x, double y, string font, string color, string align);

    [JSImport("canvas.line", "main.js")]
    public static partial void Line(double x1, double y1, double x2, double y2, string color, double lineWidth);

    [JSImport("canvas.save", "main.js")]
    public static partial void Save();

    [JSImport("canvas.restore", "main.js")]
    public static partial void Restore();

    [JSImport("canvas.translate", "main.js")]
    public static partial void Translate(double x, double y);

    [JSImport("canvas.rotate", "main.js")]
    public static partial void Rotate(double radians);

    [JSImport("canvas.alpha", "main.js")]
    public static partial void Alpha(double a);

    [JSImport("canvas.shadow", "main.js")]
    public static partial void Shadow(string color, double blur);

    [JSImport("canvas.noShadow", "main.js")]
    public static partial void NoShadow();

    /// <summary>加载图片到精灵缓存（可选，演示游戏使用矢量绘制）。</summary>
    [JSImport("canvas.loadImage", "main.js")]
    public static partial Task<bool> LoadImage(string id, string url);

    [JSImport("canvas.drawImage", "main.js")]
    public static partial void DrawImage(string id, double dx, double dy, double dw, double dh);
}
