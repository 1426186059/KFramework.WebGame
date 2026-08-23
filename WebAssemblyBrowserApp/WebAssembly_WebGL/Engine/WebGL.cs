using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

namespace WebAssemblyBrowserApp.Engine;

/// <summary>
/// 纯 WebGL 渲染层。底层走 gl.*（着色器 + 顶点缓冲），所有图元都在 GPU 上绘制。
/// API 与旧 Canvas2D 保持一致，因此上层游戏逻辑无需改动。
/// - 矩形 / 圆角矩形 / 圆：由 WebGL 内联几何着色器绘制
/// - 文本：先用离屏 2D canvas 烘焙成纹理，再作为带 alpha 的四边形贴图绘制
/// </summary>
public static partial class WebGL
{
    public const int LogicalWidth = 800;
    public const int LogicalHeight = 600;

    /// <summary>初始化 WebGL 上下文并编译着色器（含文字图集）。</summary>
    [JSImport("gl.init", "main.js")]
    public static partial void Init(string selector, int width, int height);

    /// <summary>绑定好视口，清空颜色缓冲。传入背景色。</summary>
    [JSImport("gl.clear", "main.js")]
    public static partial void Clear(string color);

    [JSImport("gl.fillRect", "main.js")]
    public static partial void FillRect(double x, double y, double w, double h, string color);

    [JSImport("gl.strokeRect", "main.js")]
    public static partial void StrokeRect(double x, double y, double w, double h, string color, double lineWidth);

    [JSImport("gl.roundedRect", "main.js")]
    public static partial void RoundedRect(double x, double y, double w, double h, double r, string color);

    [JSImport("gl.fillCircle", "main.js")]
    public static partial void FillCircle(double x, double y, double r, string color);

    [JSImport("gl.fillText", "main.js")]
    public static partial void FillText(string text, double x, double y, string font, string color, string align);

    [JSImport("gl.line", "main.js")]
    public static partial void Line(double x1, double y1, double x2, double y2, string color, double lineWidth);

    [JSImport("gl.save", "main.js")]
    public static partial void Save();

    [JSImport("gl.restore", "main.js")]
    public static partial void Restore();

    [JSImport("gl.translate", "main.js")]
    public static partial void Translate(double x, double y);

    [JSImport("gl.rotate", "main.js")]
    public static partial void Rotate(double radians);

    [JSImport("gl.alpha", "main.js")]
    public static partial void Alpha(double a);

    [JSImport("gl.shadow", "main.js")]
    public static partial void Shadow(string color, double blur);

    [JSImport("gl.noShadow", "main.js")]
    public static partial void NoShadow();

    /// <summary>加载图片到纹理缓存（可选，演示游戏使用矢量绘制）。</summary>
    [JSImport("gl.loadImage", "main.js")]
    public static partial Task<bool> LoadImage(string id, string url);

    [JSImport("gl.drawImage", "main.js")]
    public static partial void DrawImage(string id, double dx, double dy, double dw, double dh);
}
