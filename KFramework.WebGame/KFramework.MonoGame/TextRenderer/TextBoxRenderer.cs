namespace KFramework.MonoGame
{
    /// <summary>
    /// 输入框（TextBox）的绘制与 IME 覆盖层的统一入口。
    ///
    /// <b>文字与光标一律由引擎自绘</b>；浏览器 DOM &lt;input&gt; 经 <see cref="Input_IME"/>
    /// 只作 IME / 键盘捕获代理（始终透明），不再有"由 DOM 显示文字 / 光标"的模式。
    ///
    /// 调用方负责：绑定渲染目标、Begin/End 批次、清屏。
    /// </summary>
    public static class TextBoxRenderer
    {
        /// <summary>默认左边距，对齐原版输入框内边距。</summary>
        public const float DefaultPadLeft = TextCaret.DefaultPadLeft;

        /// <summary>光标闪烁状态由 <see cref="CaretTick"/> 每帧推进；本类内部持有，不对外暴露 <see cref="TextCaret"/>。</summary>
        private static readonly TextCaret _caret = new TextCaret();

        // ---------------- IME 覆盖层（始终透明，仅捕获输入） ----------------

        public static void Show(double cx, double cy, double cw, double ch, 
            double fontPx, int color,
            string value, bool password, int maxLength, bool multiline, string cssFont)
        { 
           Input_IME.Open(
               cx, cy, cw, ch, 
               fontPx, color, value, password, 
               maxLength, multiline, cssFont);
        }

        public static void Hide()
        {
            Input_IME.Close();
        }

        // ---------------- 绘制（引擎自绘文本 + 光标） ----------------

        /// <summary>
        /// 绘制文本框：引擎自绘文字 + 光标。光标闪烁由 <see cref="CaretTick"/> 每帧推进，
        /// 本方法只在当前光标可见态下绘制竖线（不重复推进节拍）。
        /// </summary>
        public static void DrawTextBox(SpriteBatch batch, GraphicsDevice device, IFont font, string text,
            Rectangle bounds, Color foreColor, int caretIndex,
            bool multiline, float padLeft = DefaultPadLeft)
        {
            if (batch == null || device == null || font == null) return;

            float lineH = font.LineSpacing;
            float y = multiline ? 2f : System.Math.Max(0f, (bounds.Height - lineH) / 2f);

            if (!string.IsNullOrEmpty(text))
            {
                font.Draw(batch, text, new Vector2(bounds.X + padLeft, bounds.Y + y), foreColor,
                    0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            }

            _caret.DrawCaret(batch, device, font, text, caretIndex, bounds, foreColor, multiline, padLeft);
        }

        /// <summary>光标当前是否可见。</summary>
        public static bool CaretVisible => _caret.Visible;

        /// <summary>
        /// 推进光标闪烁节拍（每帧绘制前调用一次）。返回 true 表示可见性发生了翻转，
        /// 控件可据此决定是否重建纹理。DrawTextBox 内部只按当前状态绘制，不会重复推进。
        /// </summary>
        public static bool CaretTick(bool focused) => _caret.Tick(focused);
    }
}
