namespace KFramework.MonoGame
{
    /// <summary>
    /// 输入光标：<b>完全由引擎自绘</b>（与 UGUI <c>InputField</c> 的
    /// <c>OnPopulateMesh / GenerateCaret / GenerateHighlight</c> 思路一致：以 text 长度为锚点，
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

        /// <summary>是否应绘制光标（等于 <see cref="Visible"/>）。</summary>
        public bool ShouldDraw => Visible;

        /// <summary>默认左边距，对齐 UGUI InputField 文本内边距。</summary>
        public const float DefaultPadLeft = 3f;

        private long _blinkStart = long.MinValue;
        private Texture2D _whitePixel;

        /// <summary>
        /// 推进闪烁节拍。返回 true 表示可见性发生了翻转（控件可据此决定是否重建纹理）。
        /// 对齐 UGUI：聚焦后光标立即可见，之后按周期闪烁（前半个周期点亮）。
        /// </summary>
        private bool Tick(bool focused)
        {
            if (!focused)
            {
                bool was = Visible;
                Visible = false;
                _blinkStart = long.MinValue;
                return was;
            }

            long now = System.Environment.TickCount64;
            if (_blinkStart == long.MinValue)
            {
                _blinkStart = now;
                Visible = true;
                return true;
            }

            long period = BlinkIntervalMs * 2;            // 完整周期 = 亮 + 灭
            long t = (now - _blinkStart) % period;
            bool on = t < BlinkIntervalMs;                // UGUI：周期前半段为"亮"
            if (on != Visible)
            {
                Visible = on;
                return true;
            }
            return false;
        }

        /// <summary>重置闪烁节拍（重新聚焦时让光标立即亮起）。</summary>
        public void Reset()
        {
            _blinkStart = long.MinValue;
            Visible = false;
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

        /// <summary>
        /// 选区高亮：对齐 UGUI <c>GenerateHighlight</c>，仅画半透明矩形，<b>不画光标</b>
        /// （调用方在有选区时应改调本方法而非 <see cref="DrawCaret"/>）。
        /// </summary>
        public void DrawSelection(SpriteBatch batch, GraphicsDevice device, IFont font, string text,
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
