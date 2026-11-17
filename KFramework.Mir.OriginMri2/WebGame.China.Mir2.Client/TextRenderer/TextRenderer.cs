using Client.MirGraphics;
using EngineColor = KFramework.MonoGame.Color;
using EngineRect = KFramework.MonoGame.Rectangle;
using EngineText = KFramework.MonoGame.TextRenderer;
using EngineTextStyle = KFramework.MonoGame.TextStyle;

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

    public static void DrawText(string text, Font font, Rectangle bounds, int foreColor, EngineTextStyle style)
    {
        EngineText.DrawText(DXManager.Batch, text, font,
            new EngineRect(bounds.X, bounds.Y, bounds.Width, bounds.Height),
            EngineColor.FromArgb((uint)foreColor), style.Anchor, style.HorizontalWrap, style.VerticalWrap);
    }

    /// <summary>富文本绘制：<paramref name="richText"/>=true 解析 &lt;color&gt;/&lt;link&gt; 等标签。
    /// <paramref name="outlineMode"/>=true 时忽略段颜色、只画纯文本（描边用）。
    /// <paramref name="linkHits"/> 非 null 时收集超链接命中矩形（相对传入 bounds 的局部坐标）。</summary>
    public static void DrawText(string text, Font font, Rectangle bounds, int foreColor, EngineTextStyle style,
                                bool richText, bool outlineMode = false,
                                List<KFramework.MonoGame.RichTextLink> linkHits = null)
    {
        EngineText.DrawText(DXManager.Batch, text, font,
            new EngineRect(bounds.X, bounds.Y, bounds.Width, bounds.Height),
            EngineColor.FromArgb((uint)foreColor), style.Anchor, style.HorizontalWrap, style.VerticalWrap,
            richText, outlineMode, linkHits);
    }

    /// <summary>富文本度量：剥离标签后测量，供 AutoSize 使用。</summary>
    public static Size MeasureText(string text, Font font, bool richText)
    {
        if (!richText)
        {
            return EngineText.MeasureText(text, font);
        }
        return EngineText.MeasureText(text, font, new KFramework.MonoGame.Size(int.MaxValue, int.MaxValue),
                                      KFramework.MonoGame.HorizontalWrapMode.Overflow, true);
    }
}
