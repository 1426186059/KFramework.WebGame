using Client.MirGraphics;
using WebGame.Mir2.MonoGame.Client;   // TextFormatFlags 及其 ToTextStyle() 折算扩展
using SlimTexture = SlimDX.Direct3D9.Texture;
using SlimSurface = SlimDX.Direct3D9.Surface;
using EngineDevice = KFramework.MonoGame.GraphicsDevice;
using EngineColor = KFramework.MonoGame.Color;
using EngineRect = KFramework.MonoGame.Rectangle;
using EngineText = KFramework.MonoGame.TextRenderer;
using EngineSize = KFramework.MonoGame.Size;
using EngineTextStyle = KFramework.MonoGame.TextStyle;

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
        => EngineText.MeasureText(text, font);

    public static MirEngine.Size MeasureText(string text, Font font, MirEngine.Size proposedSize)
        => EngineText.MeasureText(text, font, ToEngineSize(proposedSize));

    // WinForms 兼容入口：把 GDI 风格标志折算成引擎的 Unity 三元组（折算见 Shims/TextFormatFlags.cs 的 ToTextStyle）。
    // 业务代码继续用 TextFormatFlags，后续要迁移只需改这几个入口，不必动 150 处调用点。
    public static MirEngine.Size MeasureText(string text, Font font, MirEngine.Size proposedSize, TextFormatFlags flags)
        => EngineText.MeasureText(text, font, ToEngineSize(proposedSize), flags.ToTextStyle().HorizontalWrap);

    // ---------------- DrawText ----------------

    public static void DrawText(SlimTexture texture, string text, Font font, MirEngine.Rectangle bounds, int foreColor, TextFormatFlags flags)
    {
        if (texture?.RenderTarget == null) return;

        SlimSurface saved = DXManager.CurrentSurface;
        DXManager.SetSurface(new SlimSurface(texture));
        try
        {
            // WinForms 兼容入口：GDI 风格标志 → Unity 三元组（TextAnchor / HorizontalWrapMode / VerticalWrapMode）。
            EngineTextStyle style = flags.ToTextStyle();

            // 只做两件事：切到目标纹理、把字形塞进【调用方已经开好的批】。
            // 本方法【既不开批也不收批】—— GDI 与游戏渲染已合成为一个渲染系统，批的边界归调用方：
            // 同一张 ControlTexture 上要画多少次（如 MirLabel 的 4 次描边 + 1 次前景）由调用方用
            // 一次 BeginSprite / EndSprite 包住，N 次绘制才合成 1 次 draw call；若在这里每次自己开收批，
            // N 次就是 N 段。调用方必须保证进入本方法时批已开（见 MirLabel / MirTextBox / FPSDialog）。
            EngineText.DrawText(DXManager.Batch, text, font,
                new EngineRect(bounds.X, bounds.Y, bounds.Width, bounds.Height),
                EngineColor.FromArgb((uint)foreColor), style.Anchor, style.HorizontalWrap, style.VerticalWrap);
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
