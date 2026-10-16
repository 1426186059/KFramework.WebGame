using Client.MirGraphics;
using WebGame.Mir2.MonoGame.Client;   // TextFormatFlags 及其 ToTextStyle() 折算扩展
using EngineColor = KFramework.MonoGame.Color;
using EngineRect = KFramework.MonoGame.Rectangle;
using EngineSize = KFramework.MonoGame.Size;
using EngineText = KFramework.MonoGame.TextRenderer;
using EngineTextStyle = KFramework.MonoGame.TextStyle;

public static class TextRenderer
{
    public static MirEngine.Size MeasureText(string text, Font font)
        => EngineText.MeasureText(text, font);

    public static MirEngine.Size MeasureText(string text, Font font, MirEngine.Size proposedSize)
        => EngineText.MeasureText(text, font, ToEngineSize(proposedSize));

    // WinForms 兼容入口：把 GDI 风格标志折算成引擎的 Unity 三元组（折算见 Shims/TextFormatFlags.cs 的 ToTextStyle）。
    // 业务代码继续用 TextFormatFlags，后续要迁移只需改这几个入口，不必动 150 处调用点。
    public static MirEngine.Size MeasureText(string text, Font font, MirEngine.Size proposedSize, TextFormatFlags flags)
        => EngineText.MeasureText(text, font, ToEngineSize(proposedSize), flags.ToTextStyle().HorizontalWrap);

    // ---------------- DrawText ----------------

    public static void DrawText(string text, Font font, MirEngine.Rectangle bounds, int foreColor, TextFormatFlags flags)
    {
        EngineTextStyle style = flags.ToTextStyle();
        EngineText.DrawText(DXManager.Batch, text, font,
            new EngineRect(bounds.X, bounds.Y, bounds.Width, bounds.Height),
            EngineColor.FromArgb((uint)foreColor), style.Anchor, style.HorizontalWrap, style.VerticalWrap);

    }

    private static EngineSize ToEngineSize(MirEngine.Size s) => new EngineSize(s.Width, s.Height);
}
