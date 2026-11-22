using Client.MirGraphics;
using EngineColor = KFramework.MonoGame.Color;
using EngineRect = KFramework.MonoGame.Rectangle;
using EngineRich = KFramework.MonoGame.RichTextObject;
using EngineText = KFramework.MonoGame.TextRenderer;
using EngineTextStyle = KFramework.MonoGame.TextStyle;
using EngineVector2 = KFramework.MonoGame.Vector2;

public static class TextRenderer
{
    public static Size MeasureText(string text, Font font)
    {
        return EngineText.MeasureText(text, font);
    }

    public static Size MeasureText(string text, Font font, Size proposedSize)
    {
        return EngineText.MeasureText(text, font, proposedSize);
    }

    public static Size MeasureText(string text, Font font, Size proposedSize, EngineTextStyle style)
    {
        return EngineText.MeasureText(text, font, proposedSize, style.HorizontalWrap);
    }

    // ---------------- DrawText ----------------

    public static void DrawText(string text, Font font, Rectangle bounds, EngineColor foreColor, EngineTextStyle style)
    {
        EngineText.DrawText(DXManager.Batch, text, font,
            new EngineRect(bounds.X, bounds.Y, bounds.Width, bounds.Height),
            foreColor, style.Anchor, style.HorizontalWrap, style.VerticalWrap);
    }

    // ---------------- 富文本（交给 EngineRich，由调用方持有实例以复用解析结果）----------------

    /// <summary>富文本度量：用调用方持有的 rich 对象剥离标签后测量，供 AutoSize 使用。</summary>
    public static Size MeasureText(EngineRich rich, EngineTextStyle style)
    {
        return rich.Measure(new KFramework.MonoGame.Size(int.MaxValue, int.MaxValue), style.HorizontalWrap);
    }

    /// <summary>富文本绘制；outlineMode=true 时只画去标签的纯文本（描边用）。</summary>
    public static void DrawText(EngineRich rich, Rectangle bounds, EngineColor foreColor, EngineTextStyle style,
                                bool outlineMode = false)
    {
        rich.Draw(DXManager.Batch,
            new EngineRect(bounds.X, bounds.Y, bounds.Width, bounds.Height),
            foreColor, style.Anchor, style.HorizontalWrap, style.VerticalWrap,
            outlineMode);
    }

    /// <summary>超链接命中：只传世界坐标，布局已记在 TextInfo（绘制时确定）。命中返回 url，否则 null。</summary>
    public static string FindIntersectingLink(EngineRich rich, EngineVector2 worldPos)
    {
        return rich.FindIntersectingLink(worldPos);
    }
}
