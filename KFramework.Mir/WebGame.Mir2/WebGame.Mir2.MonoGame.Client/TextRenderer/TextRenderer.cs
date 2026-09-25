using Client.MirGraphics;
using SlimTexture = SlimDX.Direct3D9.Texture;
using SlimSurface = SlimDX.Direct3D9.Surface;
using EngineDevice = KFramework.MonoGame.GraphicsDevice;
using EngineColor = KFramework.MonoGame.Color;
using EngineRect = KFramework.MonoGame.Rectangle;
using EngineText = KFramework.MonoGame.TextRenderer;
using EngineSize = KFramework.MonoGame.Size;

// 客户端适配层：把引擎通用文本库（KFramework.MonoGame/TextRenderer）与本工程的设备、离屏纹理
// （SlimDX 壳 SlimDX.Direct3D9.Texture -> RenderTarget2D）接起来。
//
// 字体管理（Font -> SpriteFont 缓存）、度量、排版、折行、绘制全部由引擎库实现；
// 本文件只做三件事：提供设备（DXManager.GDevice）、绑定/恢复离屏渲染目标、int ARGB 与 Color 互转。
//
// 对外 API 形态严格对齐 System.Windows.Forms.TextRenderer（仅 MeasureText / DrawText）。
// Clear 是唯一的端口原语：原版由控件自己在 OnPaint 里清屏，这里桥接离屏纹理清屏。
public static class TextRenderer
{
    private static EngineDevice Device => DXManager.GDevice;

    // ---------------- MeasureText ----------------

    public static MirEngine.Size MeasureText(string text, Font font)
        => EngineText.MeasureText(Device, text, font);

    public static MirEngine.Size MeasureText(string text, Font font, MirEngine.Size proposedSize)
        => EngineText.MeasureText(Device, text, font, ToEngineSize(proposedSize));

    public static MirEngine.Size MeasureText(string text, Font font, MirEngine.Size proposedSize, TextFormatFlags flags)
        => EngineText.MeasureText(Device, text, font, ToEngineSize(proposedSize), flags);

    // ---------------- DrawText ----------------

    public static void DrawText(SlimTexture texture, string text, Font font, MirEngine.Rectangle bounds, int foreColor, TextFormatFlags flags)
    {
        if (texture?.RenderTarget == null) return;

        SlimSurface saved = DXManager.CurrentSurface;
        DXManager.SetSurface(new SlimSurface(texture));
        try
        {
            EngineText.DrawText(Device, text, font,
                new EngineRect(bounds.X, bounds.Y, bounds.Width, bounds.Height),
                EngineColor.FromArgb((uint)foreColor), flags);
        }
        finally
        {
            DXManager.SetSurface(saved);
        }
    }

    // ---------------- 端口原语：离屏纹理清屏 ----------------

    public static void Clear(SlimTexture texture, int argb)
    {
        if (texture?.RenderTarget == null) return;

        SlimSurface saved = DXManager.CurrentSurface;
        DXManager.SetSurface(new SlimSurface(texture));
        try
        {
            EngineColor clear = ((argb >> 24) & 0xFF) != 0
                ? EngineColor.FromArgb((uint)argb)
                : EngineColor.Transparent;
            Device?.Clear(clear);
        }
        finally
        {
            DXManager.SetSurface(saved);
        }
    }

    private static EngineSize ToEngineSize(MirEngine.Size s) => new EngineSize(s.Width, s.Height);
}
