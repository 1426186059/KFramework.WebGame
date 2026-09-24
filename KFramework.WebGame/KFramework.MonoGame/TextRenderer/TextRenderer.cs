namespace KFramework.MonoGame
{
    /// <summary>
    /// 通用文本渲染底层库：对齐 System.Windows.Forms.TextRenderer 的 API 形态，
    /// 字体参数统一为 <see cref="IFont"/>（已光栅化的字形资源抽象）。
    ///
    ///   MeasureText(string text, IFont font[, Size proposedSize [, TextFormatFlags flags]])
    ///   MeasureText(GraphicsDevice dc, string text, IFont font[, Size proposedSize [, TextFormatFlags flags]])
    ///   DrawText(GraphicsDevice dc, string text, IFont font, Point pt, Color foreColor[, TextFormatFlags flags])
    ///   DrawText(GraphicsDevice dc, string text, IFont font, Rectangle bounds, Color foreColor[, TextFormatFlags flags])
    ///   DrawText(SpriteBatch batch, string text, IFont font, Rectangle bounds, Color foreColor, TextFormatFlags flags)
    ///
    /// <see cref="IFont"/> 的实现可以是 SpriteFont（系统字体）、BitmapFont（BMFont 图集）、KFont，
    /// 也可以是 <see cref="Font"/>——它是 System.Drawing.Font 风格的"描述符"（族名/字号/样式/单位，设备无关），
    /// 同时实现 IFont，度量与绘制时按设备解析（缓存）成 SpriteFont。
    ///
    /// 原版 WinForms 的 IDeviceContext 在此对应 <see cref="GraphicsDevice"/>（绘制到它当前绑定的渲染目标）。
    /// 清屏不属于本类职责（原版由控件自身 OnPaint 清屏），调用方请用 GraphicsDevice.Clear。
    /// </summary>
    public static class TextRenderer
    {
        // 交由 GraphicsDevice 版 DrawText 复用的批（延迟创建，避免每次分配）。
        private static SpriteBatch _sharedBatch;
        private static GraphicsDevice _sharedBatchDevice;
        // backColor 铺底用的 1x1 白纹。
        private static Texture2D _whitePixel;

        private static SpriteBatch GetSharedBatch(GraphicsDevice device)
        {
            if (_sharedBatch == null || !ReferenceEquals(_sharedBatchDevice, device))
            {
                _sharedBatch = new SpriteBatch(device);
                _sharedBatchDevice = device;
            }
            return _sharedBatch;
        }

        // ---------------- MeasureText ----------------

        public static Size MeasureText(string text, IFont font)
            => MeasureTextCore(text, font, int.MaxValue, TextFormatFlags.Default);

        public static Size MeasureText(string text, IFont font, Size proposedSize)
            => MeasureTextCore(text, font, proposedSize.Width, TextFormatFlags.Default);

        public static Size MeasureText(string text, IFont font, Size proposedSize, TextFormatFlags flags)
            => MeasureTextCore(text, font, proposedSize.Width, flags);

        public static Size MeasureText(GraphicsDevice dc, string text, IFont font)
            => MeasureTextCore(text, font, int.MaxValue, TextFormatFlags.Default);

        public static Size MeasureText(GraphicsDevice dc, string text, IFont font, Size proposedSize)
            => MeasureTextCore(text, font, proposedSize.Width, TextFormatFlags.Default);

        public static Size MeasureText(GraphicsDevice dc, string text, IFont font, Size proposedSize, TextFormatFlags flags)
            => MeasureTextCore(text, font, proposedSize.Width, flags);

        private static Size MeasureTextCore(string text, IFont font, int maxWidth, TextFormatFlags flags)
        {
            if (font == null || string.IsNullOrEmpty(text)) return Size.Empty;

            bool wordBreak = (flags & TextFormatFlags.WordBreak) != 0 && maxWidth > 0;
            if (!wordBreak)
            {
                Vector2 v = font.MeasureString(text);
                return new Size((int)Math.Ceiling(v.X), (int)Math.Ceiling(v.Y));
            }

            List<string> lines = WrapLines(text, font, maxWidth);
            int w = 0;
            foreach (string line in lines)
            {
                if (!string.IsNullOrEmpty(line))
                    w = Math.Max(w, (int)Math.Ceiling(font.MeasureString(line).X));
            }
            int h = (int)Math.Ceiling(lines.Count * font.LineSpacing);
            return new Size(w, h);
        }

        // ---------------- DrawText ----------------

        public static void DrawText(GraphicsDevice dc, string text, IFont font, Point pt, Color foreColor)
            => DrawText(dc, text, font, new Rectangle(pt.X, pt.Y, int.MaxValue, int.MaxValue), foreColor, TextFormatFlags.Default);

        public static void DrawText(GraphicsDevice dc, string text, IFont font, Point pt, Color foreColor, TextFormatFlags flags)
            => DrawText(dc, text, font, new Rectangle(pt.X, pt.Y, int.MaxValue, int.MaxValue), foreColor, flags);

        public static void DrawText(GraphicsDevice dc, string text, IFont font, Rectangle bounds, Color foreColor)
            => DrawText(dc, text, font, bounds, foreColor, TextFormatFlags.Default);

        public static void DrawText(GraphicsDevice dc, string text, IFont font, Rectangle bounds, Color foreColor, TextFormatFlags flags)
        {
            if (dc == null) return;
            SpriteBatch batch = GetSharedBatch(dc);
            batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp);
            try
            {
                DrawText(batch, text, font, bounds, foreColor, flags);
            }
            finally
            {
                batch.End();
            }
        }

        // ---- 带 backColor 的重载（对齐原版 WinForms：DrawText(..., foreColor, backColor[, flags])） ----

        public static void DrawText(GraphicsDevice dc, string text, IFont font, Point pt, Color foreColor, Color backColor)
            => DrawText(dc, text, font, pt, foreColor, backColor, TextFormatFlags.Default);

        public static void DrawText(GraphicsDevice dc, string text, IFont font, Point pt, Color foreColor, Color backColor, TextFormatFlags flags)
        {
            // 原版以 pt 为文本外接矩形左上角；铺背景时按实际文本尺寸收紧，避免铺满整个 MaxSize。
            Size sz = MeasureText(text, font, new Size(int.MaxValue, int.MaxValue), flags);
            DrawText(dc, text, font, new Rectangle(pt.X, pt.Y, sz.Width, sz.Height), foreColor, backColor, flags);
        }

        public static void DrawText(GraphicsDevice dc, string text, IFont font, Rectangle bounds, Color foreColor, Color backColor)
            => DrawText(dc, text, font, bounds, foreColor, backColor, TextFormatFlags.Default);

        public static void DrawText(GraphicsDevice dc, string text, IFont font, Rectangle bounds, Color foreColor, Color backColor, TextFormatFlags flags)
        {
            if (dc == null) return;
            SpriteBatch batch = GetSharedBatch(dc);
            batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp);
            try
            {
                FillBackground(batch, dc, bounds, backColor);
                DrawText(batch, text, font, bounds, foreColor, flags);
            }
            finally
            {
                batch.End();
            }
        }

        /// <summary>backColor 非透明时先用 1x1 白纹铺底（对齐原版 backColor 语义）。</summary>
        private static void FillBackground(SpriteBatch batch, GraphicsDevice device, Rectangle bounds, Color backColor)
        {
            if (backColor.A == 0) return;
            if (_whitePixel == null)
            {
                _whitePixel = device.CreateTexture(1, 1);
                _whitePixel.SetData(new byte[] { 255, 255, 255, 255 }, 0, 0, 1, 1);
            }
            batch.Draw(_whitePixel, bounds, backColor);
        }

        /// <summary>调用方已 Begin/End 批次时使用，避免重复开关。</summary>
        public static void DrawText(SpriteBatch batch, string text, IFont font, Rectangle bounds, Color foreColor, TextFormatFlags flags)
        {
            if (batch == null || font == null || string.IsNullOrEmpty(text)) return;

            bool hCenter = (flags & TextFormatFlags.HorizontalCenter) != 0;
            bool hRight = (flags & TextFormatFlags.Right) != 0;
            bool vCenter = (flags & TextFormatFlags.VerticalCenter) != 0;
            bool vBottom = (flags & TextFormatFlags.Bottom) != 0;
            bool wordBreak = (flags & TextFormatFlags.WordBreak) != 0;

            int maxWidth = wordBreak ? bounds.Width : 0;
            List<string> lines = WrapLines(text, font, maxWidth);

            float lineH = font.LineSpacing;
            float totalH = lineH * lines.Count;

            float startY = bounds.Y;
            if (vCenter) startY = bounds.Y + Math.Max(0f, (bounds.Height - totalH) / 2f);
            else if (vBottom) startY = bounds.Y + Math.Max(0f, bounds.Height - totalH);

            foreach (string line in lines)
            {
                float lineW = font.MeasureString(line).X;
                float tx = bounds.X;
                if (hCenter) tx = bounds.X + Math.Max(0f, (bounds.Width - lineW) / 2f);
                else if (hRight) tx = bounds.X + Math.Max(0f, bounds.Width - lineW);

                font.Draw(batch, line, new Vector2(tx, startY), foreColor, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
                startY += lineH;
            }
        }

        /// <summary>按显式 '\n' 切分；带 WordBreak 时在 maxWidth 内按词折行，连续中文/长串退化逐字折行。</summary>
        public static List<string> WrapLines(string text, IFont font, int maxWidth)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrEmpty(text) || font == null) return result;

            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Replace("\r", "");
                if (maxWidth <= 0) { result.Add(line); continue; }

                string[] words = line.Split(' ');
                System.Text.StringBuilder current = new System.Text.StringBuilder();
                foreach (string word in words)
                {
                    if (current.Length > 0 &&
                        font.MeasureString(current.ToString() + " " + word).X > maxWidth &&
                        font.MeasureString(word).X <= maxWidth)
                    {
                        result.Add(current.ToString());
                        current.Clear();
                        current.Append(word);
                    }
                    else if (font.MeasureString(word).X > maxWidth)
                    {
                        foreach (char ch in word)
                        {
                            if (current.Length > 0 && font.MeasureString(current.ToString() + ch).X > maxWidth)
                            {
                                result.Add(current.ToString());
                                current.Clear();
                            }
                            current.Append(ch);
                        }
                    }
                    else
                    {
                        current.Append(current.Length == 0 ? word : " " + word);
                    }
                }
                if (current.Length > 0) result.Add(current.ToString());
            }
            return result;
        }
    }
}
