namespace KFramework.MonoGame
{
    /// <summary>
    /// 富文本对象：持有文本与字体，缓存「标签解析」与「排版结果」，避免每次 Draw 都重新解析、重新逐字符测量。
    /// 排版结果以 (0,0) 为原点缓存；仅当 Text/Font 或矩形尺寸、对齐、折行方式变化时才重新布局。
    /// 绘制与超链接命中共用同一份排版结果，保证命中区与画面一致。
    /// </summary>
    public sealed partial class RichTextObject
    {
        private string _text = string.Empty;
        private IFont _font;
        private RichTextDocument _doc;   // 解析缓存：Text 变化时才重建
        private Texture2D _whitePixel;   // 下划线用的 1x1 白纹（按需创建）

        // 排版缓存：runs 坐标相对 (0,0)，绘制/命中时按 bounds.X/Y 平移。
        // 于是同一段文字的 4 次描边 + 1 次前景（仅位置偏移 1px）能复用同一次布局。
        private string _layoutText;
        private IFont _layoutFont;
        private float _layoutWidth;
        private float _layoutHeight;
        private TextAnchor _layoutAnchor;
        private HorizontalWrapMode _layoutHWrap;
        private VerticalWrapMode _layoutVWrap;
        private List<RichTextRun> _layoutRuns;
        private List<float> _layoutLineTops;   // 各行行顶 Y（相对原点），供画辅助线

        public RichTextObject()
        {
        }

        public RichTextObject(string text, IFont font)
        {
            SetText(text);
            SetFont(font);
        }

        public string Text
        {
            get
            {
                return _text;
            }
            set
            {
                SetText(value);
            }
        }

        public IFont Font
        {
            get
            {
                return _font;
            }
            set
            {
                SetFont(value);
            }
        }

        /// <summary>是否有可绘制内容（文本非空且至少有一个可见字符）。</summary>
        public bool HasContent
        {
            get
            {
                return _doc != null && !string.IsNullOrEmpty(_doc.PlainText);
            }
        }

        public void SetText(string text)
        {
            if (_text == text)
            {
                return;
            }
            _text = text ?? string.Empty;
            _doc = Parse(text);
            _layoutRuns = null;   // 文本变了：解析与排版都失效
            _layoutLineTops = null;
        }

        public void SetFont(IFont font)
        {
            if (_font == font)
            {
                return;
            }
            _font = font;
            _layoutRuns = null;   // 字体变了：字宽/行高变了，排版失效
            _layoutLineTops = null;
        }

        private static RichTextDocument Parse(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('<') < 0)
            {
                return null;   // 无标签：交回普通路径，无需富文本文档
            }
            return RichTextDocument.Parse(text);
        }

        // ---------------- 度量 ----------------

        /// <summary>富文本度量：剥离标签后按给定宽度折行测量。</summary>
        public Size Measure(Size proposedSize, HorizontalWrapMode hWrap)
        {
            if (_font == null || string.IsNullOrEmpty(_text))
            {
                return Size.Empty;
            }
            EnsureDoc();

            string plain = _doc != null ? _doc.PlainText : _text;
            if (string.IsNullOrEmpty(plain))
            {
                return Size.Empty;
            }

            bool wrap = hWrap == HorizontalWrapMode.Wrap && proposedSize.Width > 0;
            if (!wrap)
            {
                Vector2 v = _font.MeasureString(plain);
                return new Size((int)Math.Ceiling(v.X), (int)Math.Ceiling(v.Y));
            }

            List<string> lines = TextRenderer.WrapLines(plain, _font, proposedSize.Width);
            int w = 0;
            foreach (string line in lines)
            {
                if (!string.IsNullOrEmpty(line))
                {
                    w = Math.Max(w, (int)Math.Ceiling(_font.MeasureString(line).X));
                }
            }
            int h = (int)Math.Ceiling(lines.Count * _font.LineSpacing);
            return new Size(w, h);
        }

        // ---------------- 绘制 ----------------

        /// <summary>富文本绘制；batch 必须处于 Begin / End 之间。
        /// outlineMode=true 时只画去标签的纯文本、忽略段颜色（供描边用统一色）。</summary>
        public void Draw(SpriteBatch batch, 
                         RectangleF bounds, 
                         Color foreColor,
                         TextAnchor anchor, 
                         HorizontalWrapMode hWrap, 
                         VerticalWrapMode vWrap,
                         bool outlineMode = false)
        {
            if (batch == null || _font == null || string.IsNullOrEmpty(_text))
            {
                return;
            }
            EnsureDoc();

            if (_doc != null)
            {
                if (!string.IsNullOrEmpty(_doc.PlainText))
                {
                    DrawRichCore(batch, bounds, foreColor, anchor, hWrap, vWrap, outlineMode);
                }
                return;   // 全是标签，无可见文本
            }
            // 无标签：仍按富文本路径画（逐字符折行），但样式全默认，等效普通文本。
            DrawRichCore(batch, bounds, foreColor, anchor, hWrap, vWrap, outlineMode);
        }

        /// <summary>返回各行的行顶 Y（相对 bounds 原点），供测试/调试画行辅助线。
        /// 与 Draw 共用排版缓存；放大字所在行行高更大，行顶间距并不均匀。</summary>
        public List<float> GetLineTops(RectangleF bounds, TextAnchor anchor,
                                       HorizontalWrapMode hWrap, VerticalWrapMode vWrap)
        {
            LayoutRich(bounds, anchor, hWrap, vWrap);   // 命中缓存 / 触发排版
            return _layoutLineTops ?? new List<float>();
        }

        // ---------------- 超链接命中 ----------------

        /// <summary>超链接点击回调：传入与绘制时相同的排版参数与鼠标坐标，命中某个 &lt;link&gt;/&lt;a href&gt; 段时返回 url，否则 null。
        /// 复用与 Draw 相同的排版缓存，命中区与画面一致。</summary>
        public string GetLinkAt(RectangleF bounds, TextAnchor anchor, HorizontalWrapMode hWrap, VerticalWrapMode vWrap,
                                float x, float y)
        {
            if (_font == null || string.IsNullOrEmpty(_text))
            {
                return null;
            }
            EnsureDoc();
            if (_doc == null)
            {
                return null;   // 无标签，不可能有链接
            }
            List<RichTextRun> runs = LayoutRich(bounds, anchor, hWrap, vWrap);
            foreach (RichTextRun run in runs)
            {
                if (run.Style.Link == null)
                {
                    continue;
                }
                // 缓存的坐标相对 (0,0)，命中判断要加上本次 bounds 的原点。
                float rx = run.X + bounds.X;
                float ry = run.Y + bounds.Y;
                if (x >= rx && x <= rx + run.Width && y >= ry && y <= ry + run.Height)
                {
                    return run.Style.Link;
                }
            }
            return null;
        }

        // ---------------- 内部 ----------------

        private void EnsureDoc()
        {
            if (_doc == null && _text.IndexOf('<') >= 0)
            {
                _doc = RichTextDocument.Parse(_text);
            }
        }

        /// <summary>富文本排版：逐字符折行 → 对齐 → 逐样式段切分，产出每段的文本、样式与矩形。
        /// 结果以 (0,0) 为原点缓存；Text/Font 或尺寸、对齐、折行方式变化时才重算。
        /// 注意 bounds.X/bounds.Y 只作为绘制/命中的平移量，不进缓存 key——描边偏移 1px 也能命中同一份布局。</summary>
        private List<RichTextRun> LayoutRich(RectangleF bounds, TextAnchor anchor,
                                             HorizontalWrapMode hWrap, VerticalWrapMode vWrap)
        {
            if (_layoutRuns != null && 
                ReferenceEquals(_layoutText, _text) && 
                _layoutFont == _font &&
                _layoutWidth == bounds.Width && 
                _layoutHeight == bounds.Height &&
                _layoutAnchor == anchor && 
                _layoutHWrap == hWrap && 
                _layoutVWrap == vWrap)
            {
                return _layoutRuns;
            }

            string text = _doc != null ? _doc.PlainText : _text;
            RichTextStyle[] styles = _doc != null ? _doc.Styles : null;
            bool wrap = hWrap == HorizontalWrapMode.Wrap && bounds.Width > 0;
            float lineH = _font.LineSpacing;

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
                float scale = styles != null ? styles[i].Scale : 1f;
                float cw = _font.MeasureString(ch.ToString()).X * scale;
                if (wrap && lineW + cw > bounds.Width && i > lineStart)
                {
                    spans.Add((lineStart, i, lineW, lineMaxScale));
                    lineStart = i;
                    lineW = 0;
                    lineMaxScale = 1f;
                }
                lineW += cw;
                lineMaxScale = Math.Max(lineMaxScale, scale);
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
            float startY = 0f;
            if (row == 1)
            {
                startY = (bounds.Height - totalH) / 2f;
            }
            else if (row == 2)
            {
                startY = (bounds.Height - totalH);
            }
            float clipBottom = vWrap == VerticalWrapMode.Truncate ? bounds.Height : float.MaxValue;

            // 3) 逐行、逐样式段切分
            var runs = new List<RichTextRun>();
            var lineTops = new List<float>();
            foreach (var span in spans)
            {
                float rowH = lineH * span.maxScale;
                if (startY + rowH > clipBottom)
                {
                    break;
                }
                lineTops.Add(startY);
                float tx = 0f;
                if (col == 1)
                {
                    tx = (bounds.Width - span.width) / 2f;
                }
                else if (col == 2)
                {
                    tx = (bounds.Width - span.width);
                }

                int i = span.start;
                while (i < span.end)
                {
                    if (text[i] == '\n')
                    {
                        i++;
                        continue;
                    }
                    RichTextStyle st = styles != null ? styles[i] : RichTextStyle.Default;
                    int j = i + 1;
                    while (j < span.end && text[j] != '\n' &&
                           (styles == null || SameStyle(styles[j], st)))
                    {
                        j++;
                    }
                    string seg = text.Substring(i, j - i);
                    float segW = _font.MeasureString(seg).X * st.Scale;
                    float segH = lineH * st.Scale;
                    // 同一行内不同字号按「底部对齐」：run 底边统一贴到行底（startY + rowH）。
                    // 若一律顶部对齐，放大字（scale>1）的字形会向下压到同排普通字/下划线的位置，
                    // 表现为放大段首个字与相邻下划线重叠、甚至被盖住。
                    float segY = startY + (rowH - segH);
                    // Height 存该段自身缩放后的高度：下划线与命中区都按各自字号贴字。
                    runs.Add(new RichTextRun(seg, st, tx, segY, segW, segH));
                    tx += segW;
                    i = j;
                }
                startY += rowH;
            }

            // 命中：只有这些量变化才重算；bounds.X/Y 不进 key（只做平移）。
            _layoutText = _text;
            _layoutFont = _font;
            _layoutWidth = bounds.Width;
            _layoutHeight = bounds.Height;
            _layoutAnchor = anchor;
            _layoutHWrap = hWrap;
            _layoutVWrap = vWrap;
            _layoutRuns = runs;
            _layoutLineTops = lineTops;
            return runs;
        }

        /// <summary>用排版结果逐段绘制（颜色/超链接/下划线/字号）。</summary>
        private void DrawRichCore(SpriteBatch batch, RectangleF bounds, Color foreColor,
                                  TextAnchor anchor, HorizontalWrapMode hWrap, VerticalWrapMode vWrap,
                                  bool outlineMode)
        {
            List<RichTextRun> runs = LayoutRich(bounds, anchor, hWrap, vWrap);
            foreach (RichTextRun run in runs)
            {
                Color segColor = outlineMode ? foreColor : run.Style.EffectiveColor(foreColor);
                // 缓存坐标相对 (0,0)，绘制时加上本次 bounds 原点。
                float px = run.X + bounds.X;
                float py = run.Y + bounds.Y;
                _font.Draw(batch, run.Text, new Vector2(px, py), segColor,
                           0f, Vector2.Zero, run.Style.Scale, SpriteEffects.None, 0f);
                if (!outlineMode && run.Style.Underline)
                {
                    DrawUnderline(batch, px, py + run.Height - 1, run.Width, segColor);
                }
            }
        }

        private static bool SameStyle(RichTextStyle a, RichTextStyle b)
        {
            return a.Color == b.Color && a.Link == b.Link && a.Bold == b.Bold &&
                   a.Italic == b.Italic && a.Underline == b.Underline && a.Scale == b.Scale;
        }

        private void DrawUnderline(SpriteBatch batch, float x, float y, float width, Color color)
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
    }
}
