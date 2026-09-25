namespace KFramework.MonoGame
{
    /// <summary>
    /// 输入光标画笔：命名对齐 WinForms 的 <see cref="System.Drawing.Pen"/>，原版传奇中每个文本框各自持有一支。
    ///
    /// 负责以 text 长度为锚点、用字体度量定位并绘制光标竖线，并维护<b>自身</b>的闪烁节拍——
    /// 这些状态（_blinkStartTime / alpha / _focused）都是 TextBox 的实例字段，因此每个 TextBox 各持有一份独立的
    /// 光标状态，彼此的闪烁相位 / 可见性互不干扰（不再像旧实现那样多个文本框共用一个静态光标）。
    ///
    /// 对齐 UGUI <c>InputField.GenerateCaret</c>：聚焦后光标立即可见，之后按 <see cref="BlinkIntervalMs"/> 周期闪烁。
    /// 浏览器 DOM &lt;input&gt; 的 caretColor 已置透明，屏幕上只有这枚引擎自绘光标。
    /// </summary>
    public sealed partial class TextBox
    {
        /// <summary>闪烁半周期（毫秒）：亮、灭各占一个半周期。默认 530ms（≈ UGUI 的 0.85Hz）。</summary>
        public long BlinkIntervalMs { get; set; } = 530;

        /// <summary>当前是否处于“亮”半周期（供外部判断是否需要重绘）。</summary>
        public bool Visible => _focused && alpha > 0f;

        /// <summary>默认左边距，对齐 UGUI InputField 文本内边距。</summary>
        public const float DefaultPadLeft = 3f;

        private DateTime _blinkStartTime = DateTime.Now;
        private Texture2D _whitePixel;
        private float alpha = 0;
        private bool _focused;

        /// <summary>
        /// 通知画笔焦点状态变化。仅在“切换”那一刻生效（重复调用同一状态会被忽略），
        /// 因此聚焦时立即点亮并重新开始计时，失焦时熄灭——调用方可每帧放心调用。
        /// </summary>
        public void OnFocusChanged(bool focused)
        {
            if (focused == _focused) return;
            _focused = focused;
            if (focused)
            {
                _blinkStartTime = DateTime.Now;
                alpha = 1.0f;
            }
            else
            {
                alpha = 0f;
            }
        }

        private void Tick()
        {
            if (!_focused) { alpha = 0f; return; }
            var now = DateTime.Now;
            if ((now - _blinkStartTime).TotalMilliseconds >= BlinkIntervalMs)
            {
                alpha = alpha == 0 ? 1 : 0;
                _blinkStartTime = now;
            }
        }

        private static float Measure(IFont font, string s)
            => (font != null && !string.IsNullOrEmpty(s)) ? font.MeasureString(s).X : 0f;

        private static float YOf(int lineIndex, bool multiline, float lineH, Rectangle bounds)
            => multiline
                ? (lineIndex * lineH + 2f)
                : System.Math.Max(0f, (bounds.Height - lineH) / 2f);

        /// <summary>
        /// 计算光标位置（相对 bounds 左上角）。对齐 UGUI <c>GenerateCaret</c>：
        /// x 取 caretIndex 之前文本的测量宽度（光标落在该字符右侧）；
        /// 单行垂直居中，多行按字符所在行顶定位。
        /// </summary>
        public Vector2 GetPosition(IFont font, string text, int caretIndex, Rectangle bounds, bool multiline, float padLeft = DefaultPadLeft)
        {
            float lineH = font != null ? font.LineSpacing : 0f;

            if (string.IsNullOrEmpty(text) || caretIndex <= 0)
                return new Vector2(padLeft, YOf(0, multiline, lineH, bounds));

            string prefix = caretIndex >= text.Length ? text : text.Substring(0, caretIndex);

            if (!multiline)
                return new Vector2(padLeft + Measure(font, prefix), YOf(0, multiline, lineH, bounds));

            // 多行：定位到 prefix 的末行行内偏移
            string[] segs = prefix.Split('\n');
            int lineIndex = segs.Length - 1;
            return new Vector2(padLeft + Measure(font, segs[lineIndex]), YOf(lineIndex, multiline, lineH, bounds));
        }

        /// <summary>
        /// 绘制光标竖线（按当前闪烁相位决定是否可见）。调用方每帧调用即可看到闪烁；未聚焦时光标自动熄灭。
        /// </summary>
        public void Draw(SpriteBatch batch, GraphicsDevice device, IFont font, string text, int caretIndex,
                         Rectangle bounds, Color color, bool multiline, float padLeft = DefaultPadLeft)
        {
            Tick();
            if (alpha <= 0f) return;

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
