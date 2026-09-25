namespace KFramework.MonoGame
{
    /// <summary>
    /// 输入光标：<b>完全由引擎自绘</b>（与 UGUI <c>InputField</c> 的
    /// <c>OnPopulateMesh / GenerateCaret</c> 思路一致：以 text 长度为锚点，
    /// 用字体度量定位光标 x，按字符所在行定位多行 y；浏览器 DOM &lt;input&gt; 仅作 IME / 键盘捕获，
    /// 其 <c>caretColor</c> 已被置为透明，故屏幕上只有这一枚引擎绘制的光标——不存在"多画一个光标"的问题）。
    ///
    /// 闪烁由 <see cref="Draw"/> 内部自行驱动——调用方每帧调 <see cref="Draw"/> 即可看到光标闪烁，
    /// 无需外部喂时间。若控件只在"亮/灭翻转"那一刻才重建纹理，可用 <see cref="Tick"/> 判断是否变化。
    /// </summary>
    internal sealed class TextCaret
    {
        /// <summary>闪烁半周期（毫秒）：亮、灭各占一个半周期。默认 530ms（≈ UGUI 的 0.85Hz）。</summary>
        public long BlinkIntervalMs { get; set; } = 530;

        /// <summary>当前是否处于"亮"半周期。</summary>
        public bool Visible { get; private set; }

        /// <summary>默认左边距，对齐 UGUI InputField 文本内边距。</summary>
        public const float DefaultPadLeft = 3f;

        private DateTime _blinkStartTime = DateTime.Now;
        private Texture2D _whitePixel;
        private float alpha = 0;

        /// <summary>
        /// 推进闪烁节拍。返回 true 表示可见性发生了翻转（控件可据此决定是否重建纹理）。
        /// 对齐 UGUI：聚焦后光标立即可见，之后按周期闪烁（前半个周期点亮）。
        /// </summary>
        private void Tick()
        {
            var now = DateTime.Now;
            if ((now - _blinkStartTime).TotalMilliseconds >= BlinkIntervalMs)
            {
                alpha = alpha == 0 ? 1 : 0;
                _blinkStartTime = now;
            }
        }

        /// <summary>重置闪烁节拍（重新聚焦时让光标立即亮起）。</summary>
        public void Reset()
        {
            _blinkStartTime = DateTime.Now;
            alpha = 1.0f;
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
        /// 每帧调用：内部先推进闪烁节拍，再按 <see cref="ShouldDraw"/> 绘制光标竖线。
        /// 未聚焦时熄灭且不绘制。
        /// </summary>
        public void Draw(SpriteBatch batch, GraphicsDevice device, IFont font, string text, int caretIndex,
                         Rectangle bounds, bool multiline, float padLeft = DefaultPadLeft)
        {
            Tick();

            Color color = alpha > 0 ? Color.White : Color.Transparent;
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
