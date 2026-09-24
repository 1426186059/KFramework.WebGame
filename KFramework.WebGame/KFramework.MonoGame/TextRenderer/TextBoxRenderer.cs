namespace KFramework.MonoGame
{
    /// <summary>
    /// 输入框（TextBox）的绘制与 IME 覆盖层的统一入口。
    ///
    /// <b>文字与光标一律由引擎自绘</b>；浏览器 DOM &lt;input&gt; 经 <see cref="TextInputHtmlIme"/>
    /// 只作 IME / 键盘捕获代理（始终透明），不再有"由 DOM 显示文字 / 光标"的模式。
    ///
    /// 调用方负责：绑定渲染目标、Begin/End 批次、清屏。
    /// </summary>
    public static class TextBoxRenderer
    {
        /// <summary>默认左边距，对齐原版输入框内边距。</summary>
        public const float DefaultPadLeft = TextCaret.DefaultPadLeft;

        // ---------------- IME 覆盖层（始终透明，仅捕获输入） ----------------

        public static void Show(double cx, double cy, double cw, double ch, double fontPx, int color,
                                string value, bool password, int maxLength, bool multiline, string cssFont)
            => TextInputHtmlIme.Show(cx, cy, cw, ch, fontPx, color, value, password, maxLength, multiline, cssFont);

        public static void Hide() => TextInputHtmlIme.Hide();

        public static void Reposition(double cx, double cy, double cw, double ch)
            => TextInputHtmlIme.Reposition(cx, cy, cw, ch);

        // ---------------- 绘制（引擎自绘文本 + 光标） ----------------

        public static void DrawTextBox(SpriteBatch batch, GraphicsDevice device, IFont font, string text,
            Rectangle bounds, Color foreColor, int caretIndex, TextCaret caret,
            bool multiline, bool focused, float padLeft = DefaultPadLeft)
        {
            if (batch == null || device == null || font == null) return;

            float lineH = font.LineSpacing;
            float y = multiline ? 2f : System.Math.Max(0f, (bounds.Height - lineH) / 2f);

            if (!string.IsNullOrEmpty(text))
            {
                font.Draw(batch, text, new Vector2(bounds.X + padLeft, bounds.Y + y), foreColor,
                    0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            }

            // 光标闪烁由 TextCaret.Draw 内部驱动。
            caret?.Draw(batch, device, font, text, caretIndex, bounds, foreColor, multiline, focused, padLeft);
        }
    }
}
