using System.Text;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.Common.Tests.RichTextTest
{
    /// <summary>
    /// 富文本（RichText）测试：验证 KRichLabel 的 Unity 风格标签子集 —— 颜色、超链接、粗/斜/下划线、字号。
    /// 五个页签：
    /// ① 标签总览：颜色（#RRGGBB / #RRGGBBAA / 命名色 / 嵌套 / 恢复默认）与样式标签，旁边给非富文本的原始串对照。
    /// ② 超链接命中：&lt;link="url"&gt; 与 &lt;a href="url"&gt; 两种写法，悬停 / 点击时调 KRichLabel.GetLinkAt 直接取回 URL。
    /// ③ 自动断言：用 Measure 验证标签确实被剥离、原样度量更宽；用 GetLinkAt 在链接文字坐标上采样，断言返回正确 URL。
    /// ④ 随机样本对照：按按钮随机生成 1~3 行、覆盖全部标签类型的富文本，上格显示原始串、下格显示渲染结果。
    /// ⑤ 自动换行：标签内容被折行拆开时，颜色/下划线/字号/链接是否延续到下一行（窄/中/宽框切换）。
    /// 富文本一律走 KRichLabel（内部持有 RichTextObject，解析 + 排版缓存复用），原始串对照走 TextRenderer 普通文本 API。
    /// </summary>
    public sealed class RichTextTestScene : TestSceneBase
    {
        public override string Title
        {
            get
            {
                return "富文本 RichText（颜色 / 超链接 / 标签 / 命中断言）";
            }
        }

        private enum Page
        {
            Tags = 0,
            Links = 1,
            Assert = 2,
            Random = 3,
            Wrap = 4,
        }

        private Page _page = Page.Tags;

        // 页②：超链接绘制用的矩形/排版，Update 与 Draw 要一致，命中查询才能对上画面。
        private Rectangle _linkRect;
        private string _hoverUrl = string.Empty;
        private string _status = "把鼠标移到蓝色下划线文字上，点击可查看命中 URL（测试里只显示，不打开浏览器）";

        // 页④：随机生成的富文本样本（上=原始串，下=富文本渲染）。
        private string _randomSample = string.Empty;
        private int _randomLineCount = 1;
        private readonly Random _random = new();

        // 页⑤：自动换行测试（标签内容被折行拆开时，颜色/链接/下划线是否延续到下一行）。
        private string _wrapSample = string.Empty;
        private int _wrapWidth = 420;

        // 富文本控件：内部持有 RichTextObject，解析与排版缓存复用；测试页只管设 Text / 调 DrawTo。
        private readonly KRichLabel _colorRich = new KRichLabel(ColorSample);
        private readonly KRichLabel _styleRich = new KRichLabel(StyleSample);
        private readonly KRichLabel _linkRich = new KRichLabel(LinkSample);
        private readonly KRichLabel _probeRich = new KRichLabel("<link=\"urlA\">甲</link>X<a href=\"urlB\">乙</a>");
        private readonly KRichLabel _randomRich = new KRichLabel();
        private readonly KRichLabel _wrapRich = new KRichLabel(WrapSample);
        private readonly KRichLabel _queryRich = new KRichLabel();   // GetLink 复用
        private readonly KRichLabel _measureRich = new KRichLabel(); // Measure 复用

        // ── 样本串（同时用于展示与断言）────────────────────────────────────

        private const string ColorSample =
            "<color=#FF5050>#FF5050 红</color>  <color=#50FF50>#50FF50 绿</color>  <color=#5070FF>#5070FF 蓝</color>\n" +
            "<color=red>red</color>  <color=orange>orange</color>  <color=gold>gold</color>  <color=#8040C080>#8040C080 半透明紫</color>\n" +
            "<color=#10A0A0>青色 <color=#FFD700>里面套金色</color> 再回青色</color>  <color>恢复默认色</color>";

        private const string StyleSample =
            "<b>粗体 b</b>  <i>斜体 i</i>  <u>下划线 u</u>  <size=26>大字号 size=26</size>  <size=12>小字号 size=12</size>\n" +
            "<b><i><u><size=22>四种标签嵌套</size></u></i></b>  普通文字对照";

        private const string LinkSample =
            "官网 <link=\"https://www.example.com/\">https://www.example.com/</link>，" +
            "文档 <a href=\"https://docs.example.com/guide\">https://docs.example.com/guide</a>。";

        /// <summary>自动换行样本：故意写成一整句长文本，让折行点落在标签内容中间，
        /// 检验颜色/超链接/下划线/<size> 跨行后是否延续（解析阶段已剥离成逐字符样式，理论上应正确）。</summary>
        private const string WrapSample =
            "开头普通文字，接着是<color=#FF4040>一段很长的红色文字它会被自动折行打断成两行甚至三行" +
            "并且颜色应该一直保持到这段结束为止</color>，然后普通，再是<u>一段很长的下划线文字也会被折行打断" +
            "下划线应当出现在折行后的每一行下面</u>，再普通，最后是<size=26>一段放大字号的长文字被折行打断" +
            "放大效果跨行保持</size>收尾。中间夹一个<link=\"https://wrap.example/long\">很长的超链接文字" +
            "被折行拆成两段时两段都应命中同一个网址</link>结束。";

        private void Layout()
        {
            BeginButtons(68);
            AddUiButton("① 颜色与样式标签", () => { _page = Page.Tags; }, _page == Page.Tags);
            AddUiButton("② 超链接命中", () => { _page = Page.Links; }, _page == Page.Links);
            AddUiButton("③ 自动断言", () => { _page = Page.Assert; }, _page == Page.Assert);
            AddUiButton("④ 随机样本对照", () => { _page = Page.Random; }, _page == Page.Random);
            AddUiButton("⑤ 自动换行", () => { _page = Page.Wrap; }, _page == Page.Wrap);

            if (_page == Page.Wrap)
            {
                if (_wrapSample.Length == 0)
                {
                    _wrapSample = WrapSample;
                }
                AddUiButton("窄框 220px", () => { _wrapWidth = 220; });
                AddUiButton("中框 420px", () => { _wrapWidth = 420; });
                AddUiButton("宽框 700px", () => { _wrapWidth = 700; });
            }

            if (_page == Page.Random)
            {
                if (_randomSample.Length == 0)
                {
                    RegenerateRandomSample();
                }
                AddUiButton("换一批（1~3 行随机）", () => { RegenerateRandomSample(); });
                AddUiButton("固定 1 行", () => { GenerateRandomSample(1); });
                AddUiButton("固定 3 行", () => { GenerateRandomSample(3); });
            }

            // 链接页的绘制矩形：Update 命中查询与 Draw 绘制必须共用同一个矩形与排版参数。
            // 起点用 ContentTop（按钮区下方），与 DrawLinksPage 的正文起点一致。
            float x = 28f;
            float y = ContentTop;
            y += Font.LineSpacing + 10f;   // DrawSection
            y += Font.LineSpacing + 6f;    // DrawLine
            _linkRect = new Rectangle((int)x, (int)y, (int)(Device.Viewport.Width - x * 2f), 70);
        }

        public override void Update()
        {
            base.Update();
            Layout();

            if (ClickButtons() || _page != Page.Links)
            {
                return;
            }

            Vector2 p = Input_Mouse.Position;
            _hoverUrl = GetLink(LinkSample, _linkRect, p.X, p.Y) ?? string.Empty;

            if (Input_Mouse.GetButtonDown(MouseButton.Left))
            {
                _status = _hoverUrl.Length > 0
                    ? "命中链接：" + _hoverUrl
                    : "未命中链接（点蓝色下划线文字）";
            }
        }

        /// <summary>用与链接页绘制相同的排版参数调 KRichLabel.GetLinkAt。</summary>
        private string GetLink(string text, Rectangle bounds, float x, float y)
        {
            _queryRich.Text = text;
            return _queryRich.GetLinkAt(bounds,
                                        TextAnchor.UpperLeft, HorizontalWrapMode.Overflow, VerticalWrapMode.Overflow,
                                        x, y);
        }

        protected override void DrawBody(SpriteBatch batch, Vector2 origin)
        {
            switch (_page)
            {
                case Page.Tags:
                    DrawTagsPage(batch, origin);
                    break;
                case Page.Links:
                    DrawLinksPage(batch, origin);
                    break;
                case Page.Assert:
                    DrawAssertPage(batch, origin);
                    break;
                case Page.Random:
                    DrawRandomPage(batch, origin);
                    break;
                default:
                    DrawWrapPage(batch, origin);
                    break;
            }
            DrawButtons(batch);
        }

        // ── 页①：标签总览 ─────────────────────────────────────────────────

        private void DrawTagsPage(SpriteBatch batch, Vector2 origin)
        {
            float x = origin.X;
            float y = ContentTop;
            float width = Device.Viewport.Width - x * 2f;

            y += DrawSection(batch, "① 颜色标签 <color=...>（开启富文本：标签不显示，只给文字上色）", new Vector2(x, y));
            Rectangle colorRect = new((int)x, (int)y, (int)width, 150);
            DrawRect(batch, colorRect, new Color(18, 23, 36));
            _colorRich.DrawTo(batch, colorRect, Color.White,
                              TextAnchor.UpperLeft, HorizontalWrapMode.Wrap, VerticalWrapMode.Overflow);
            y += colorRect.Height + 16f;

            y += DrawSection(batch, "② 样式标签 <b> <i> <u> <size=n>（粗/斜体取决于字体是否提供该字形）", new Vector2(x, y));
            Rectangle styleRect = new((int)x, (int)y, (int)width, 110);
            DrawRect(batch, styleRect, new Color(18, 23, 36));
            _styleRich.DrawTo(batch, styleRect, Color.White,
                              TextAnchor.UpperLeft, HorizontalWrapMode.Wrap, VerticalWrapMode.Overflow);
            y += styleRect.Height + 16f;

            y += DrawSection(batch, "③ 对照：richText = false（标签原样显示成字符）", new Vector2(x, y));
            Rectangle rawRect = new((int)x, (int)y, (int)width, 70);
            DrawRect(batch, rawRect, new Color(36, 28, 20));
            TextRenderer.DrawText(batch, "<color=#FF5050>红色</color> <link=\"url\">链接</link> <b>粗体</b>", Font, rawRect,
                                  new Color(255, 210, 160), TextAnchor.UpperLeft,
                                  HorizontalWrapMode.Wrap, VerticalWrapMode.Overflow);
        }

        // ── 页②：超链接命中 ───────────────────────────────────────────────

        private void DrawLinksPage(SpriteBatch batch, Vector2 origin)
        {
            float x = origin.X;
            float y = ContentTop;
            float width = Device.Viewport.Width - x * 2f;

            y += DrawSection(batch, "超链接：<link=\"url\"> / <a href=\"url\"> —— 蓝色下划线，调用 GetLinkAt 直接取回 URL", new Vector2(x, y));
            y += DrawLine(batch, Font, "鼠标悬停高亮，点击显示 URL（测试里不打开浏览器）",
                          new Vector2(x, y), new Color(190, 205, 230));

            DrawRect(batch, _linkRect, new Color(18, 23, 36));
            _linkRich.DrawTo(batch, _linkRect, Color.White,
                             TextAnchor.UpperLeft, HorizontalWrapMode.Overflow, VerticalWrapMode.Overflow);
            y = _linkRect.Bottom + 14f;

            Vector2 p = Input_Mouse.Position;
            bool hover = _hoverUrl.Length > 0;
            Color hoverColor = hover ? new Color(160, 220, 255) : new Color(150, 170, 200);
            y += DrawLine(batch, Font, "悬停命中：" + (hover ? _hoverUrl : "（无）"),
                          new Vector2(x, y), hoverColor);
            y += DrawLine(batch, Font, _status, new Vector2(x, y), new Color(255, 220, 120));

            // 两段链接的 URL（绘制顺序即阅读顺序）。
            y += DrawLine(batch, Font, "① https://www.example.com/    ② https://docs.example.com/guide",
                          new Vector2(x, y), new Color(140, 190, 240));
        }

        // ── 页③：自动断言 ─────────────────────────────────────────────────

        private void DrawAssertPage(SpriteBatch batch, Vector2 origin)
        {
            float x = origin.X;
            float y = ContentTop;
            float width = Device.Viewport.Width - x * 2f;

            y += DrawSection(batch, "自动断言（PASS/FAIL）：MeasureText 剥离标签 + DrawText 链接命中", new Vector2(x, y));

            List<(string name, bool pass, string detail)> rows = new();

            Size a = Measure("无标签文本ABC", true);
            Size b = Measure("无标签文本ABC", false);
            Add(rows, "无 < 时快路径：richText true/false 度量一致",
                a.Width == b.Width && a.Height == b.Height,
                a.Width + "x" + a.Height + " vs " + b.Width + "x" + b.Height);

            Size stripped = Measure("<color=#FF0000>红色文字</color>", true);
            Size plain = Measure("红色文字", false);
            Add(rows, "color 标签剥离后度量 == 纯文本",
                stripped.Width == plain.Width && stripped.Height == plain.Height,
                stripped.Width + "x" + stripped.Height + " vs " + plain.Width + "x" + plain.Height);

            Size raw = Measure("<color=#FF0000>红色文字</color>", false);
            Add(rows, "未解析时标签算字符，原样度量更宽",
                raw.Width > stripped.Width,
                "原样 " + raw.Width + " > 剥离 " + stripped.Width);

            Add(rows, "命名色 red 与 #FF0000 度量一致",
                Measure("<color=red>红色</color>", true).Width == Measure("红色", false).Width,
                "两者都应是「红色」的宽度");

            Add(rows, "link 标签剥离后度量 == 纯文本",
                Measure("<link=\"u\">链接文字</link>", true).Width == Measure("链接文字", false).Width, "链接文字");
            Add(rows, "a href 标签剥离后度量 == 纯文本",
                Measure("<a href=\"u\">链接文字</a>", true).Width == Measure("链接文字", false).Width, "链接文字");
            Add(rows, "嵌套样式标签剥离后度量 == 纯文本",
                Measure("<b>粗</b><i>斜</i><u>线</u>", true).Width == Measure("粗斜线", false).Width, "粗斜线");

            Size empty = Measure("<color=#FFFFFF></color>", true);
            Add(rows, "纯标签无可见文本时度量为 Empty",
                empty.Width == 0 && empty.Height == 0,
                empty.Width + "x" + empty.Height);

            // GetLinkAt：把被测串画出来，在每个字符中心采样，验证点击回调直接返回 url。
            const string probeText = "<link=\"urlA\">甲</link>X<a href=\"urlB\">乙</a>";
            Rectangle probe = new((int)x, (int)y, (int)MathF.Min(width, 700f), 40);
            DrawRect(batch, probe, new Color(18, 23, 36));
            _probeRich.DrawTo(batch, probe, Color.White,
                              TextAnchor.UpperLeft, HorizontalWrapMode.Overflow, VerticalWrapMode.Overflow);
            y += probe.Height + 14f;

            float wA = Font.MeasureString("甲").X;
            float wX = Font.MeasureString("X").X;
            float sampleY = probe.Y + Font.LineSpacing / 2f;
            string atA = GetLink(probeText, probe, probe.X + wA / 2f, sampleY);
            string atX = GetLink(probeText, probe, probe.X + wA + wX / 2f, sampleY);
            string atB = GetLink(probeText, probe, probe.X + wA + wX + wA / 2f, sampleY);
            string atOut = GetLink(probeText, probe, probe.Right - 2f, sampleY);

            Add(rows, "GetLinkAt 命中 link 段返回 urlA", atA == "urlA", "got=" + (atA ?? "null"));
            Add(rows, "GetLinkAt 命中 a href 段返回 urlB", atB == "urlB", "got=" + (atB ?? "null"));
            Add(rows, "GetLinkAt 非链接字符返回 null", atX == null, "got=" + (atX ?? "null"));
            Add(rows, "GetLinkAt 文字外空白返回 null", atOut == null, "got=" + (atOut ?? "null"));
            Add(rows, "GetLinkAt 无标签文本返回 null",
                GetLink("普通文字", probe, probe.X + wA / 2f, sampleY) == null, "got=null");

            int pass = 0;
            foreach ((string name, bool pass, string detail) row in rows)
            {
                if (row.pass)
                {
                    pass++;
                }
                Color c = row.pass ? new Color(90, 225, 130) : new Color(235, 90, 90);
                DrawLine(batch, Font, (row.pass ? "PASS  " : "FAIL  ") + row.name, new Vector2(x, y), c);
                y += Font.LineSpacing + 2f;
                DrawLine(batch, Font, "        " + row.detail, new Vector2(x, y), new Color(150, 170, 200));
                y += Font.LineSpacing + 8f;
            }
            DrawLine(batch, Font, "合计 " + pass + "/" + rows.Count + " 通过", new Vector2(x, y),
                     pass == rows.Count ? new Color(120, 240, 160) : new Color(255, 160, 80));
        }

        // ── 页④：随机样本生成与上下对照 ───────────────────────────────────

        /// <summary>可随机的词，让每次生成的样本内容不同。</summary>
        private static readonly string[] RandomWords =
        [
            "传奇", "沙巴克", "裁决", "屠龙", "赤月", "祖玛", "沃玛", "道士", "法师", "战士", "行会", "攻城"
        ];

        /// <summary>所有标签类型的模板：{0} 是占位词。每次生成按随机顺序取用，保证一行里覆盖全部标签。</summary>
        private static readonly string[] TagTemplates =
        [
            "<color=#FF5050>{0}</color>",
            "<color=#50C05080>{0}</color>",
            "<color=red>{0}</color>",
            "<color=orange>{0}</color>",
            "<color>{0}</color>",
            "<b>{0}</b>",
            "<i>{0}</i>",
            "<u>{0}</u>",
            "<size=26>{0}</size>",
            "<size=12>{0}</size>",
            "<link=\"https://www.example.com/\">{0}</link>",
            "<a href=\"https://docs.example.com/guide\">{0}</a>",
        ];

        private string PickWord()
        {
            return RandomWords[_random.Next(RandomWords.Length)];
        }

        /// <summary>生成一行随机富文本：把所有标签类型洗牌后各用一次，词随机。</summary>
        private string GenerateRandomLine()
        {
            List<string> order = new(TagTemplates);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            StringBuilder line = new();
            for (int i = 0; i < order.Count; i++)
            {
                if (i > 0)
                {
                    line.Append(' ');
                }
                line.Append(string.Format(order[i], PickWord()));
            }
            return line.ToString();
        }

        private void GenerateRandomSample(int lineCount)
        {
            _randomLineCount = Math.Clamp(lineCount, 1, 3);
            StringBuilder sample = new();
            for (int i = 0; i < _randomLineCount; i++)
            {
                if (i > 0)
                {
                    sample.Append('\n');
                }
                sample.Append(GenerateRandomLine());
            }
            _randomSample = sample.ToString();
        }

        private void RegenerateRandomSample()
        {
            GenerateRandomSample(_random.Next(1, 4));
        }

        private void DrawRandomPage(SpriteBatch batch, Vector2 origin)
        {
            float x = origin.X;
            float y = ContentTop;
            float width = Device.Viewport.Width - x * 2f;

            y += DrawSection(batch, "④ 随机样本对照：上=原始文本（含标签，richText=false），下=富文本渲染（richText=true）",
                             new Vector2(x, y));
            y += DrawLine(batch, Font, "点上方「换一批 / 固定 1 行 / 固定 3 行」按钮生成；每行随机洗牌覆盖全部标签类型。",
                          new Vector2(x, y), new Color(190, 205, 230));

            // 度量两块的真实高度：原始串含标签更长，按 Wrap 折行。
            Size rawSize = TextRenderer.MeasureText(_randomSample, Font, new Size((int)width, int.MaxValue),
                                                    HorizontalWrapMode.Wrap);
            _randomRich.Text = _randomSample;
            Size richSize = _randomRich.Measure(new Size((int)width, int.MaxValue), HorizontalWrapMode.Wrap);
            int rawH = Math.Max(40, rawSize.Height + 12);
            int richH = Math.Max(40, richSize.Height + 12);

            // 上：原始文本（标签原样显示）
            DrawSection(batch, "原始文本：" + _randomLineCount + " 行，共 " + _randomSample.Length + " 字符（标签当字符显示）",
                        new Vector2(x, y));
            y += Font.LineSpacing + 6f;
            Rectangle rawRect = new((int)x, (int)y, (int)width, rawH);
            DrawRect(batch, rawRect, new Color(36, 28, 20));
            TextRenderer.DrawText(batch, _randomSample, Font, rawRect, new Color(255, 215, 170),
                                  TextAnchor.UpperLeft, HorizontalWrapMode.Wrap, VerticalWrapMode.Overflow);
            y += rawH + 14f;

            // 下：富文本渲染结果
            DrawSection(batch, "富文本渲染结果（标签生效：颜色 / 超链接 / 粗斜体 / 下划线 / 字号）",
                        new Vector2(x, y));
            y += Font.LineSpacing + 6f;
            Rectangle richRect = new((int)x, (int)y, (int)width, richH);
            DrawRect(batch, richRect, new Color(18, 23, 36));
            _randomRich.DrawTo(batch, richRect, Color.White,
                               TextAnchor.UpperLeft, HorizontalWrapMode.Wrap, VerticalWrapMode.Overflow);
        }

        // ── 页⑤：自动换行（标签内容被折行拆开）─────────────────────────────

        private void DrawWrapPage(SpriteBatch batch, Vector2 origin)
        {
            float x = origin.X;
            float y = ContentTop;
            float fullW = Device.Viewport.Width - x * 2f;

            y += DrawSection(batch, "⑤ 自动换行：折行点落在标签内容中间时，颜色 / 下划线 / 字号 / 超链接是否延续到下一行",
                             new Vector2(x, y));
            y += DrawLine(batch, Font, "标签在解析阶段已剥离成逐字符样式，折行按纯文本做，再按样式切段——理论上跨行应正确。",
                          new Vector2(x, y), new Color(190, 205, 230));
            y += DrawLine(batch, Font, "当前框宽 " + _wrapWidth + "px（用上方按钮切换，越窄折行越多）。",
                          new Vector2(x, y), new Color(190, 205, 230));

            // 上：原始串（标签当字符，同样折行，作对照）
            Size rawSize = TextRenderer.MeasureText(_wrapSample, Font, new Size(_wrapWidth, int.MaxValue),
                                                    HorizontalWrapMode.Wrap);
            int rawH = Math.Max(40, rawSize.Height + 12);
            DrawSection(batch, "原始文本（标签原样，richText=false）", new Vector2(x, y));
            y += Font.LineSpacing + 6f;
            Rectangle rawRect = new((int)x, (int)y, _wrapWidth, rawH);
            DrawRect(batch, rawRect, new Color(36, 28, 20));
            TextRenderer.DrawText(batch, _wrapSample, Font, rawRect, new Color(255, 215, 170),
                                  TextAnchor.UpperLeft, HorizontalWrapMode.Wrap, VerticalWrapMode.Overflow);
            y += rawH + 16f;

            // 下：富文本渲染（标签生效），并画每行高度辅助线
            Size richSize = _wrapRich.Measure(new Size(_wrapWidth, int.MaxValue), HorizontalWrapMode.Wrap);
            int richH = Math.Max(40, richSize.Height + 12);
            DrawSection(batch, "富文本渲染（richText=true，横线=每行基线辅助，观察折行点）", new Vector2(x, y));
            y += Font.LineSpacing + 6f;
            Rectangle richRect = new((int)x, (int)y, _wrapWidth, richH);
            DrawRect(batch, richRect, new Color(18, 23, 36));
            _wrapRich.DrawTo(batch, richRect, Color.White,
                             TextAnchor.UpperLeft, HorizontalWrapMode.Wrap, VerticalWrapMode.Overflow);
            int lineCount = Math.Max(1, (int)MathF.Ceiling(richSize.Height / Font.LineSpacing));
            for (int i = 1; i < lineCount; i++)
            {
                int ly = richRect.Y + (int)(i * Font.LineSpacing);
                DrawRect(batch, new Rectangle(richRect.X, ly, richRect.Width, 1), new Color(45, 60, 90));
            }
            y += richH + 14f;

            // 链接跨行命中验证：在富文本块上按网格采样 GetLinkAt，统计唯一 URL 与命中行数。
            int hitRows = 0;
            string hitUrl = null;
            for (int py = richRect.Y + 2; py < richRect.Bottom - 2; py += 4)
            {
                bool rowHit = false;
                for (int px = richRect.X + 2; px < richRect.Right - 2; px += 6)
                {
                    string url = _wrapRich.GetLinkAt(richRect,
                                      TextAnchor.UpperLeft, HorizontalWrapMode.Wrap, VerticalWrapMode.Overflow, px, py);
                    if (url != null)
                    {
                        hitUrl = url;
                        rowHit = true;
                        break;
                    }
                }
                if (rowHit)
                {
                    hitRows++;
                }
            }
            y += DrawLine(batch, Font, "链接跨行命中：URL=" + (hitUrl ?? "（未命中）") + "  命中扫描行数≈" + hitRows,
                          new Vector2(x, y), hitUrl != null ? new Color(160, 220, 255) : new Color(235, 90, 90));
            y += DrawLine(batch, Font, "（命中行数>1 说明链接被折行拆到多行，且各行都能取回同一 URL，属正确）",
                          new Vector2(x, y), new Color(150, 170, 200));

            // 顺带列出 MeasureText（Wrap）量出的行数，核对高度是否随折行增长。
            y += DrawLine(batch, Font, "度量：原始 " + rawSize.Height + "px / 富文本 " + richSize.Height +
                                       "px，约 " + lineCount + " 行（字号放大行会更高）",
                          new Vector2(x, y), new Color(150, 200, 255));
        }

        private Size Measure(string text, bool richText)
        {
            if (!richText)
                return TextRenderer.MeasureText(text, Font, new Size(int.MaxValue, int.MaxValue),
                                                HorizontalWrapMode.Overflow);
            _measureRich.Text = text;
            return _measureRich.Measure(new Size(int.MaxValue, int.MaxValue), HorizontalWrapMode.Overflow);
        }

        private static void Add(List<(string name, bool pass, string detail)> rows, string name, bool pass, string detail)
        {
            rows.Add((name, pass, detail));
        }
    }
}
