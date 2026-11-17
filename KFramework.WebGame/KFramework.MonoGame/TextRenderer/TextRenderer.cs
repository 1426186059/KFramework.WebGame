namespace KFramework.MonoGame
{
    /// <summary>
    /// 通用文本渲染底层库。字体参数统一为 <see cref="IFont"/>（已光栅化的字形资源抽象）。
    /// <para>
    /// <b>本类不持有、也不创建 SpriteBatch</b>：所有绘制入口都要求调用方传入自己的
    /// <see cref="SpriteBatch"/>（外部批），由调用方决定何时 <c>Begin</c> / <c>End</c>。
    /// 这样批的材质 / 变换 / 生命周期完全归调用方掌控，也不会冒出一个"隐式共享批"与外部批抢状态。
    /// <c>MeasureText</c> 与批无关，直接量即可。
    /// </para>
    /// <para>
    /// <b>排版风格用 Unity 的三元组</b>：<see cref="TextAnchor"/>（对齐）+
    /// <see cref="HorizontalWrapMode"/>（水平折行）+ <see cref="VerticalWrapMode"/>（垂直截断）。
    /// 引擎不认识 WinForms 的 TextFormatFlags —— 需要的调用方（如传奇客户端）
    /// 自行提供 GDI 标志到三元组的折算即可。
    /// </para>
    /// <para>
    ///   MeasureText(string text, IFont font[, Size proposedSize[, HorizontalWrapMode hWrap]])
    ///   DrawText(SpriteBatch batch, string text, IFont font, Rectangle bounds, Color foreColor
    ///            [, Color backColor][, TextAnchor anchor[, HorizontalWrapMode hWrap[, VerticalWrapMode vWrap]]])
    /// </para>
    /// <see cref="IFont"/> 的实现可以是 SpriteFont（系统字体）、BitmapFont（BMFont 图集）、KFont，
    /// 也可以是 <see cref="Font"/>——它是 System.Drawing.Font 风格的"描述符"（族名/字号/样式/单位，设备无关），
    /// 同时实现 IFont，度量与绘制时按设备解析（缓存）成 SpriteFont。
    ///
    /// 清屏不属于本类职责（原版由控件自身 OnPaint 清屏），调用方请用 GraphicsDevice.Clear。
    /// </summary>
    public static class TextRenderer
    {
        // backColor 铺底用的 1x1 白纹（按需创建；只此一项共享资源，与批无关）。
        private static Texture2D _whitePixel;

        // ---------------- MeasureText ----------------

        public static Size MeasureText(string text, IFont font)
        {
            return MeasureTextCore(text, font, int.MaxValue, HorizontalWrapMode.Overflow, false);
        }

        public static Size MeasureText(string text, IFont font, Size proposedSize)
        {
            return MeasureTextCore(text, font, proposedSize.Width, HorizontalWrapMode.Overflow, false);
        }

        /// <summary>度量文本：<paramref name="hWrap"/> = Wrap 时按 proposedSize.Width 折行后再量（取最宽行 + 行数高）。</summary>
        public static Size MeasureText(string text, IFont font, Size proposedSize, HorizontalWrapMode hWrap)
        {
            return MeasureTextCore(text, font, proposedSize.Width, hWrap, false);
        }

        /// <summary>富文本度量：<paramref name="richText"/>=true 时先剥离 &lt;color&gt;/&lt;link&gt; 等标签再量。</summary>
        public static Size MeasureText(string text, IFont font, Size proposedSize, HorizontalWrapMode hWrap, bool richText)
        {
            return MeasureTextCore(text, font, proposedSize.Width, hWrap, richText);
        }

        private static Size MeasureTextCore(string text, IFont font, int maxWidth, HorizontalWrapMode hWrap, bool richText)
        {
            if (font == null || string.IsNullOrEmpty(text))
            {
                return Size.Empty;
            }

            RichTextDocument doc = richText ? RichTextDocument.Parse(text) : null;
            if (doc != null)
            {
                text = doc.PlainText;   // 度量一律用去标签的纯文本
            }
            if (string.IsNullOrEmpty(text))
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

        /// <summary>在给定矩形内绘制文本；<paramref name="batch"/> 必须处于 Begin / End 之间。</summary>
        public static void DrawText(SpriteBatch batch, string text, IFont font, RectangleF bounds, Color foreColor,
                                    TextAnchor anchor = TextAnchor.UpperLeft,
                                    HorizontalWrapMode hWrap = HorizontalWrapMode.Overflow,
                                    VerticalWrapMode vWrap = VerticalWrapMode.Overflow)
        {
            DrawText(batch, text, font, bounds, foreColor, anchor, hWrap, vWrap, false);
        }

        /// <summary>富文本绘制：<paramref name="richText"/>=true 时解析 &lt;color&gt;/&lt;link&gt;/&lt;b&gt;/&lt;i&gt;/&lt;u&gt;/&lt;size&gt; 标签逐段上色。
        /// <paramref name="outlineMode"/>=true 时只画去标签的纯文本、忽略段颜色（供描边用统一色）。
        /// 超链接点击命中请用 <see cref="GetLinkAt"/>，它复用本方法的排版逻辑。</summary>
        public static void DrawText(SpriteBatch batch, string text, IFont font, RectangleF bounds, Color foreColor,
                                    TextAnchor anchor, HorizontalWrapMode hWrap, VerticalWrapMode vWrap, bool richText,
                                    bool outlineMode = false)
        {
            if (batch == null || font == null || string.IsNullOrEmpty(text))
            {
                return;
            }
            if (richText)
            {
                RichTextDocument doc = RichTextDocument.Parse(text);
                if (doc != null && !string.IsNullOrEmpty(doc.PlainText))
                {
                    DrawRichCore(batch, doc, font, bounds, foreColor, anchor, hWrap, vWrap, outlineMode);
                    return;
                }
                if (doc != null)
                {
                    return; // 全是标签，无可见文本
                }
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

        /// <summary>富文本排版：逐字符折行 → 对齐 → 逐样式段切分，产出每段的文本、样式与矩形。
        /// 绘制（<see cref="DrawRichCore"/>）与超链接命中（<see cref="GetLinkAt"/>）共用，保证命中区与画面一致。</summary>
        private static List<RichTextRun> LayoutRich(RichTextDocument doc, IFont font, RectangleF bounds,
                                                    TextAnchor anchor, HorizontalWrapMode hWrap, VerticalWrapMode vWrap)
        {
            string text = doc.PlainText;
            RichTextStyle[] styles = doc.Styles;
            bool wrap = hWrap == HorizontalWrapMode.Wrap && bounds.Width > 0;
            float lineH = font.LineSpacing;

            // 1) 逐字符折行，记录每行的 [start,end)、宽度与该行最大缩放
            // 行高按最大 Scale 撑开，避免放大的字（<size> / 粗体）压到下一行。
            var spans = new List<(int start, int end, float width, float maxScale)>();
            int lineStart = 0;
            float lineW = 0;
            float lineMaxScale = 1f;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch == '\n')
                {
                    spans.Add((lineStart, i, lineW, lineMaxScale));
                    lineStart = i + 1;
                    lineW = 0;
                    lineMaxScale = 1f;
                    continue;
                }
                float cw = font.MeasureString(ch.ToString()).X * styles[i].Scale;
                if (wrap && lineW + cw > bounds.Width && i > lineStart)
                {
                    spans.Add((lineStart, i, lineW, lineMaxScale));
                    lineStart = i;
                    lineW = 0;
                    lineMaxScale = 1f;
                }
                lineW += cw;
                lineMaxScale = Math.Max(lineMaxScale, styles[i].Scale);
            }
            spans.Add((lineStart, text.Length, lineW, lineMaxScale));

            // 2) 对齐：每行行高 = 基础行高 * 该行最大缩放，累加得到总高
            int row = (int)anchor / 3;
            int col = (int)anchor % 3;
            float totalH = 0f;
            foreach (var span in spans)
            {
                totalH += lineH * span.maxScale;
            }
            float startY = bounds.Y;
            if (row == 1)
            {
                startY = bounds.Y + (bounds.Height - totalH) / 2f;
            }
            else if (row == 2)
            {
                startY = bounds.Y + (bounds.Height - totalH);
            }
            float clipBottom = vWrap == VerticalWrapMode.Truncate ? bounds.Y + bounds.Height : float.MaxValue;

            // 3) 逐行、逐样式段切分
            var runs = new List<RichTextRun>();
            foreach (var span in spans)
            {
                float rowH = lineH * span.maxScale;
                if (startY + rowH > clipBottom)
                {
                    break;
                }
                float tx = bounds.X;
                if (col == 1)
                {
                    tx = bounds.X + (bounds.Width - span.width) / 2f;
                }
                else if (col == 2)
                {
                    tx = bounds.X + (bounds.Width - span.width);
                }

                int i = span.start;
                while (i < span.end)
                {
                    if (text[i] == '\n')
                    {
                        i++;
                        continue;
                    }
                    RichTextStyle st = styles[i];
                    int j = i + 1;
                    while (j < span.end && text[j] != '\n' && SameStyle(styles[j], st))
                    {
                        j++;
                    }
                    string seg = text.Substring(i, j - i);
                    float segW = font.MeasureString(seg).X * st.Scale;
                    // Height 存该段自身缩放后的高度：下划线与命中区都按各自字号贴字。
                    // 行与行的间距另用 rowH（该行最大缩放）控制，互不干扰。
                    runs.Add(new RichTextRun(seg, st, tx, startY, segW, lineH * st.Scale));
                    tx += segW;
                    i = j;
                }
                startY += rowH;
            }
            return runs;
        }

        /// <summary>富文本核心：用 <see cref="LayoutRich"/> 的结果逐段绘制（颜色/超链接/下划线/字号）。</summary>
        private static void DrawRichCore(SpriteBatch batch, RichTextDocument doc, IFont font, RectangleF bounds,
                                         Color foreColor, TextAnchor anchor, HorizontalWrapMode hWrap, VerticalWrapMode vWrap,
                                         bool outlineMode)
        {
            List<RichTextRun> runs = LayoutRich(doc, font, bounds, anchor, hWrap, vWrap);
            foreach (RichTextRun run in runs)
            {
                Color segColor = outlineMode ? foreColor : run.Style.EffectiveColor(foreColor);
                font.Draw(batch, run.Text, new Vector2(run.X, run.Y), segColor,
                          0f, Vector2.Zero, run.Style.Scale, SpriteEffects.None, 0f);
                if (!outlineMode && run.Style.Underline)
                {
                    DrawUnderline(batch, run.X, run.Y + run.Height - 1, run.Width, segColor);
                }
            }
        }

        /// <summary>超链接点击回调：传入与绘制时相同的排版参数与鼠标坐标，命中某个 &lt;link&gt;/&lt;a href&gt; 段时返回其 url，
        /// 否则返回 null。内部复用 <see cref="LayoutRich"/>，因此命中区与实际画面逐像素一致。</summary>
        public static string GetLinkAt(string text, IFont font, RectangleF bounds,
                                       TextAnchor anchor, HorizontalWrapMode hWrap, VerticalWrapMode vWrap,
                                       float x, float y)
        {
            if (font == null || string.IsNullOrEmpty(text))
            {
                return null;
            }
            RichTextDocument doc = RichTextDocument.Parse(text);
            if (doc == null)
            {
                return null;
            }
            List<RichTextRun> runs = LayoutRich(doc, font, bounds, anchor, hWrap, vWrap);
            foreach (RichTextRun run in runs)
            {
                if (run.Style.Link == null)
                {
                    continue;
                }
                if (x >= run.X && x <= run.X + run.Width && y >= run.Y && y <= run.Y + run.Height)
                {
                    return run.Style.Link;
                }
            }
            return null;
        }

        private static bool SameStyle(RichTextStyle a, RichTextStyle b)
        {
            return a.Color == b.Color && a.Link == b.Link && a.Bold == b.Bold &&
                   a.Italic == b.Italic && a.Underline == b.Underline && a.Scale == b.Scale;
        }

        private static void DrawUnderline(SpriteBatch batch, float x, float y, float width, Color color)
        {
            if (width <= 0)
            {
                return;
            }
            if (_whitePixel == null)
            {
                _whitePixel = batch.GraphicsDevice.CreateTexture(1, 1);
                _whitePixel.SetData(new byte[] { 255, 255, 255, 255 }, 0, 0, 1, 1);
            }
            batch.Draw(_whitePixel, new RectangleF(x, y, width, 1f), null, color);
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

        /// <summary>backColor 非透明时用 1x1 白纹铺底（对齐原版 backColor 语义）。</summary>
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
                if (maxWidth <= 0) { result.Add(line); continue; }

                // 记录行首空白，仅贴到本段“第一个产出行”前面作为缩进；续行从 x=0 起（与 GDI 一致）。
                int lead = 0;
                while (lead < line.Length && line[lead] == ' ') lead++;
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
                    result.Add(firstLine ? leadStr + current.ToString() : current.ToString());
            }
            return result;
        }

    }
}
