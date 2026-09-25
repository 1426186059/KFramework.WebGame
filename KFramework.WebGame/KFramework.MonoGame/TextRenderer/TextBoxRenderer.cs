namespace KFramework.MonoGame
{
    /// <summary>
    /// 输入框（TextBox）绘制辅助：引擎自绘文字 + 光标 / 选区。
    ///
    /// 与原版 Crystal / UGUI 一致，本类只负责"画"；IME / 键盘捕获由 <see cref="Input_IME"/> 单独处理——
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
        /// 绘制文本框：引擎自绘文字 + 选区高亮 + 光标。
        /// 光标闪烁由内部 <see cref="TextCaret"/> 自驱，传入 <paramref name="focused"/> 指示是否聚焦（失焦时光标自动熄灭）。
        /// 对齐 UGUI：存在选区时只画高亮、不画光标。
        /// </summary>
        public static void DrawTextBox(SpriteBatch batch, GraphicsDevice device, IFont font, string text,
            Rectangle bounds, Color foreColor, int caretIndex,
            bool multiline, bool focused, float padLeft = DefaultPadLeft, string composition = "",
            int selectionStart = 0, int selectionLength = 0)
        {
            if (batch == null || device == null || font == null) return;

            string display = string.IsNullOrEmpty(composition) ? (text ?? string.Empty) : (text ?? string.Empty) + composition;
            float lineH = font.LineSpacing;
            float y = multiline ? 2f : System.Math.Max(0f, (bounds.Height - lineH) / 2f);

            // 选区高亮（仅 text 部分；composition 预览不计入选区）。对齐 UGUI：有选区时不画光标。
            bool hasSelection = selectionLength > 0 && (selectionStart + selectionLength) <= text.Length;
            if (hasSelection)
            {
                _caret.DrawSelection(batch, device, font, text, selectionStart, selectionStart + selectionLength,
                    bounds, new Color(51, 153, 255, 128), multiline, padLeft);
            }

            if (!string.IsNullOrEmpty(display))
            {
                font.Draw(batch, display, new Vector2(bounds.X + padLeft, bounds.Y + y), foreColor,
                    0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            }

            if (!hasSelection)
            {
                // 光标置于 text 之后、IME 预览之后（对齐 UGUI：caretPosition 跟随 composition 末尾）。
                int caretPos = System.Math.Min(caretIndex + (composition == null ? 0 : composition.Length), display.Length);
                _caret.Draw(batch, device, font, display, caretPos, bounds, foreColor, multiline, focused, padLeft);
            }
        }
    }
}
