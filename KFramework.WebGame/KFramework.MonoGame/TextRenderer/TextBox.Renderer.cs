namespace KFramework.MonoGame
{
    /// <summary>
    /// 输入框（TextBox）绘制辅助：引擎自绘文字 + 光标 / 选区。
    ///
    /// 与原版 Crystal / UGUI 一致，本类只负责“画”；IME / 键盘捕获由 <see cref="Input_IME"/> 单独处理。
    ///
    /// <para>光标由调用方持有的 <see cref="Pen"/>（每文本框一支）自驱闪烁：传入 <paramref name="pen"/> 与
    /// <paramref name="focused"/>，本方法会驱动该 Pen 的闪烁状态并绘制光标；失焦时自动熄灭。
    /// 选区高亮独立于光标，见下方 <see cref="DrawSelection"/>。</para>
    /// </summary>
    public sealed partial class TextBox
    {
        public const float DefaultPadLeft = Pen.DefaultPadLeft;

        public static void DrawTextBox(SpriteBatch batch, GraphicsDevice device, IFont font, string text,
            Pen pen, Rectangle bounds, Color foreColor, int caretIndex,
            bool multiline, bool focused, float padLeft = DefaultPadLeft, string composition = "",
            int selectionStart = 0, int selectionLength = 0)
        {
            if (batch == null || device == null || font == null) return;

            string display = string.IsNullOrEmpty(composition) ? (text ?? string.Empty) : (text ?? string.Empty) + composition;
            float lineH = font.LineSpacing;
            float y = multiline ? 2f : System.Math.Max(0f, (bounds.Height - lineH) / 2f);

            if (pen != null)
                pen.OnFocusChanged(focused);

            if (!string.IsNullOrEmpty(display))
            {
                font.Draw(batch, display, new Vector2(bounds.X + padLeft, bounds.Y + y), foreColor,
                    0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            }

            bool hasSelection = selectionLength > 0 && (selectionStart + selectionLength) <= text.Length;
            if (hasSelection)
            {
                DrawSelection(batch, device, font, text, selectionStart, selectionStart + selectionLength,
                    bounds, new Color(51, 153, 255, 128), multiline, padLeft);
            }
            else if (focused && pen != null)
            {
                int caretPos = System.Math.Min(caretIndex + (composition == null ? 0 : composition.Length), display.Length);
                pen.Draw(batch, device, font, display, caretPos, bounds, foreColor, multiline, padLeft);
            }
        }

        // —— 以下为选区高亮绘制：选区是独立于光标的关注点，故放在本渲染器而非 Pen 内 ——

        private static float Measure(IFont font, string s)
            => (font != null && !string.IsNullOrEmpty(s)) ? font.MeasureString(s).X : 0f;

        private static Texture2D _whitePixel;
        private static Texture2D WhitePixel(GraphicsDevice device)
        {
            if (_whitePixel == null)
            {
                _whitePixel = device.CreateTexture(1, 1);
                _whitePixel.SetData(new byte[] { 255, 255, 255, 255 }, 0, 0, 1, 1);
            }
            return _whitePixel;
        }

        /// <summary>
        /// 选区高亮：对齐 UGUI <c>GenerateHighlight</c>，仅画半透明矩形，<b>不画光标</b>。
        /// 选区与光标是两个独立概念，因此此绘制逻辑放在本渲染器（文本框整体绘制者）内，
        /// 而不污染只负责光标的 <see cref="Pen"/>。
        /// </summary>
        private static void DrawSelection(SpriteBatch batch, GraphicsDevice device, IFont font, string text,
            int selStart, int selEnd, Rectangle bounds, Color color, bool multiline, float padLeft = DefaultPadLeft)
        {
            if (batch == null || device == null || font == null) return;
            if (selEnd < selStart) { int tmp = selStart; selStart = selEnd; selEnd = tmp; }
            if (string.IsNullOrEmpty(text)) return;
            selStart = System.Math.Max(0, System.Math.Min(selStart, text.Length));
            selEnd = System.Math.Max(0, System.Math.Min(selEnd, text.Length));
            if (selEnd <= selStart) return;

            Texture2D px = WhitePixel(device);
            int lineH = (int)System.Math.Round(font.LineSpacing);

            if (!multiline)
            {
                float x0 = padLeft + Measure(font, text.Substring(0, selStart));
                float x1 = padLeft + Measure(font, text.Substring(0, selEnd));
                batch.Draw(px, new Rectangle(bounds.X + (int)System.Math.Round(x0), bounds.Y + (int)System.Math.Max(0f, (bounds.Height - font.LineSpacing) / 2f),
                    (int)System.Math.Max(1, System.Math.Round(x1 - x0)), lineH), color);
                return;
            }

            // 多行：逐行绘制选中片段（对齐 UGUI 按行裁剪）。
            int line = 0, idx = 0;
            foreach (var ln in text.Split('\n'))
            {
                int lineStart = idx;
                int lineEnd = idx + ln.Length;
                int s = System.Math.Max(selStart, lineStart);
                int e = System.Math.Min(selEnd, lineEnd);
                if (e > s)
                {
                    float x0 = padLeft + Measure(font, ln.Substring(0, s - lineStart));
                    float x1 = padLeft + Measure(font, ln.Substring(0, e - lineStart));
                    batch.Draw(px, new Rectangle(bounds.X + (int)System.Math.Round(x0), bounds.Y + (int)System.Math.Round(line * font.LineSpacing + 2f),
                        (int)System.Math.Max(1, System.Math.Round(x1 - x0)), lineH), color);
                }
                idx = lineEnd + 1; // 跳过 '\n'
                line++;
            }
        }
    }
}
