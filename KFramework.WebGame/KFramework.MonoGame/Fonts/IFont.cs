namespace KFramework.MonoGame
{
    /// <summary>
    /// 字体抽象：<see cref="SpriteFont"/>（系统字体 / 自定义字体，运行时由 Canvas2D 光栅化）与
    /// <see cref="BitmapFont"/>（美术字，BMFont 图集）都实现它，使 UI（如 KLabel）与
    /// <see cref="SpriteBatch.DrawString(IFont, string, Vector2, Color)"/> 不必关心字形来源。
    /// </summary>
    public interface IFont
    {
        /// <summary>行距（MonoGame 命名为 LineSpacing，等价于行高）。</summary>
        float LineSpacing { get; }

        /// <summary>基线到行顶的距离（像素）。富文本混排字号时按各字号的 Ascent 做基线对齐。</summary>
        float Ascent { get; }

        /// <summary>基线到字形底部的距离（像素）。富文本下划线按各字号的 Descent 贴字形底。</summary>
        float Descent { get; }

        /// <summary>测量文本占的宽高（多行按换行符分行）。</summary>
        Vector2 MeasureString(string text);
        Vector2 MeasureString(ReadOnlySpan<char> text);
        /// <summary>测量单个字符：富文本逐字符排版用，避免 ch.ToString() 产生临时字符串。</summary>
        Vector2 MeasureString(char ch);

        /// <summary>测量 StringBuilder：WrapLines 产出的行不再 ToString。</summary>
        Vector2 MeasureString(System.Text.StringBuilder text);

        /// <summary>把文本画进 <paramref name="batch"/>（须在 Begin / End 之间调用）。</summary>
        void Draw(SpriteBatch batch, string text, Vector2 position, Color color,
                  float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth);

        /// <summary>Draw 的 StringBuilder 版本：WrapLines 产出的行不再 ToString。</summary>
        void Draw(SpriteBatch batch, System.Text.StringBuilder text, Vector2 position, Color color,
                  float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth);

        /// <summary>Draw 的 span 版本：调用方持有 ReadOnlySpan 时无需分配字符串。</summary>
        void Draw(SpriteBatch batch, ReadOnlySpan<char> text, Vector2 position, Color color,
                  float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth);
    }
}
