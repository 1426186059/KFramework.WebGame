namespace KFramework.MonoGame.TextRenderer
{
    /// <summary>
    /// 文本输入光标的两种工作模式。
    /// </summary>
    public enum TextCaretMode
    {
        /// <summary>
        /// 浏览器模式：文字与光标都由 DOM &lt;input&gt; 覆盖层显示（原生光标、原生选区、原生 IME 候选窗）。
        /// 引擎侧不绘制光标，且控件纹理通常只保留背景，避免与 DOM 文字重影。
        /// </summary>
        Browser = 0,

        /// <summary>
        /// 自绘模式：DOM &lt;input&gt; 仅作 IME / 键盘捕获代理（透明），文字与光标由引擎在 canvas 上绘制。
        /// 引擎侧负责光标闪烁与绘制，不依赖浏览器的光标渲染。
        /// </summary>
        Rendered = 1
    }

    /// <summary>
    /// 输入光标：负责闪烁节拍与（自绘模式下）光标绘制。
    /// Browser 模式下 <see cref="ShouldDraw"/> 恒为 false，交给浏览器原生光标。
    /// </summary>
    public sealed class TextCaret
    {
        /// <summary>光标模式，默认自绘（与浏览器 DOM 覆盖层的 transparent 默认值一致）。</summary>
        public TextCaretMode Mode { get; set; } = TextCaretMode.Rendered;

        /// <summary>闪烁间隔（毫秒）。</summary>
        public long BlinkIntervalMs { get; set; } = 530;

        /// <summary>当前是否处于"亮"半周期。</summary>
        public bool Visible { get; private set; }

        /// <summary>是否应由引擎绘制光标（Browser 模式下交给浏览器，恒为 false）。</summary>
        public bool ShouldDraw => Mode == TextCaretMode.Rendered && Visible;

        private long _lastToggle = long.MinValue;
        private Texture2D _whitePixel;

        /// <summary>按时间推进闪烁；失焦时立即熄灭并把光标置到起点。</summary>
        public void Update(long nowMs, bool focused)
        {
            if (!focused)
            {
                Visible = false;
                _lastToggle = long.MinValue;
                return;
            }

            if (_lastToggle == long.MinValue)
            {
                _lastToggle = nowMs;
                Visible = true;
                return;
            }

            if (nowMs - _lastToggle >= BlinkIntervalMs)
            {
                _lastToggle = nowMs;
                Visible = !Visible;
            }
        }

        /// <summary>自绘模式下的光标位置（相对 bounds 左上角）。</summary>
        public Vector2 GetPosition(GraphicsDevice device, IFont font, string text, int caretIndex, Rectangle bounds, bool multiline, float padLeft)
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

        /// <summary>自绘模式下绘制光标竖线。Browser 模式下此调用为空操作。</summary>
        public void Draw(SpriteBatch batch, GraphicsDevice device, IFont font, string text, int caretIndex, Rectangle bounds, Color color, bool multiline, float padLeft = 3f)
        {
            if (!ShouldDraw || batch == null || device == null || font == null) return;

            Vector2 pos = GetPosition(device, font, text, caretIndex, bounds, multiline, padLeft);
            float lineH = font.LineSpacing;
            // 光标宽度：优先用字形行高推算；IFont 无字号概念时退化为 1px。
            float w = System.Math.Max(1f, lineH * 0.06f);
            Rectangle rect = new Rectangle(
                bounds.X + (int)System.Math.Round(pos.X),
                bounds.Y + (int)System.Math.Round(pos.Y),
                (int)System.Math.Round(w),
                (int)System.Math.Round(lineH));

            batch.Draw(WhitePixel(device), rect, color);
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
