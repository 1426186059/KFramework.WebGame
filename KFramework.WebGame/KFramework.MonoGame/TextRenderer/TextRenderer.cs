using System.Text;

namespace KFramework.MonoGame
{
    public static partial class TextRenderer
    {
        // backColor 铺底用的 1x1 白纹（按需创建；只此一项共享资源，与批无关）。
        private static Texture2D _whitePixel;

        private static readonly char[] WrapSpaces = new string(' ', 64).ToCharArray();
        private static readonly ListStringBuilder _wrapLinesResult = new ListStringBuilder();


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

            ListStringBuilder lines = WrapLines(text, font, maxWidth);
            int w = 0;
            foreach (System.Text.StringBuilder line in lines)
            {
                if (line.Length > 0)
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
        public static void DrawText(
            SpriteBatch batch,
            string text,
            IFont font,
            RectangleF bounds,
            Color foreColor,
            TextStyle style)
        {
            if (batch == null || font == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            TextAnchor anchor = style.Anchor;
            HorizontalWrapMode hWrap = style.HorizontalWrap;
            VerticalWrapMode vWrap = style.VerticalWrap;
            int maxWidth = hWrap == HorizontalWrapMode.Wrap ? (int)bounds.Width : 0;
            ListStringBuilder lines = WrapLines(text, font, maxWidth);

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

            foreach (StringBuilder line in lines)
            {
                if (startY + lineH > clipBottom)
                {
                    break;
                }

                float lineW = font.MeasureString(line).X;
                //水平左对齐
                float tx = bounds.X;
                if (col == 1)
                {
                    //水平居中对齐
                    tx = bounds.X + (bounds.Width - lineW) / 2f;
                }
                else if (col == 2)
                {
                    //水平右对齐
                    tx = bounds.X + (bounds.Width - lineW);
                }
                font.Draw(batch, line, new Vector2(tx, startY), foreColor, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
                startY += lineH;
            }

        }

        /// <summary>带背景铺底的重载：<paramref name="backColor"/> 非透明时先用 1x1 白纹把矩形填满，再画文本。</summary>
        public static void DrawText(
            SpriteBatch batch, 
            string text, 
            IFont font, 
            RectangleF bounds, 
            Color foreColor, 
            Color backColor,
            TextStyle style)
        {
            if (batch == null)
            {
                return;
            }
            FillBackground(batch, bounds, backColor);
            DrawText(batch, text, font, bounds, foreColor, style);
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

        public static ListStringBuilder WrapLines(ReadOnlySpan<char> text, IFont font, int maxWidth)
        {
            ListStringBuilder result = _wrapLinesResult;
            result.Reset();
            if (text.IsEmpty || font == null)
            {
                return result;
            }

            int lineStart = 0;
            while (lineStart <= text.Length)
            {
                int relNl = text.Slice(lineStart).IndexOf('\n');
                ReadOnlySpan<char> raw = relNl < 0 ? text.Slice(lineStart) : text.Slice(lineStart, relNl);

                // 去掉行尾 \r（原 raw.Replace("\r", "") 只处理行尾这一个）
                if (raw.Length > 0 && raw[raw.Length - 1] == '\r')
                {
                    raw = raw.Slice(0, raw.Length - 1);
                }

                if (raw.IsEmpty)
                {
                    result.AddLine();
                }
                else if (maxWidth <= 0)
                {
                    result.AddLine().Append(raw);
                }
                else
                {
                    int lead = 0;
                    while (lead < raw.Length && raw[lead] == ' ')
                    {
                        lead++;
                    }

                    System.Text.StringBuilder current = result.AddLine();
                    bool firstLine = true;
                    int wordStart = 0;
                    while (wordStart <= raw.Length)
                    {
                        int relSp = raw.Slice(wordStart).IndexOf(' ');
                        ReadOnlySpan<char> word = relSp < 0 ? raw.Slice(wordStart) : raw.Slice(wordStart, relSp);
                        float wordW = font.MeasureString(word).X;
                        // 原逻辑：current + " " + word 超宽且 word 自身不超宽 → 换行。用 Append + 长度回退避免 ToString。
                        if (current.Length > 0 && wordW <= maxWidth)
                        {
                            int saved = current.Length;
                            current.Append(' ').Append(word);
                            bool overflow = font.MeasureString(current).X > maxWidth;
                            current.Length = saved;
                            if (overflow)
                            {
                                if (firstLine && lead > 0)
                                {
                                    current.Insert(0, WrapSpaces, 0, lead);
                                }
                                firstLine = false;
                                current = result.AddLine().Append(word);
                                if (relSp < 0)
                                {
                                    break;
                                }
                                wordStart += relSp + 1;
                                continue;
                            }
                        }

                        if (wordW > maxWidth)
                        {
                            foreach (char ch in word)
                            {
                                if (current.Length > 0)
                                {
                                    int saved = current.Length;
                                    current.Append(ch);
                                    bool overflow = font.MeasureString(current).X > maxWidth;
                                    current.Length = saved;
                                    if (overflow)
                                    {
                                        if (firstLine && lead > 0)
                                        {
                                            current.Insert(0, WrapSpaces, 0, lead);
                                        }
                                        firstLine = false;
                                        current = result.AddLine();
                                    }
                                }
                                current.Append(ch);
                            }
                        }
                        else
                        {
                            if (current.Length > 0)
                            {
                                current.Append(' ');
                            }
                            current.Append(word);
                        }

                        if (relSp < 0)
                        {
                            break;
                        }
                        wordStart += relSp + 1;
                    }

                    if (current.Length > 0 && firstLine && lead > 0)
                    {
                        current.Insert(0, WrapSpaces, 0, lead);
                    }
                }

                if (relNl < 0)
                {
                    break;
                }
                lineStart += relNl + 1;
            }
            return result;
        }
    }
}
