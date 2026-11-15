using Client.MirGraphics;
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

    public static MirEngine.Size MeasureText(string text, Font font, MirEngine.Size proposedSize, EngineTextStyle style)
        => EngineText.MeasureText(text, font, ToEngineSize(proposedSize), style.HorizontalWrap);

    // ---------------- DrawText ----------------

    public static void DrawText(string text, Font font, MirEngine.Rectangle bounds, int foreColor, EngineTextStyle style)
    {
        EngineText.DrawText(DXManager.Batch, text, font,
            new EngineRect(bounds.X, bounds.Y, bounds.Width, bounds.Height),
            EngineColor.FromArgb((uint)foreColor), style.Anchor, style.HorizontalWrap, style.VerticalWrap);
    }

    private static EngineSize ToEngineSize(MirEngine.Size s) => new EngineSize(s.Width, s.Height);
}
