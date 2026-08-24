namespace WebAssemblyBrowserApp.Games;

/// <summary>
/// 跨渲染器绘制接口：让 XyWorld 的绘制逻辑在 Canvas2D / WebGL / WebGPU 三端共用。
/// 各端引擎只需把对应 API 封装成这个接口。
/// </summary>
public interface IXyRenderer
{
    void Clear(string color);
    void FillRect(float x, float y, float w, float h, string color);
    void FillCircle(float cx, float cy, float r, string color);
    void FillText(string text, float x, float y, string font, string color, string align);
    void Alpha(float a);
}
