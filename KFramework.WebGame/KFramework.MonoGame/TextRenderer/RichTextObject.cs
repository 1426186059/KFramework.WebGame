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
        private readonly RichTextDocument _doc = new RichTextDocument();  // 复用实例，Text 变化才重解析
        private Texture2D _whitePixel;   // 下划线用的 1x1 白纹（按需创建）
        private string _layoutText;
        private IFont _layoutFont;
        private float _layoutWidth;
        private float _layoutHeight;
        private TextAnchor _layoutAnchor;
        private HorizontalWrapMode _layoutHWrap;
        private VerticalWrapMode _layoutVWrap;
        private List<RichTextRun> _layoutRuns = null;
        private List<float> _layoutLineTops = null;
        private float _layoutMeasuredWidth;   // LayoutRich 算出的最宽行，Measure 复用
        private float _layoutMeasuredHeight;  // LayoutRich 算出的总高（含 Scale 撑开）
        private RichTextInfo _textInfo; 
        private Color _lastForeColor = Color.White;

        public RichTextInfo TextInfo
        {
            get
            {
                return _textInfo;
            }
        }

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

        private bool _rich = true;

        /// <summary>是否解析富文本标签。默认 true（保持既有行为）；
        /// 置 false 时不解析，&lt;color&gt; 等标签按普通字符原样绘制，样式 / 链接全部失效。</summary>
        public bool Rich
        {
            get
            {
                return _rich;
            }
            set
            {
                if (_rich == value)
                {
                    return;
                }
                _rich = value;
                Reparse();
            }
        }

        /// <summary>是否有可绘制内容（文本非空且至少有一个可见字符）。</summary>
        public bool HasContent
        {
            get
            {
                return _doc.bHaveRichText && _doc.Styles.Count > 0;
            }
        }

        public void SetText(string text)
        {
            if (_text == text)
            {
                return;
            }
            _text = text ?? string.Empty;
            Reparse();
        }

        /// <summary>重新解析标签并让排版缓存失效。Rich=false 时不解析，
        /// LayoutRich / Measure 直接用 _text 原文，标签当普通字符显示。</summary>
        private void Reparse()
        {
            // 唯一解析入口：仅开启富文本时才让文档解析；关闭时清空标记走原文。
            if (_rich)
            {
                _doc.Parse(_text);
            }

            _layoutRuns = null;
            _layoutLineTops = null;
            _textInfo = null;
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
            _textInfo = null;
        }

        /// <summary>富文本度量：直接复用 LayoutRich 的排版结果（逐字符 Scale 宽度 + 每行最大 Scale 撑高），
        /// 与 Draw 同源，避免「测的」和「画的」不一致。Bold/Italic 当前绘制路径未生效，故不参与测量。</summary>
        public Size Measure(Size proposedSize, HorizontalWrapMode hWrap)
        {
            if (_font == null || string.IsNullOrEmpty(_text))
            {
                return Size.Empty;
            }

            // 关闭富文本：直接回退到 TextRenderer 普通文本度量。
            if (!_rich)
            {
                return TextRenderer.MeasureText(_text, _font, proposedSize, hWrap);
            }

            bool wrap = hWrap == HorizontalWrapMode.Wrap && proposedSize.Width > 0;
            float boundsW = wrap ? proposedSize.Width : float.MaxValue;
            LayoutRich(new RectangleF(0, 0, boundsW, float.MaxValue), TextAnchor.UpperLeft,
                       hWrap, VerticalWrapMode.Overflow);
            return new Size((int)Math.Ceiling(_layoutMeasuredWidth), (int)Math.Ceiling(_layoutMeasuredHeight));
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
            _lastForeColor = foreColor;

            // 关闭富文本：直接回退到 TextRenderer 普通文本绘制（无逐字符样式 / 无标签解析）。
            if (!_rich)
            {
                TextRenderer.DrawText(batch, _text, _font, bounds, foreColor, anchor, hWrap, vWrap);
                return;
            }

            if (_doc.bHaveRichText)
            {
                if (_doc.Styles.Count > 0)
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

        /// <summary>强制按给定矩形排版并生成 TextInfo，对标 TMP_Text.ForceMeshUpdate 的排版部分。
        /// 正常绘制无需调用（Draw 内部已排版）；仅在「未绘制就要命中/取字符信息」时显式调用。</summary>
        public void Layout(RectangleF bounds, TextAnchor anchor, HorizontalWrapMode hWrap, VerticalWrapMode vWrap)
        {
            if (_font == null)
            {
                return;
            }
            LayoutRich(bounds, anchor, hWrap, vWrap);
        }

        /// <summary>超链接命中便捷版（返回 url，否则 null）。只传鼠标位置——布局与参数 Draw/Layout 时已记在 TextInfo 里。
        /// TMP 签名的静态版在 <see cref="RichTextUtil.FindIntersectingLink"/>（返回 link 索引）。</summary>
        public string FindIntersectingLink(Vector2 worldPos)
        {
            RichTextInfo info = _textInfo;
            if (info == null || info.LinkCount == 0)
            {
                return null;
            }
            int idx = RichTextUtil.FindIntersectingLink(this, worldPos);
            return idx >= 0 ? info.Links[idx].Url : null;
        }

        // ---------------- 内部 ----------------

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

            List<RichTextStyle> docStyles =  _doc.Styles;
            int textLen = docStyles != null ? docStyles.Count : _text.Length;
            bool wrap = hWrap == HorizontalWrapMode.Wrap && bounds.Width > 0;
            float lineH = _font.LineSpacing;
            float fontAscent = _font.Ascent;   // 基准字号（scale=1）的基线到行顶距离
            float fontDescent = _font.Descent; // 基准字号的基线到字底距离

            // 1) 逐字符折行，记录每行的 [start,end)、宽度与该行最大缩放
            // 行高按最大 Scale 撑开，避免放大的字（<size> / 粗体）压到下一行。
            var spans = new List<(int start, int end, float width, float maxScale)>();
            int lineStart = 0;
            float lineW = 0;
            float lineMaxScale = 1f;
            for (int i = 0; i < textLen; i++)
            {
                char ch = docStyles != null ? docStyles[i].Character : _text[i];
                if (ch == '\n')
                {
                    spans.Add((lineStart, i, lineW, lineMaxScale));
                    lineStart = i + 1;
                    lineW = 0;
                    lineMaxScale = 1f;
                    continue;
                }
                float scale = docStyles != null ? docStyles[i].Scale : 1f;
                float cw = _font.MeasureString(ch).X * scale;
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
            spans.Add((lineStart, textLen, lineW, lineMaxScale));

            // 2) 对齐：每行行高 = 基础行高 * 该行最大缩放，累加得到总高
            int row = (int)anchor / 3;
            int col = (int)anchor % 3;
            float totalH = 0f;
            float maxLineW = 0f;
            foreach (var span in spans)
            {
                totalH += lineH * span.maxScale;
                maxLineW = Math.Max(maxLineW, span.width);
            }
            _layoutMeasuredWidth = maxLineW;
            _layoutMeasuredHeight = totalH;
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
                // 该行统一基线：最大字号的字形基线落在行顶 + Ascent*maxScale，
                // 段内各字号按自身 Ascent*scale 回退，最终都贴到这条线上。
                float baseY = startY + fontAscent * span.maxScale;
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
                    if ((docStyles != null ? docStyles[i].Character : _text[i]) == '\n')
                    {
                        i++;
                        continue;
                    }
                    RichTextStyle st = docStyles != null ? docStyles[i] : RichTextStyle.Default;
                    int j = i + 1;
                    while (j < span.end && (docStyles != null ? docStyles[j].Character : _text[j]) != '\n' &&
                           (docStyles == null || SameStyle(docStyles[j], st)))
                    {
                        j++;
                    }
                    string seg = docStyles != null
                        ? string.Create(j - i, (Styles: docStyles, Start: i), static (dst, state) =>
                          {
                              for (int k = 0; k < dst.Length; k++)
                              {
                                  dst[k] = state.Styles[state.Start + k].Character;
                              }
                          })
                        : _text.Substring(i, j - i);
                    float segW = _font.MeasureString(seg).X * st.Scale;
                    // 仿 TMP：排版时就把该段按自身字号缩放好的基线/升降部固化进 run，
                    // 后续绘制、命中、下划线只消费 run 自带值，不再回查字体。
                    float segAsc = fontAscent * st.Scale;
                    float segDesc = fontDescent * st.Scale;
                    runs.Add(new RichTextRun(seg, st, tx, segW, segAsc + segDesc, baseY, segAsc, segDesc, i));
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
            BuildTextInfo(bounds, runs, lineTops);
            return runs;
        }

        /// <summary>把 run 列表展开成逐字符信息（对标 TMP 的 characterInfo），并汇总行/链接。</summary>
        private void BuildTextInfo(RectangleF bounds, List<RichTextRun> runs, List<float> lineTops)
        {
            _textInfo = _textInfo ?? new RichTextInfo();
            RichTextInfo info = _textInfo;
            info.Clear();
            info.Bounds = bounds;

            // url -> link 索引，同一 url 跨行/分段合并为一条 RichLinkInfo
            var linkIndex = new Dictionary<string, int>();

            foreach (RichTextRun run in runs)
            {
                RichTextStyle st = run.Style;
                int linkIdx = -1;
                if (!string.IsNullOrEmpty(st.Link))
                {
                    if (!linkIndex.TryGetValue(st.Link, out linkIdx))
                    {
                        linkIdx = info.AddLink(new RichLinkInfo(st.Link, info.CharacterCount, info.CharacterCount));
                        linkIndex[st.Link] = linkIdx;
                    }
                }

                float cx = run.X;
                int charStart = info.CharacterCount;
                for (int k = 0; k < run.Text.Length; k++)
                {
                    char ch = run.Text[k];
                    float cw = _font.MeasureString(ch).X * st.Scale;
                    // 行号：行顶 <= 该字符基线 - Ascent 的最后一个行
                    int line = 0;
                    float glyphTop = run.BaselineY - run.Ascent;
                    for (int li = lineTops.Count - 1; li >= 0; li--)
                    {
                        if (glyphTop >= lineTops[li] - 0.5f)
                        {
                            line = li;
                            break;
                        }
                    }
                    info.AddCharacter(new RichCharacterInfo(
                        ch,
                        run.TextIndexBase + k,
                        line,
                        cx,
                        cw,
                        run.BaselineY,
                        run.Ascent,
                        run.Descent,
                        st.EffectiveColor(_lastForeColor),
                        linkIdx));
                    cx += cw;
                }

                if (linkIdx >= 0)
                {
                    info.ExtendLink(linkIdx, charStart, info.CharacterCount);
                }
            }

            // 行信息：用行顶和下一行行顶（或最后行高）算 [start,end) 字符范围
            for (int li = 0; li < lineTops.Count; li++)
            {
                int start = -1;
                for (int ci = 0; ci < info.CharacterCount; ci++)
                {
                    if (info.Characters[ci].Line == li)
                    {
                        start = ci;
                        break;
                    }
                }
                if (start < 0)
                {
                    continue;
                }
                int end = start;
                while (end < info.CharacterCount && info.Characters[end].Line == li)
                {
                    end++;
                }
                float top = lineTops[li];
                float height = li + 1 < lineTops.Count ? lineTops[li + 1] - top : _font.LineSpacing;
                info.AddLine(new RichLineInfo(start, end, top, height));
            }
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
                // 缓存坐标相对 (0,0)，绘制时加 bounds 原点；只消费 run 自带的基线数据。
                float px = run.X + bounds.X;
                float baselineY = run.BaselineY + bounds.Y;
                float glyphTop = baselineY - run.Ascent;
                _font.Draw(batch, run.Text, new Vector2(px, glyphTop), segColor,
                           0f, Vector2.Zero, run.Style.Scale, SpriteEffects.None, 0f);
                if (!outlineMode && run.Style.Underline)
                {
                    float glyphBottom = baselineY + run.Descent;
                    DrawUnderline(batch, px, glyphBottom - 1, run.Width, segColor);
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
