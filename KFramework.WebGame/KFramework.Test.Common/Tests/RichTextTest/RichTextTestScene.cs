using System.Text;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.Common.Tests.RichTextTest
{
    /// <summary>
    /// 富文本（RichText）测试：验证引擎 TextRenderer 的 Unity 风格标签子集 —— 颜色、超链接、粗/斜/下划线、字号。
    /// 三个页签：
    /// ① 标签总览：颜色（#RRGGBB / #RRGGBBAA / 命名色 / 嵌套 / 恢复默认）与样式标签，旁边给 richText=false 的对照。
    /// ② 超链接命中：&lt;link="url"&gt; 与 &lt;a href="url"&gt; 两种写法，悬停 / 点击时调 TextRenderer.GetLinkAt 直接取回 URL。
    /// ③ 自动断言：用 MeasureText 验证标签确实被剥离、原样度量更宽；用 GetLinkAt 在链接文字坐标上采样，断言返回正确 URL。
    /// ④ 随机样本对照：按按钮随机生成 1~3 行、覆盖全部标签类型的富文本，上格显示原始串、下格显示渲染结果。
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

        private void Layout()
        {
            BeginButtons(68);
            AddUiButton("① 颜色与样式标签", () => { _page = Page.Tags; }, _page == Page.Tags);
            AddUiButton("② 超链接命中", () => { _page = Page.Links; }, _page == Page.Links);
            AddUiButton("③ 自动断言", () => { _page = Page.Assert; }, _page == Page.Assert);
            AddUiButton("④ 随机样本对照", () => { _page = Page.Random; }, _page == Page.Random);

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

        /// <summary>用与链接页绘制相同的排版参数调 GetLinkAt。</summary>
        private string GetLink(string text, Rectangle bounds, float x, float y)
        {
            return TextRenderer.GetLinkAt(text, Font, bounds,
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
                default:
                    DrawRandomPage(batch, origin);
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
            TextRenderer.DrawText(batch, ColorSample, Font, colorRect, Color.White,
                                  TextAnchor.UpperLeft, HorizontalWrapMode.Wrap, VerticalWrapMode.Overflow, true);
            y += colorRect.Height + 16f;

            y += DrawSection(batch, "② 样式标签 <b> <i> <u> <size=n>（粗/斜体取决于字体是否提供该字形）", new Vector2(x, y));
            Rectangle styleRect = new((int)x, (int)y, (int)width, 110);
            DrawRect(batch, styleRect, new Color(18, 23, 36));
            TextRenderer.DrawText(batch, StyleSample, Font, styleRect, Color.White,
                                  TextAnchor.UpperLeft, HorizontalWrapMode.Wrap, VerticalWrapMode.Overflow, true);
            y += styleRect.Height + 16f;

            y += DrawSection(batch, "③ 对照：richText = false（标签原样显示成字符）", new Vector2(x, y));
            Rectangle rawRect = new((int)x, (int)y, (int)width, 70);
            DrawRect(batch, rawRect, new Color(36, 28, 20));
            TextRenderer.DrawText(batch, "<color=#FF5050>红色</color> <link=\"url\">链接</link> <b>粗体</b>", Font, rawRect,
                                  new Color(255, 210, 160), TextAnchor.UpperLeft,
                                  HorizontalWrapMode.Wrap, VerticalWrapMode.Overflow, false);
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
            TextRenderer.DrawText(batch, LinkSample, Font, _linkRect, Color.White,
                                  TextAnchor.UpperLeft, HorizontalWrapMode.Overflow, VerticalWrapMode.Overflow,
                                  true);
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
            TextRenderer.DrawText(batch, probeText, Font, probe, Color.White,
                                  TextAnchor.UpperLeft, HorizontalWrapMode.Overflow, VerticalWrapMode.Overflow,
                                  true);
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
                                                    HorizontalWrapMode.Wrap, false);
            Size richSize = TextRenderer.MeasureText(_randomSample, Font, new Size((int)width, int.MaxValue),
                                                     HorizontalWrapMode.Wrap, true);
            int rawH = Math.Max(40, rawSize.Height + 12);
            int richH = Math.Max(40, richSize.Height + 12);

            // 上：原始文本（标签原样显示）
            DrawSection(batch, "原始文本：" + _randomLineCount + " 行，共 " + _randomSample.Length + " 字符（标签当字符显示）",
                        new Vector2(x, y));
            y += Font.LineSpacing + 6f;
            Rectangle rawRect = new((int)x, (int)y, (int)width, rawH);
            DrawRect(batch, rawRect, new Color(36, 28, 20));
            TextRenderer.DrawText(batch, _randomSample, Font, rawRect, new Color(255, 215, 170),
                                  TextAnchor.UpperLeft, HorizontalWrapMode.Wrap, VerticalWrapMode.Overflow, false);
            y += rawH + 14f;

            // 下：富文本渲染结果
            DrawSection(batch, "富文本渲染结果（标签生效：颜色 / 超链接 / 粗斜体 / 下划线 / 字号）",
                        new Vector2(x, y));
            y += Font.LineSpacing + 6f;
            Rectangle richRect = new((int)x, (int)y, (int)width, richH);
            DrawRect(batch, richRect, new Color(18, 23, 36));
            TextRenderer.DrawText(batch, _randomSample, Font, richRect, Color.White,
                                  TextAnchor.UpperLeft, HorizontalWrapMode.Wrap, VerticalWrapMode.Overflow, true);
        }

        private Size Measure(string text, bool richText)
        {
            return TextRenderer.MeasureText(text, Font, new Size(int.MaxValue, int.MaxValue),
                                            HorizontalWrapMode.Overflow, richText);
        }

        private static void Add(List<(string name, bool pass, string detail)> rows, string name, bool pass, string detail)
        {
            rows.Add((name, pass, detail));
        }
    }
}
