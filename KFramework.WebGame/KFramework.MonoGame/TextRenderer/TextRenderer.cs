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
            => MeasureTextCore(text, font, int.MaxValue, HorizontalWrapMode.Overflow);

        public static Size MeasureText(string text, IFont font, Size proposedSize)
            => MeasureTextCore(text, font, proposedSize.Width, HorizontalWrapMode.Overflow);

        /// <summary>度量文本：<paramref name="hWrap"/> = Wrap 时按 proposedSize.Width 折行后再量（取最宽行 + 行数高）。</summary>
        public static Size MeasureText(string text, IFont font, Size proposedSize, HorizontalWrapMode hWrap)
            => MeasureTextCore(text, font, proposedSize.Width, hWrap);

        private static Size MeasureTextCore(string text, IFont font, int maxWidth, HorizontalWrapMode hWrap)
        {
            if (font == null || string.IsNullOrEmpty(text)) return Size.Empty;

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
                    w = Math.Max(w, (int)Math.Ceiling(font.MeasureString(line).X));
            }
            int h = (int)Math.Ceiling(lines.Count * font.LineSpacing);
            return new Size(w, h);
        }

        // ---------------- DrawText（一律用调用方传入的批） ----------------

        /// <summary>在给定矩形内绘制文本；<paramref name="batch"/> 必须处于 Begin / End 之间。</summary>
        public static void DrawText(SpriteBatch batch, string text, IFont font, Rectangle bounds, Color foreColor,
                                    TextAnchor anchor = TextAnchor.UpperLeft,
                                    HorizontalWrapMode hWrap = HorizontalWrapMode.Overflow,
                                    VerticalWrapMode vWrap = VerticalWrapMode.Overflow)
        {
            if (batch == null || font == null || string.IsNullOrEmpty(text)) return;

            // Wrap 才折行；Overflow 只按显式 '\n' 分行（maxWidth <= 0 即不折）。
            int maxWidth = hWrap == HorizontalWrapMode.Wrap ? bounds.Width : 0;
            List<string> lines = WrapLines(text, font, maxWidth);

            float lineH = font.LineSpacing;
            float totalH = lineH * lines.Count;

            // TextAnchor 是行优先布局：(int)/3 取行（0=Upper / 1=Middle / 2=Lower），%3 取列（0=Left / 1=Center / 2=Right）。
            int row = (int)anchor / 3;
            int col = (int)anchor % 3;

            float startY = bounds.Y;
            if (row == 1) startY = bounds.Y + Math.Max(0f, (bounds.Height - totalH) / 2f);
            else if (row == 2) startY = bounds.Y + Math.Max(0f, bounds.Height - totalH);

            // Truncate：整行已落到框外就停（只截断，不画省略号）。
            float clipBottom = vWrap == VerticalWrapMode.Truncate ? bounds.Y + bounds.Height : float.MaxValue;

            foreach (string line in lines)
            {
                if (startY >= clipBottom) break;

                float lineW = font.MeasureString(line).X;
                float tx = bounds.X;
                if (col == 1) tx = bounds.X + Math.Max(0f, (bounds.Width - lineW) / 2f);
                else if (col == 2) tx = bounds.X + Math.Max(0f, bounds.Width - lineW);

                font.Draw(batch, line, new Vector2(tx, startY), foreColor, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
                startY += lineH;
            }
        }

        /// <summary>带背景铺底的重载：<paramref name="backColor"/> 非透明时先用 1x1 白纹把矩形填满，再画文本。</summary>
        public static void DrawText(SpriteBatch batch, string text, IFont font, Rectangle bounds, Color foreColor, Color backColor,
                                    TextAnchor anchor = TextAnchor.UpperLeft,
                                    HorizontalWrapMode hWrap = HorizontalWrapMode.Overflow,
                                    VerticalWrapMode vWrap = VerticalWrapMode.Overflow)
        {
            if (batch == null) return;
            FillBackground(batch, bounds, backColor);
            DrawText(batch, text, font, bounds, foreColor, anchor, hWrap, vWrap);
        }

        /// <summary>backColor 非透明时用 1x1 白纹铺底（对齐原版 backColor 语义）。</summary>
        private static void FillBackground(SpriteBatch batch, Rectangle bounds, Color backColor)
        {
            if (backColor.A == 0) return;
            if (_whitePixel == null)
            {
                _whitePixel = batch.GraphicsDevice.CreateTexture(1, 1);
                _whitePixel.SetData(new byte[] { 255, 255, 255, 255 }, 0, 0, 1, 1);
            }
            batch.Draw(_whitePixel, bounds, null, backColor);
        }

        /// <summary>按显式 '\n' 切分；maxWidth &gt; 0 时在给定宽度内按词折行，连续中文/长串退化逐字折行。
        /// 必须保留每段首行的行首空白（缩进）：WinForms 的 TextRenderer.MeasureText / DrawText 底层同走 GDI
        /// DrawText（度量仅加 DT_CALCRECT），对行首空格度量与绘制一致；移植层若在此剥掉行首空格，会导致
        /// Measure（含空格宽）与 Draw（无缩进）错位，进而使 NPC 对话框黄色链接按钮与底层文字重叠
        /// （如赏金公告板的缩进行）。故此处把行首空格作为缩进贴回“本段第一个产出行”前，续行从 x=0 起。</summary>
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
