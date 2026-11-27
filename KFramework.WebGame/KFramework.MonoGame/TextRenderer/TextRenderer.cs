namespace KFramework.MonoGame
{
    public static class TextRenderer
    {
        // backColor 铺底用的 1x1 白纹（按需创建；只此一项共享资源，与批无关）。
        private static Texture2D _whitePixel;

        // ---------------- MeasureText ----------------

        public static Size MeasureText(string text, IFont font)
        {
            return MeasureTextCore(text, font, int.MaxValue, HorizontalWrapMode.Overflow);
        }

        public static Size MeasureText(string text, IFont font, Size proposedSize)
        {
            return MeasureTextCore(text, font, proposedSize.Width, HorizontalWrapMode.Overflow);
        }

        /// <summary>度量文本：<paramref name="hWrap"/> = Wrap 时按 proposedSize.Width 折行后再量（取最宽行 + 行数高）。</summary>
        public static Size MeasureText(string text, IFont font, Size proposedSize, HorizontalWrapMode hWrap)
        {
            return MeasureTextCore(text, font, proposedSize.Width, hWrap);
        }

        private static Size MeasureTextCore(string text, IFont font, int maxWidth, HorizontalWrapMode hWrap)
        {
            if (font == null || string.IsNullOrEmpty(text))
            {
                return Size.Empty;
            }

            bool wrap = hWrap == HorizontalWrapMode.Wrap && maxWidth > 0;
            if (!wrap)
            {
                Vector2 v = font.MeasureString(text);
                return new Size((int)Math.Ceiling(v.X), (int)Math.Ceiling(v.Y));
            }

            List<string> lines = WrapLines(text, font, maxWidth);
            int w = 0;
            foreach (string line in lines)
            {
                if (!string.IsNullOrEmpty(line))
                {
                    w = Math.Max(w, (int)Math.Ceiling(font.MeasureString(line).X));
                }
            }
            int h = (int)Math.Ceiling(lines.Count * font.LineSpacing);
            return new Size(w, h);
        }

        // ---------------- DrawText（一律用调用方传入的批） ----------------

        /// <summary>在给定矩形内绘制普通文本；<paramref name="batch"/> 必须处于 Begin / End 之间。
        /// 富文本请用 <see cref="RichTextObject"/>。</summary>
        public static void DrawText(SpriteBatch batch, string text, IFont font, RectangleF bounds, Color foreColor,
                                    TextAnchor anchor = TextAnchor.UpperLeft,
                                    HorizontalWrapMode hWrap = HorizontalWrapMode.Overflow,
                                    VerticalWrapMode vWrap = VerticalWrapMode.Overflow)
        {
            if (batch == null || font == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            int maxWidth = hWrap == HorizontalWrapMode.Wrap ? (int)bounds.Width : 0;
            List<string> lines = WrapLines(text, font, maxWidth);

            float lineH = font.LineSpacing;
            float totalH = lineH * lines.Count;

            int row = (int)anchor / 3;
            int col = (int)anchor % 3;
            
            float startY = bounds.Y; //上对齐
            if (row == 1) //中间对齐
            {
                startY = bounds.Y + (bounds.Height - totalH) / 2f;
            }
            else if (row == 2) //下对齐
            {
                startY = bounds.Y + (bounds.Height - totalH);
            }

            float clipBottom = vWrap == VerticalWrapMode.Truncate ? 
                bounds.Y + bounds.Height : float.MaxValue;

            foreach (string line in lines)
            {
                if (startY + lineH > clipBottom)
                {
                    break;
                }

                float lineW = font.MeasureString(line).X;
                float tx = bounds.X;
                if (col == 1)
                {
                    tx = bounds.X + (bounds.Width - lineW) / 2f;
                }
                else if (col == 2)
                {
                    tx = bounds.X + (bounds.Width - lineW);
                }
                font.Draw(batch, line, new Vector2(tx, startY), foreColor, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
                startY += lineH;
            }

        }

        /// <summary>带背景铺底的重载：<paramref name="backColor"/> 非透明时先用 1x1 白纹把矩形填满，再画文本。</summary>
        public static void DrawText(SpriteBatch batch, string text, IFont font, RectangleF bounds, Color foreColor, Color backColor,
                                    TextAnchor anchor = TextAnchor.UpperLeft,
                                    HorizontalWrapMode hWrap = HorizontalWrapMode.Overflow,
                                    VerticalWrapMode vWrap = VerticalWrapMode.Overflow)
        {
            if (batch == null)
            {
                return;
            }
            FillBackground(batch, bounds, backColor);
            DrawText(batch, text, font, bounds, foreColor, anchor, hWrap, vWrap);
        }

        private static void FillBackground(SpriteBatch batch, RectangleF bounds, Color backColor)
        {
            if (backColor.A == 0)
            {
                return;
            }

            if (_whitePixel == null)
            {
                _whitePixel = batch.GraphicsDevice.CreateTexture(1, 1);
                _whitePixel.SetData(new byte[] { 255, 255, 255, 255 }, 0, 0, 1, 1);
            }
            batch.Draw(_whitePixel, bounds, null, backColor);
        }

        public static List<string> WrapLines(string text, IFont font, int maxWidth)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrEmpty(text) || font == null)
            {
                return result;
            }

            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Replace("\r", "");
                if (line.Length == 0)
                {
                    result.Add(string.Empty);
                    continue;
                }

                if (maxWidth <= 0) 
                { 
                    result.Add(line); 
                    continue; 
                }

                int lead = 0;
                while (lead < line.Length && line[lead] == ' ')
                {
                    lead++;
                }
                string leadStr = lead > 0 ? new string(' ', lead) : string.Empty;

                string[] words = line.Split(' ');
                System.Text.StringBuilder current = new System.Text.StringBuilder();
                bool firstLine = true;
                foreach (string word in words)
                {
                    if (current.Length > 0 &&
                        font.MeasureString(current.ToString() + " " + word).X > maxWidth &&
                        font.MeasureString(word).X <= maxWidth)
                    {
                        result.Add(firstLine ? leadStr + current.ToString() : current.ToString());
                        firstLine = false;
                        current.Clear();
                        current.Append(word);
                    }
                    else if (font.MeasureString(word).X > maxWidth)
                    {
                        foreach (char ch in word)
                        {
                            if (current.Length > 0 && font.MeasureString(current.ToString() + ch).X > maxWidth)
                            {
                                result.Add(firstLine ? leadStr + current.ToString() : current.ToString());
                                firstLine = false;
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

                if (current.Length > 0)
                {
                    result.Add(firstLine ? leadStr + current.ToString() : current.ToString());
                }
            }
            return result;
        }
    }
}
