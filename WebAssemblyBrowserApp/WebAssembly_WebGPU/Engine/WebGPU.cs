using System.Runtime.InteropServices.JavaScript;
using System.Numerics;

namespace WebAssemblyBrowserApp.Engine;

/// <summary>
/// WebGPU 渲染封装。所有绘制调用都通过 [JSImport] 直接转交给 main.js 中的
/// WebGPU 后端（device / renderPipeline / WGSL / 离屏纹理阴影 / 真纹理贴图等）。
/// 接口与旧的 Canvas2D 保持兼容，所以上层游戏逻辑无需改动。
/// 注意：WebGPU 设备初始化是异步的 —— Init() 只负责在 JS 侧触发异步流程，
/// 设备真正就绪后由 JS 回调 C# 的 EngineReady()（见 Program.cs）。
/// </summary>
public static partial class WebGPU
{
    // ---- 生命周期 ----
    [JSImport("gpu.init", "main.js")] public static partial void Init();
    [JSImport("gpu.resize", "main.js")] public static partial void Resize(int w, int h);
    [JSImport("gpu.beginFrame", "main.js")] public static partial void BeginFrame(double clearR, double clearG, double clearB, double clearA);
    [JSImport("gpu.clear", "main.js")] public static partial void ClearColor(string color);
    [JSImport("gpu.endFrame", "main.js")] public static partial void EndFrame();

    // ---- 状态 / 变换 ----
    [JSImport("gpu.setTransform", "main.js")] public static partial void SetTransform(double m11, double m12, double m21, double m22, double dx, double dy);
    [JSImport("gpu.resetTransform", "main.js")] public static partial void ResetTransform();
    [JSImport("gpu.saveTransform", "main.js")] public static partial void SaveTransform();
    [JSImport("gpu.restoreTransform", "main.js")] public static partial void RestoreTransform();
    [JSImport("gpu.translate", "main.js")] public static partial void Translate(double x, double y);
    [JSImport("gpu.setAlpha", "main.js")] public static partial void SetAlpha(double a);

    // ---- 形状 ----
    [JSImport("gpu.fillRect", "main.js")] public static partial void FillRect(double x, double y, double w, double h, string color);
    [JSImport("gpu.roundedRect", "main.js")] public static partial void RoundedRect(double x, double y, double w, double h, double r, string color);
    [JSImport("gpu.fillCircle", "main.js")] public static partial void FillCircle(double cx, double cy, double r, string color);
    [JSImport("gpu.drawLine", "main.js")] public static partial void DrawLine(double x1, double y1, double x2, double y2, double width, string color);
    [JSImport("gpu.strokeRect", "main.js")] public static partial void StrokeRect(double x, double y, double w, double h, double width, string color);

    // ---- 阴影 ----
    [JSImport("gpu.shadow", "main.js")] public static partial void Shadow(double ox, double oy, double blur, double r, double g, double b, double a);
    [JSImport("gpu.shadowColor", "main.js")] public static partial void ShadowColor(string color, double blur);
    [JSImport("gpu.noShadow", "main.js")] public static partial void NoShadow();

    // ---- 文本 / 图片（真纹理） ----
    [JSImport("gpu.fillText", "main.js")] public static partial void FillText(string text, double x, double y, string font, string color, string align);
    [JSImport("gpu.loadImage", "main.js")] public static partial int LoadImage(string src);
    [JSImport("gpu.drawImage", "main.js")] public static partial void DrawImage(int id, double x, double y, double w, double h);
    [JSImport("gpu.measureText", "main.js")] public static partial double MeasureText(string text, string font);

    // ---- 便捷封装 ----
    public static void Clear(string color) => ClearColor(color);
    public static void Clear(Vector4 c) => BeginFrame(c.X, c.Y, c.Z, c.W);
    public static void Save() => SaveTransform();
    public static void Restore() => RestoreTransform();
    public static void Alpha(double a) => SetAlpha(a);

    public static void FillRect(Vector2 pos, Vector2 size, string color) => FillRect(pos.X, pos.Y, size.X, size.Y, color);
    public static void FillCircle(Vector2 c, double r, string color) => FillCircle(c.X, c.Y, r, color);
    public static void FillText(string text, Vector2 pos, string font, string color, string align = "center")
        => FillText(text, pos.X, pos.Y, font, color, align);
    public static void Shadow(string color, double blur) => ShadowColor(color, blur);

    public static void Transform(Matrix3x2 m)
        => SetTransform(m.M11, m.M12, m.M21, m.M22, m.M31, m.M32);
}
