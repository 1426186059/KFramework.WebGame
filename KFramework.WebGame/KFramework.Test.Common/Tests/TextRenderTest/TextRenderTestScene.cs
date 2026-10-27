using KFramework.MonoGame;
using KFramework.MonoGameExtend;
using KFramework.MonoGameExtend;

namespace KFramework.Test.Common.Tests.TextRenderTest
{

    /// <summary>
    /// 文本渲染 / 输入框测试模块（三个页签，按钮切换）。
    /// <para>
    /// ① <b>TextRenderer</b>：把引擎文本库的排版契约摊开看 ——
    /// <see cref="TextAnchor"/> 九个方向各占一个矩形（<b>格内画的就是锚点名本身</b>，一眼看出它贴在哪一角）、
    /// <see cref="HorizontalWrapMode"/> 的 Wrap / Overflow、<see cref="VerticalWrapMode"/> 的 Truncate / Overflow。
    /// </para>
    /// <para>
    /// ② <b>KLabel</b>：<b>同一套矩形、同一份测试</b>，只是改走 <see cref="KLabel"/> 控件
    /// （它内部同样落到 <see cref="TextRenderer"/>，但多了一层控件矩形 / Pivot / Alignment 的推导）——
    /// 两页对照就能分离"文本库本身的问题"与"控件层的换算问题"。
    /// </para>
    /// <para>
    /// ③ <b>TextBox</b>：模拟单机版聊天 —— 上方面板是聊天记录（自动折行、从底向上排），
    /// 下方输入框点击聚焦后可键入 / IME 中文，回车发送；引擎自绘文字 + 光标 / 选区。
    /// </para>
    /// </summary>
    public sealed class TextRenderTestScene : TestSceneBase
    {
        public override string Title => "文本渲染 TextRenderer / KLabel（锚点 9 向 · Wrap 模式）/ 输入框 TextBox";

        // ── 页签 ────────────────────────────────────────────────────────────
        private enum Page { TextRenderer = 0, KLabel = 1, TextBox = 2 }
        private Page _page = Page.TextRenderer;

        // ── 布局缓存（每帧重算；Update 也要用，故不能只在 Draw 里算）────────
        private readonly Rectangle[] _anchorCells = new Rectangle[9];
        private float _anchorTitleY;
        private Rectangle _hWrapCell, _hOverflowCell;
        private float _hWrapTitleY;
        private Rectangle _vTruncateCell, _vOverflowCell;
        private float _vWrapTitleY;

        // ── 页 ②：KLabel（与页 ① 共用同一套矩形，只是改走控件）──────────────
        private readonly KLabel[] _anchorLabels = new KLabel[9];
        private KLabel _hWrapLabel, _hOverflowLabel, _vTruncateLabel, _vOverflowLabel;

        // ── 页 ③：聊天 ─────────────────────────────────────────────────────
        private readonly TextBox _chatInput = new();
        private readonly List<string> _log = [];
        private Rectangle _chatPanel;
        private Rectangle _chatRect;

        private IFont _font;

        /// <summary>九个锚点的名字（格内就画它自己，对齐效果一眼可见）。</summary>
        private static readonly string[] AnchorNames =
        [
            "UpperLeft", "UpperCenter", "UpperRight",
            "MiddleLeft", "MiddleCenter", "MiddleRight",
            "LowerLeft", "LowerCenter", "LowerRight",
        ];

        /// <summary>九格配色（按行渐变，便于对照）。</summary>
        private static readonly Color[] AnchorColors =
        [
            new(255, 138, 128), new(255, 190, 120), new(255, 235, 130),
            new(160, 235, 150), new(120, 225, 215), new(130, 200, 255),
            new(170, 165, 255), new(225, 150, 245), new(255, 150, 190),
        ];

        /// <summary>水平 Wrap 样本：窄矩形里 Wrap 折行，Overflow 整行画出去。</summary>
        private const string WrapSample =
            "HorizontalWrapMode.Wrap：超过矩形宽度就在这里折行；"
            + "Overflow：整行一直画下去，越过右边界。";

        /// <summary>垂直 Wrap 样本：显式 \n 造 5 行，矮矩形里 Truncate 截断、Overflow 溢出。</summary>
        private const string VWrapSample =
            "第 1 行（VerticalWrapMode）\n第 2 行\n第 3 行\n第 4 行\n第 5 行 —— Truncate 到框底就停，Overflow 会画到框外。";

        public override void LoadContent()
        {
            _font = KDefaultRes.DefaultSpriteFont;

            _chatInput.Font = _font;
            _chatInput.Multiline = false;
            _chatInput.MaxLength = 200;
            _chatInput.OverlayScale = 1f;
            _chatInput.OverlayFontPx = 20f;
            _chatInput.OverlayFontCss = "20px sans-serif";

            // 回车发送：KeyPress(Enter) 由全局键盘 / DOM 覆盖层经 SimulateKeyDown 触发。
            _chatInput.KeyPress += (_, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                string text = _chatInput.Text.Trim();
                if (text.Length == 0) return;
                _log.Add("我：" + text);
                _chatInput.Text = string.Empty;
            };

            _log.Add("系统：本地聊天（单机）已就绪，点下方输入框开始输入，回车发送。");
            _log.Add("系统：支持中文 IME 与退格 / 方向键 / 选区。");

            // 页 ②：九个锚点各一个控件（格内文字就是锚点名），另加 Horizontal / Vertical Wrap 专项。
            for (int i = 0; i < 9; i++)
            {
                _anchorLabels[i] = new KLabel(AnchorNames[i], AnchorColors[i], _font)
                {
                    Alignment = (TextAnchor)i,
                };
            }

            _hWrapLabel = new KLabel(WrapSample, Color.White, _font)
            {
                Alignment = TextAnchor.UpperLeft,
                HorizontalWrap = HorizontalWrapMode.Wrap,
            };
            _hOverflowLabel = new KLabel(WrapSample, new Color(255, 214, 170), _font)
            {
                Alignment = TextAnchor.UpperLeft,
                HorizontalWrap = HorizontalWrapMode.Overflow,
            };
            _vTruncateLabel = new KLabel(VWrapSample, Color.White, _font)
            {
                Alignment = TextAnchor.UpperLeft,
                VerticalWrap = VerticalWrapMode.Truncate,
            };
            _vOverflowLabel = new KLabel(VWrapSample, new Color(255, 214, 170), _font)
            {
                Alignment = TextAnchor.UpperLeft,
                VerticalWrap = VerticalWrapMode.Overflow,
            };
        }

        // ── 布局（按钮登记 + 各测试块矩形）──────────────────────────────────

        private void Layout()
        {
            // 页签按钮：每帧重新登记（照 FontTestScene 的用法）。
            BeginButtons(68);
            AddUiButton("① TextRenderer（锚点 9 向 · Wrap 模式）",
                        () => _page = Page.TextRenderer, _page == Page.TextRenderer);
            AddUiButton("② KLabel（同一套测试走控件）",
                        () => _page = Page.KLabel, _page == Page.KLabel);
            AddUiButton("③ TextBox（单机聊天）",
                        () => _page = Page.TextBox, _page == Page.TextBox);

            if (_page == Page.TextBox)
            {
                LayoutChat();
                return;
            }

            // 页 ① 与页 ② 共用同一套矩形：同一份测试，分别走 TextRenderer / KLabel。
            const float margin = 28f, gap = 6f;
            float viewW = Device.Viewport.Width;
            float viewH = Device.Viewport.Height;
            float availW = viewW - margin * 2f;

            float titleH = Font.LineSpacing + 10f;   // DrawSection 实际占用的高度
            float bandH = 76f;                       // ②③ 面板高度（刻意比 5 行文本矮，好看出截断）

            // ① 九宫格：3×3，高度吃掉剩余空间。
            float y = ContentTop;
            _anchorTitleY = y;
            y += titleH;

            float gridAvail = viewH - y - (titleH + bandH) * 2f - 20f;
            float cellH = Math.Clamp(gridAvail / 3f, 34f, 88f);
            float cellW = (availW - gap * 2f) / 3f;

            for (int i = 0; i < 9; i++)
            {
                int row = i / 3, col = i % 3;
                _anchorCells[i] = new Rectangle(
                    (int)(margin + col * (cellW + gap)),
                    (int)(y + row * (cellH + gap)),
                    (int)cellW,
                    (int)cellH);
            }
            y += cellH * 3f + gap * 2f + 10f;

            // ② 水平 Wrap：两块并排。Overflow 那块放右边 —— 溢出的文字会画到它右边去，一眼可见。
            _hWrapTitleY = y;
            y += titleH;
            float halfW = (availW - gap) / 2f;
            _hWrapCell = new Rectangle((int)margin, (int)y, (int)halfW, (int)bandH);
            _hOverflowCell = new Rectangle((int)(margin + halfW + gap), (int)y, (int)halfW, (int)bandH);
            y += bandH + 10f;

            // ③ 垂直 Wrap：同样是并排两块（矮矩形 + 5 行文本）。
            _vWrapTitleY = y;
            y += titleH;
            _vTruncateCell = new Rectangle((int)margin, (int)y, (int)halfW, (int)bandH);
            _vOverflowCell = new Rectangle((int)(margin + halfW + gap), (int)y, (int)halfW, (int)bandH);

            // 页 ②：把本帧算好的矩形同步给 KLabel 控件。
            if (_page == Page.KLabel)
                LayoutKLabel();
        }

        /// <summary>
        /// 把本帧的矩形同步给页 ② 的 KLabel 控件。
        /// 控件用 <c>WorldPosition</c> 定位，而 <see cref="KLabel.Bounds"/> = <c>WorldPosition - Pivot * Size</c>，
        /// 故保持默认 <c>Pivot = (0,0)</c> 就能让控件矩形与页 ① 的矩形逐像素对齐 —— 两页可直接叠着看。
        /// </summary>
        private void LayoutKLabel()
        {
            for (int i = 0; i < 9; i++)
                SyncLabel(_anchorLabels[i], _anchorCells[i]);

            SyncLabel(_hWrapLabel, _hWrapCell);
            SyncLabel(_hOverflowLabel, _hOverflowCell);
            SyncLabel(_vTruncateLabel, _vTruncateCell);
            SyncLabel(_vOverflowLabel, _vOverflowCell);
        }

        private static void SyncLabel(KLabel label, Rectangle rect)
        {
            label.Size = new Vector2(rect.Width, rect.Height);
            label.WorldPosition = new Vector2(rect.X, rect.Y);
        }

        /// <summary>聊天页的布局（面板 + 输入框；输入框位置要同步给 DOM 覆盖层）。</summary>
        private void LayoutChat()
        {
            const float margin = 28f;
            float w = Device.Viewport.Width - margin * 2f;

            // 与 DrawTextBoxPage 的排版必须一致：节标题 + 提示行之后才是面板。
            float y = ContentTop + (Font.LineSpacing + 10f) + (Font.LineSpacing + 6f);

            int panelH = Math.Max(120, (int)(Device.Viewport.Height - y - 70f));
            _chatPanel = new Rectangle((int)margin, (int)y, (int)w, panelH);
            _chatRect = new Rectangle((int)margin, _chatPanel.Bottom + 10, (int)w, 34);

            // 覆盖层坐标与绘制坐标保持一致（Focus 用 Location × OverlayScale）。
            _chatInput.Location = new Point(_chatRect.X, _chatRect.Y);
            _chatInput.Size = new Size(_chatRect.Width, _chatRect.Height);
        }

        public override void Update()
        {
            base.Update();
            Layout();

            if (_page == Page.TextBox)
            {
                if (Input_Mouse.GetButtonDown(MouseButton.Left))
                {
                    if (_chatRect.Contains(Input_Mouse.Position))
                        _chatInput.Focus();
                    else
                        _chatInput.Blur();
                }
            }
            else
            {
                // 切到别的页就收起输入框：否则占位用的 DOM 覆盖层会留在屏幕上抢焦点。
                _chatInput.Blur();
            }

            ClickButtons();
        }

        protected override void DrawBody(SpriteBatch batch, Vector2 origin)
        {
            Layout();

            switch (_page)
            {
                case Page.TextRenderer: DrawTextRendererPage(batch); break;
                case Page.KLabel: DrawKLabelPage(batch); break;
                default: DrawTextBoxPage(batch); break;
            }

            DrawButtons(batch);
        }

        // ── 页 ①：TextRenderer（锚点九向 + 两种 Wrap 模式）──────────────────

        private void DrawTextRendererPage(SpriteBatch batch)
        {
            // ① TextAnchor 九向：每个矩形里用不同锚点画同一段（这里就用锚点名本身）。
            DrawSection(batch, "① TextAnchor 九向：同一段文字放进九个矩形，格内文字就是锚点名",
                        new Vector2(28f, _anchorTitleY));

            for (int i = 0; i < 9; i++)
            {
                Rectangle cell = _anchorCells[i];
                DrawRect(batch, cell, new Color(18, 23, 36));
                DrawBorder(batch, cell, new Color(58, 74, 108));

                // 默认 hWrap / vWrap 都是 Overflow：短文本不会被折行或截断。
                TextRenderer.DrawText(batch, AnchorNames[i], Font, cell, AnchorColors[i], (TextAnchor)i);
            }

            // ② HorizontalWrapMode：同一个窄矩形，Wrap 折行 / Overflow 画出去。
            DrawSection(batch, "② HorizontalWrapMode：同一个窄矩形 —— 左 Wrap 折行 / 右 Overflow 画出去",
                        new Vector2(28f, _hWrapTitleY));

            DrawRect(batch, _hWrapCell, new Color(18, 23, 36));
            DrawBorder(batch, _hWrapCell, new Color(70, 130, 110));
            TextRenderer.DrawText(batch, WrapSample, Font, _hWrapCell, Color.White,
                                  TextAnchor.UpperLeft, HorizontalWrapMode.Wrap);

            DrawRect(batch, _hOverflowCell, new Color(18, 23, 36));
            DrawBorder(batch, _hOverflowCell, new Color(140, 90, 70));
            TextRenderer.DrawText(batch, WrapSample, Font, _hOverflowCell, new Color(255, 214, 170),
                                  TextAnchor.UpperLeft, HorizontalWrapMode.Overflow);

            // ③ VerticalWrapMode：同一个矮矩形 + 5 行文本，Truncate 截断 / Overflow 溢出。
            DrawSection(batch, "③ VerticalWrapMode：同一个矮矩形 + 5 行文本 —— 左 Truncate 截断 / 右 Overflow 溢出",
                        new Vector2(28f, _vWrapTitleY));

            DrawRect(batch, _vTruncateCell, new Color(18, 23, 36));
            DrawBorder(batch, _vTruncateCell, new Color(70, 130, 110));
            TextRenderer.DrawText(batch, VWrapSample, Font, _vTruncateCell, Color.White,
                                  TextAnchor.UpperLeft, HorizontalWrapMode.Overflow, VerticalWrapMode.Truncate);

            DrawRect(batch, _vOverflowCell, new Color(18, 23, 36));
            DrawBorder(batch, _vOverflowCell, new Color(140, 90, 70));
            TextRenderer.DrawText(batch, VWrapSample, Font, _vOverflowCell, new Color(255, 214, 170),
                                  TextAnchor.UpperLeft, HorizontalWrapMode.Overflow, VerticalWrapMode.Overflow);
        }

        // ── 页 ②：KLabel（同一套矩形与样本，改走控件）──────────────────────

        private void DrawKLabelPage(SpriteBatch batch)
        {
            DrawSection(batch, "① KLabel.Alignment 九向：与页 ① 完全相同的九个矩形，改由 KLabel 控件绘制",
                        new Vector2(28f, _anchorTitleY));

            for (int i = 0; i < 9; i++)
            {
                Rectangle cell = _anchorCells[i];
                DrawRect(batch, cell, new Color(18, 23, 36));
                DrawBorder(batch, cell, new Color(58, 74, 108));

                // 控件自己走 TextRenderer；批已由 DrawBody 开好，这里只需在批内调用。
                _anchorLabels[i].Draw();
            }

            DrawSection(batch, "② KLabel.HorizontalWrap：同一个窄矩形 —— 左 Wrap 折行 / 右 Overflow 画出去",
                        new Vector2(28f, _hWrapTitleY));

            DrawRect(batch, _hWrapCell, new Color(18, 23, 36));
            DrawBorder(batch, _hWrapCell, new Color(70, 130, 110));
            _hWrapLabel.Draw();

            DrawRect(batch, _hOverflowCell, new Color(18, 23, 36));
            DrawBorder(batch, _hOverflowCell, new Color(140, 90, 70));
            _hOverflowLabel.Draw();

            DrawSection(batch, "③ KLabel.VerticalWrap：同一个矮矩形 + 5 行文本 —— 左 Truncate 截断 / 右 Overflow 溢出",
                        new Vector2(28f, _vWrapTitleY));

            DrawRect(batch, _vTruncateCell, new Color(18, 23, 36));
            DrawBorder(batch, _vTruncateCell, new Color(70, 130, 110));
            _vTruncateLabel.Draw();

            DrawRect(batch, _vOverflowCell, new Color(18, 23, 36));
            DrawBorder(batch, _vOverflowCell, new Color(140, 90, 70));
            _vOverflowLabel.Draw();
        }

        /// <summary>画 1px 矩形边框（四条细矩形拼）。</summary>
        private void DrawBorder(SpriteBatch batch, Rectangle rect, Color color)
        {
            DrawRect(batch, new Rectangle(rect.X, rect.Y, rect.Width, 1), color);
            DrawRect(batch, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), color);
            DrawRect(batch, new Rectangle(rect.X, rect.Y, 1, rect.Height), color);
            DrawRect(batch, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), color);
        }

        // ── 页 ③：单机聊天（TextBox）────────────────────────────────────────

        private void DrawTextBoxPage(SpriteBatch batch)
        {
            float x = 28f;
            float y = ContentTop;

            y += DrawSection(batch, "单机聊天（TextBox 输入 + 记录）", new Vector2(x, y));
            DrawLine(batch, Font, "点输入框聚焦 → 键入 / IME 中文 → 回车发送", new Vector2(x, y), Color.LightGray);

            DrawChatLog(batch, _chatPanel);
            DrawBox(batch, _chatInput, _chatRect, _chatInput.Text, false);
        }

        /// <summary>聊天记录面板：从底向上排，最新在底部，超出顶部自动截断。</summary>
        private void DrawChatLog(SpriteBatch batch, Rectangle panel)
        {
            // 面板底 + 内底
            batch.Draw(KDefaultRes.DefaultTexture2D, panel, null, new Color(110, 150, 210));
            batch.Draw(KDefaultRes.DefaultTexture2D,
                       new Rectangle(panel.X + 2, panel.Y + 2, panel.Width - 4, panel.Height - 4),
                       null, new Color(14, 18, 30));

            int availW = panel.Width - 20;
            float x = panel.X + 10f;
            float y = panel.Bottom - 10f;

            for (int i = _log.Count - 1; i >= 0; i--)
            {
                string msg = _log[i];
                Size sz = TextRenderer.MeasureText(msg, _font, new Size(availW, int.MaxValue), HorizontalWrapMode.Wrap);
                y -= sz.Height;
                if (y < panel.Y + 8f) break; // 超出顶部，丢弃更旧的消息

                Color c = msg.StartsWith("我：") ? new Color(160, 230, 160)
                          : msg.StartsWith("系统：") ? new Color(255, 206, 110)
                          : Color.White;
                TextRenderer.DrawText(batch, msg, _font, new Rectangle((int)x, (int)y, availW, sz.Height), c,
                                      hWrap: HorizontalWrapMode.Wrap);
            }
        }

        /// <summary>画一个带边框的输入框底，并调用 TextBox 自绘（文字 + 光标 / 选区）。</summary>
        private void DrawBox(SpriteBatch batch, TextBox box, Rectangle rect, string shown, bool multiline)
        {
            batch.Draw(KDefaultRes.DefaultTexture2D, rect, null, new Color(110, 150, 210));
            batch.Draw(KDefaultRes.DefaultTexture2D,
                       new Rectangle(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4),
                       null, box.Focused ? new Color(30, 38, 58) : new Color(20, 26, 40));

            box.DrawTextBox(batch, Device, box.Font, shown, rect, Color.White,
                            box.SelectionStart, multiline, box.Focused,
                            TextBox.DefaultPadLeft, box.CompositionString,
                            box.SelectionStart, box.SelectionLength);
        }

        public override void Dispose()
        {
            _chatInput.Blur();
            base.Dispose();
        }
    }

}
