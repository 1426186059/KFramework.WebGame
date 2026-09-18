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

        /// <summary>测量文本占的宽高（多行按换行符分行）。</summary>
        Vector2 Measure(string text);

        /// <summary>Measure 的 MonoGame 命名别名。</summary>
        Vector2 MeasureString(string text);

        /// <summary>把文本画进 <paramref name="batch"/>（须在 Begin / End 之间调用）。</summary>
        void Draw(SpriteBatch batch, string text, Vector2 position, Color color,
                  float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth);
    }
}
