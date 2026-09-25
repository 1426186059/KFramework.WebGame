namespace KFramework.MonoGame
{
    /// <summary>
    /// 输入框（TextBox）绘制辅助：引擎自绘文字 + 光标。
    ///
    /// 与原版 Crystal 一致，本类只负责"画"；IME / 键盘捕获由 <see cref="Input_IME"/> 单独处理——
    /// 文本框获得焦点时由调用方直接 <see cref="Input_IME.Open"/> 接管，失焦时 <see cref="Input_IME.Close"/>。
    ///
    /// <para>光标闪烁由内部 <see cref="TextCaret"/> 自驱：每帧调用 <see cref="DrawTextBox"/> 即可看到闪烁，
    /// 调用方只需传入 <paramref name="focused"/>（失焦时光标自动熄灭），无需自行推进节拍或读取光标状态。</para>
    /// </summary>
    public static class TextBoxRenderer
    {
        /// <summary>默认左边距，对齐原版输入框内边距。</summary>
        public const float DefaultPadLeft = TextCaret.DefaultPadLeft;

        private static readonly TextCaret _caret = new TextCaret();

        /// <summary>
        /// 绘制文本框：引擎自绘文字 + 光标。光标闪烁由内部 <see cref="TextCaret"/> 自驱，
        /// 传入 <paramref name="focused"/> 指示是否聚焦（失焦时光标自动熄灭）。
        /// </summary>
        public static void DrawTextBox(SpriteBatch batch, GraphicsDevice device, IFont font, string text,
            Rectangle bounds, Color foreColor, int caretIndex,
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

            // 光标闪烁由 TextCaret.Draw 内部自驱（失焦时熄灭），故动画完全在引擎内。
            _caret.Draw(batch, device, font, text, caretIndex, bounds, foreColor, multiline, focused, padLeft);
        }
    }
}
