using System;
using System.Collections.Generic;

namespace KFramework.MonoGame.TextRenderer
{
    /// <summary>
    /// 通用文本渲染底层库：对齐 System.Windows.Forms.TextRenderer 的 API 形态，绘制/度量一律接收 <see cref="Font"/>。
    ///
    ///   MeasureText(string text, Font font)
    ///   MeasureText(string text, Font font, Size proposedSize)
    ///   MeasureText(string text, Font font, Size proposedSize, TextFormatFlags flags)
    ///   MeasureText(GraphicsDevice dc, string text, Font font[, Size proposedSize [, TextFormatFlags flags]])
    ///   DrawText(GraphicsDevice dc, string text, Font font, Point pt, Color foreColor[, TextFormatFlags flags])
    ///   DrawText(GraphicsDevice dc, string text, Font font, Rectangle bounds, Color foreColor[, TextFormatFlags flags])
    ///   DrawText(SpriteBatch batch, string text, Font font, Rectangle bounds, Color foreColor, TextFormatFlags flags)
    ///
    /// 原版 WinForms 的 IDeviceContext 在此对应 <see cref="GraphicsDevice"/>（绘制到它当前绑定的渲染目标）；
    /// 若调用方已自行 Begin/End 了 SpriteBatch，可用 batch 版重载避免重复开关批次。
    /// 清屏不属于本类职责（原版由控件自身 OnPaint 清屏），调用方请用 GraphicsDevice.Clear。
    /// </summary>
    public static class TextRenderer
    {
        // Font -> SpriteFont 缓存（按 族名|字号|单位|样式）。引擎通常只有一个 GraphicsDevice，
        // 若设备变化则整体失效重建。
        private static readonly Dictionary<string, SpriteFont> _fontCache = new Dictionary<string, SpriteFont>();
        private static GraphicsDevice _cacheDevice;
        // 交由 GraphicsDevice 版 DrawText 复用的批（延迟创建，避免每次分配）。
        private static SpriteBatch _sharedBatch;
        private static GraphicsDevice _sharedBatchDevice;

        internal static SpriteFont GetSpriteFont(GraphicsDevice device, Font font)
        {
            if (device == null || font == null) return null;

            if (!ReferenceEquals(_cacheDevice, device))
            {
                _fontCache.Clear();
                _cacheDevice = device;
            }

            string key = $"{font.Name}|{font.Size}|{(int)font.Unit}|{(int)font.Style}";
            if (_fontCache.TryGetValue(key, out SpriteFont cached)) return cached;

            string family = font.Name;
            if (family.IndexOf("sans-serif", StringComparison.OrdinalIgnoreCase) < 0 &&
                family.IndexOf("serif", StringComparison.OrdinalIgnoreCase) < 0 &&
                family.IndexOf("monospace", StringComparison.OrdinalIgnoreCase) < 0)
                family += ", sans-serif";

            SpriteFont sf = new SpriteFont(device, font.GetPixelSize(), family, font.Bold);
            _fontCache[key] = sf;
            return sf;
        }

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

        public static Size MeasureText(string text, Font font)
            => MeasureTextCore(null, text, font, int.MaxValue, TextFormatFlags.Default);

        public static Size MeasureText(string text, Font font, Size proposedSize)
            => MeasureTextCore(null, text, font, proposedSize.Width, TextFormatFlags.Default);

        public static Size MeasureText(string text, Font font, Size proposedSize, TextFormatFlags flags)
            => MeasureTextCore(null, text, font, proposedSize.Width, flags);

        public static Size MeasureText(GraphicsDevice dc, string text, Font font)
            => MeasureTextCore(dc, text, font, int.MaxValue, TextFormatFlags.Default);

        public static Size MeasureText(GraphicsDevice dc, string text, Font font, Size proposedSize)
            => MeasureTextCore(dc, text, font, proposedSize.Width, TextFormatFlags.Default);

        public static Size MeasureText(GraphicsDevice dc, string text, Font font, Size proposedSize, TextFormatFlags flags)
            => MeasureTextCore(dc, text, font, proposedSize.Width, flags);

        private static Size MeasureTextCore(GraphicsDevice dc, string text, Font font, int maxWidth, TextFormatFlags flags)
        {
            if (font == null || string.IsNullOrEmpty(text)) return Size.Empty;

            SpriteFont sf = GetSpriteFont(dc ?? _cacheDevice, font);
            if (sf == null) return Size.Empty;

            bool wordBreak = (flags & TextFormatFlags.WordBreak) != 0 && maxWidth > 0;
            if (!wordBreak)
            {
                Vector2 v = sf.MeasureString(text);
                return new Size((int)Math.Ceiling(v.X), (int)Math.Ceiling(v.Y));
            }

            List<string> lines = WrapLines(text, sf, maxWidth);
            int w = 0;
            foreach (string line in lines)
            {
                if (!string.IsNullOrEmpty(line))
                    w = Math.Max(w, (int)Math.Ceiling(sf.MeasureString(line).X));
            }
            int h = (int)Math.Ceiling(lines.Count * sf.LineHeight);
            return new Size(w, h);
        }

        // ---------------- DrawText ----------------

        public static void DrawText(GraphicsDevice dc, string text, Font font, Point pt, Color foreColor)
            => DrawText(dc, text, font, new Rectangle(pt.X, pt.Y, int.MaxValue, int.MaxValue), foreColor, TextFormatFlags.Default);

        public static void DrawText(GraphicsDevice dc, string text, Font font, Point pt, Color foreColor, TextFormatFlags flags)
            => DrawText(dc, text, font, new Rectangle(pt.X, pt.Y, int.MaxValue, int.MaxValue), foreColor, flags);

        public static void DrawText(GraphicsDevice dc, string text, Font font, Rectangle bounds, Color foreColor)
            => DrawText(dc, text, font, bounds, foreColor, TextFormatFlags.Default);

        public static void DrawText(GraphicsDevice dc, string text, Font font, Rectangle bounds, Color foreColor, TextFormatFlags flags)
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

        /// <summary>调用方已 Begin/End 批次时使用，避免重复开关。</summary>
        public static void DrawText(SpriteBatch batch, string text, Font font, Rectangle bounds, Color foreColor, TextFormatFlags flags)
        {
            if (batch == null || font == null || string.IsNullOrEmpty(text)) return;

            SpriteFont sf = GetSpriteFont(_cacheDevice, font);
            if (sf == null) return;

            bool hCenter = (flags & TextFormatFlags.HorizontalCenter) != 0;
            bool hRight = (flags & TextFormatFlags.Right) != 0;
            bool vCenter = (flags & TextFormatFlags.VerticalCenter) != 0;
            bool vBottom = (flags & TextFormatFlags.Bottom) != 0;
            bool wordBreak = (flags & TextFormatFlags.WordBreak) != 0;

            int maxWidth = wordBreak ? bounds.Width : 0;
            List<string> lines = WrapLines(text, sf, maxWidth);

            float lineH = sf.LineHeight;
            float totalH = lineH * lines.Count;

            float startY = bounds.Y;
            if (vCenter) startY = bounds.Y + Math.Max(0f, (bounds.Height - totalH) / 2f);
            else if (vBottom) startY = bounds.Y + Math.Max(0f, bounds.Height - totalH);

            foreach (string line in lines)
            {
                float lineW = sf.MeasureString(line).X;
                float tx = bounds.X;
                if (hCenter) tx = bounds.X + Math.Max(0f, (bounds.Width - lineW) / 2f);
                else if (hRight) tx = bounds.X + Math.Max(0f, bounds.Width - lineW);

                sf.Draw(batch, line, new Vector2(tx, startY), foreColor, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
                startY += lineH;
            }
        }

        /// <summary>按显式 '\n' 切分；带 WordBreak 时在 maxWidth 内按词折行，连续中文/长串退化逐字折行。</summary>
        public static List<string> WrapLines(string text, SpriteFont sf, int maxWidth)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrEmpty(text) || sf == null) return result;

            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Replace("\r", "");
                if (maxWidth <= 0) { result.Add(line); continue; }

                string[] words = line.Split(' ');
                System.Text.StringBuilder current = new System.Text.StringBuilder();
                foreach (string word in words)
                {
                    if (current.Length > 0 &&
                        sf.MeasureString(current.ToString() + " " + word).X > maxWidth &&
                        sf.MeasureString(word).X <= maxWidth)
                    {
                        result.Add(current.ToString());
                        current.Clear();
                        current.Append(word);
                    }
                    else if (sf.MeasureString(word).X > maxWidth)
                    {
                        foreach (char ch in word)
                        {
                            if (current.Length > 0 && sf.MeasureString(current.ToString() + ch).X > maxWidth)
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
