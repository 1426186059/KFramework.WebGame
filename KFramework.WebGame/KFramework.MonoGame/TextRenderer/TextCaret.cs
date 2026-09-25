namespace KFramework.MonoGame
{
    /// <summary>
    /// 输入光标：<b>完全由引擎自绘</b>（浏览器 DOM &lt;input&gt; 只作 IME / 键盘捕获，见
    /// <see cref="Input_IME"/>，不显示任何文字或光标）。
    ///
    /// 闪烁由 <see cref="Draw"/> 内部自行驱动——调用方每帧调 <see cref="Draw"/> 即可看到光标闪烁，
    /// 无需再单独喂时间或推进状态。若控件需要在"亮/灭翻转"那一刻才重建纹理，
    /// 可用 <see cref="Tick"/> 判断是否变化。
    /// </summary>
    internal sealed class TextCaret
    {
        /// <summary>闪烁间隔（毫秒）。</summary>
        public long BlinkIntervalMs { get; set; } = 530;

        /// <summary>当前是否处于"亮"半周期。</summary>
        public bool Visible { get; private set; }

        /// <summary>是否应由引擎绘制光标（等于 <see cref="Visible"/>：光标一律由引擎自绘）。</summary>
        public bool ShouldDraw => Visible;

        /// <summary>默认左边距。</summary>
        public const float DefaultPadLeft = 3f;

        private long _lastToggle = long.MinValue;
        private Texture2D _whitePixel;

        /// <summary>
        /// 推进闪烁节拍。返回 true 表示可见性发生了翻转（控件可据此决定是否重建纹理）。
        /// Draw 内部会自动调用；重复调用安全（同一帧内第二次不会再次翻转）。
        /// </summary>
        public bool Tick(bool focused)
        {
            if (!focused)
            {
                bool wasVisible = Visible;
                Visible = false;
                _lastToggle = long.MinValue;
                return wasVisible;
            }

            long now = System.Environment.TickCount64;
            if (_lastToggle == long.MinValue)
            {
                _lastToggle = now;
                Visible = true;
                return true;
            }

            if (now - _lastToggle >= BlinkIntervalMs)
            {
                _lastToggle = now;
                Visible = !Visible;
                return true;
            }

            return false;
        }

        /// <summary>重置闪烁节拍（重新聚焦时让光标立即亮起）。</summary>
        public void Reset()
        {
            _lastToggle = long.MinValue;
            Visible = false;
        }

        /// <summary>光标位置（相对 bounds 左上角）。</summary>
        public Vector2 GetPosition(IFont font, string text, int caretIndex, Rectangle bounds, bool multiline, float padLeft = DefaultPadLeft)
        {
            float lineH = font != null ? font.LineSpacing : 0f;

            string prefix = string.Empty;
            if (!string.IsNullOrEmpty(text) && caretIndex > 0)
                prefix = text.Substring(0, System.Math.Min(caretIndex, text.Length));

            float x = padLeft;
            if (font != null && prefix.Length > 0)
                x += font.MeasureString(prefix).X;

            float y = multiline ? 2f : System.Math.Max(0f, (bounds.Height - lineH) / 2f);
            return new Vector2(x, y);
        }

        /// <summary>
        /// 每帧调用：内部先推进闪烁节拍，再按 <see cref="ShouldDraw"/> 绘制光标竖线。
        /// 未聚焦时熄灭且不绘制。
        /// </summary>
        public void Draw(SpriteBatch batch, GraphicsDevice device, IFont font, string text, int caretIndex,
                         Rectangle bounds, Color color, bool multiline, bool focused, float padLeft = DefaultPadLeft)
        {
            Tick(focused);
            DrawCaret(batch, device, font, text, caretIndex, bounds, color, multiline, padLeft);
        }

        /// <summary>
        /// 仅按当前 <see cref="ShouldDraw"/> 绘制光标竖线，<b>不推进</b>闪烁节拍
        /// （节拍由 <see cref="Tick"/> 控制，便于外部每帧推进、仅在翻转时重建纹理）。
        /// </summary>
        public void DrawCaret(SpriteBatch batch, GraphicsDevice device, IFont font, string text, int caretIndex,
                              Rectangle bounds, Color color, bool multiline, float padLeft = DefaultPadLeft)
        {
            DrawCore(batch, device, font, text, caretIndex, bounds, color, multiline, padLeft);
        }

        private void DrawCore(SpriteBatch batch, GraphicsDevice device, IFont font, string text, int caretIndex,
                              Rectangle bounds, Color color, bool multiline, float padLeft)
        {
            if (!ShouldDraw || batch == null || device == null || font == null) return;

            Vector2 pos = GetPosition(font, text, caretIndex, bounds, multiline, padLeft);
            float lineH = font.LineSpacing;
            float w = System.Math.Max(1f, lineH * 0.06f);

            batch.Draw(WhitePixel(device),
                new Rectangle(
                    bounds.X + (int)System.Math.Round(pos.X),
                    bounds.Y + (int)System.Math.Round(pos.Y),
                    (int)System.Math.Round(w),
                    (int)System.Math.Round(lineH)),
                color);
        }

        private Texture2D WhitePixel(GraphicsDevice device)
        {
            if (_whitePixel == null)
            {
                _whitePixel = device.CreateTexture(1, 1);
                _whitePixel.SetData(new byte[] { 255, 255, 255, 255 }, 0, 0, 1, 1);
            }
            return _whitePixel;
        }
    }
}
