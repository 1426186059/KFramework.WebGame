using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace MirGame.Tests.TextRenderTest
{

    /// <summary>
    /// 文本渲染 / 输入框测试模块，左右两栏布局。
    /// <para>左栏 <see cref="TextRenderer"/>：对齐 WinForms GDI 的 MeasureText / DrawText，
    /// 支持 WordBreak 自动折行、HorizontalCenter / Right / VerticalCenter 对齐、backColor 铺底。</para>
    /// <para>右栏 <see cref="TextBox"/>：模拟单机版聊天——下方面板是聊天记录（自动折行、从底向上排），
    /// 底部输入框点击聚焦后可键入 / IME 中文，回车发送；引擎自绘文字 + 光标 / 选区。</para>
    /// </summary>
    public sealed class TextRenderTestScene : TestSceneBase
    {
        public override string Title => "文本渲染 TextRenderer / 输入框 TextBox（左：渲染 · 右：单机聊天）";

        // 右栏聊天：输入框 + 记录。
        private readonly TextBox _chatInput = new();
        private readonly List<string> _log = new();
        private Rectangle _chatPanel;
        private Rectangle _chatRect;

        // 布局缓存（每帧按视口宽度重算）。
        private float _leftX, _rightX, _colW;
        private const float TopY = 68f;

        private IFont _font;

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
        }

        private void ComputeLayout()
        {
            float viewW = Device.Viewport.Width;
            float margin = 28f, gap = 24f;
            _colW = (viewW - margin * 2 - gap) / 2f;
            _leftX = margin;
            _rightX = margin + _colW + gap;

            // 右栏先画「标题 + 提示行」（约 34 + 30 = 64px），聊天面板在其下方。
            float chatTop = TopY + 34f + 30f + 8f;
            _chatPanel = new Rectangle((int)_rightX, (int)chatTop, (int)_colW, 470);
            _chatRect = new Rectangle((int)_rightX, _chatPanel.Bottom + 12, (int)_colW, 34);

            // 覆盖层坐标与绘制坐标保持一致（Focus 用 Location × OverlayScale）。
            _chatInput.Location = new Point(_chatRect.X, _chatRect.Y);
            _chatInput.Size = new Size(_chatRect.Width, _chatRect.Height);
        }

        public override void Update()
        {
            base.Update();
            ComputeLayout();

            if (Input_Mouse.GetButtonDown(MouseButton.Left))
            {
                Vector2 p = Input_Mouse.Position;
                if (_chatRect.Contains(p))
                    _chatInput.Focus();
                else
                    _chatInput.Blur();
            }
        }

        protected override void DrawBody(SpriteBatch batch, Vector2 origin)
        {
            ComputeLayout();
            DrawLeftColumn(batch);
            DrawRightColumn(batch);
        }

        // ── 左栏：TextRenderer ──────────────────────────────────────────────
        private void DrawLeftColumn(SpriteBatch batch)
        {
            float x = _leftX;
            float y = TopY;

            y += DrawSection(batch, "① TextRenderer（对齐 WinForms GDI 排版）", new Vector2(x, y));

            const string sample = "这是一段用于演示 WordBreak 自动折行的中文与 English 混排文本，"
                                  + "超出宽度时按词 / 逐字换行，并用 HorizontalCenter + VerticalCenter 居中。";

            // 主面板：WordBreak + 居中。
            int panelH = 150;
            Rectangle panel = new((int)x, (int)y, (int)_colW, panelH);
            DrawRect(batch, panel, new Color(24, 30, 46));
            Rectangle textBounds = new(panel.X + 10, panel.Y + 8, panel.Width - 20, panelH - 16);
            TextRenderer.DrawText(batch, sample, _font, textBounds, Color.White,
                                  TextFormatFlags.WordBreak | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            Size measured = TextRenderer.MeasureText(sample, _font, new Size(panel.Width - 20, int.MaxValue), TextFormatFlags.WordBreak);
            y += panelH + 6f;
            y += DrawLine(batch, Font, $"MeasureText(WordBreak) = {measured.Width} × {measured.Height}", new Vector2(x, y), Color.LightGray);

            // 三个对齐示例色带（backColor 铺底）。
            y += 8f;
            y += DrawSection(batch, "对齐 + 底色（backColor）", new Vector2(x, y));
            int bandH = 30, bandW = (int)_colW;
            DrawBand(batch, "Left 左对齐", new Rectangle((int)x, (int)y, bandW, bandH), Color.White, TextFormatFlags.Left | TextFormatFlags.VerticalCenter, new Color(60, 90, 140));
            y += bandH + 8;
            DrawBand(batch, "HorizontalCenter 居中", new Rectangle((int)x, (int)y, bandW, bandH), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter, new Color(60, 130, 110));
            y += bandH + 8;
            DrawBand(batch, "Right 右对齐", new Rectangle((int)x, (int)y, bandW, bandH), Color.White, TextFormatFlags.Right | TextFormatFlags.VerticalCenter, new Color(130, 90, 60));
        }

        private static void DrawBand(SpriteBatch batch, string text, Rectangle band, Color fore, TextFormatFlags flags, Color back)
        {
            batch.Draw(KDefaultRes.DefaultTexture2D, band, back);
            TextRenderer.DrawText(batch, text, KDefaultRes.DefaultSpriteFont, band, fore, flags);
        }

        // ── 右栏：单机聊天（TextBox） ────────────────────────────────────────
        private void DrawRightColumn(SpriteBatch batch)
        {
            float x = _rightX;
            float y = TopY;

            y += DrawSection(batch, "② 单机聊天（TextBox 输入 + 记录）", new Vector2(x, y));
            y += DrawLine(batch, Font, "点输入框聚焦 → 键入 / IME 中文 → 回车发送", new Vector2(x, y), Color.LightGray);

            DrawChatLog(batch, _chatPanel);
            DrawBox(batch, _chatInput, _chatRect, _chatInput.Text, false);
        }

        /// <summary>聊天记录面板：从底向上排，最新在底部，超出顶部自动截断。</summary>
        private void DrawChatLog(SpriteBatch batch, Rectangle panel)
        {
            // 面板底 + 内底
            batch.Draw(KDefaultRes.DefaultTexture2D, panel, new Color(110, 150, 210));
            batch.Draw(KDefaultRes.DefaultTexture2D,
                       new Rectangle(panel.X + 2, panel.Y + 2, panel.Width - 4, panel.Height - 4),
                       new Color(14, 18, 30));

            int availW = panel.Width - 20;
            float x = panel.X + 10f;
            float y = panel.Bottom - 10f;

            for (int i = _log.Count - 1; i >= 0; i--)
            {
                string msg = _log[i];
                Size sz = TextRenderer.MeasureText(msg, _font, new Size(availW, int.MaxValue), TextFormatFlags.WordBreak);
                y -= sz.Height;
                if (y < panel.Y + 8f) break; // 超出顶部，丢弃更旧的消息

                Color c = msg.StartsWith("我：") ? new Color(160, 230, 160)
                          : msg.StartsWith("系统：") ? new Color(255, 206, 110)
                          : Color.White;
                TextRenderer.DrawText(batch, msg, _font, new Rectangle((int)x, (int)y, availW, sz.Height), c, TextFormatFlags.WordBreak);
            }
        }

        /// <summary>画一个带边框的输入框底，并调用 TextBox 自绘（文字 + 光标 / 选区）。</summary>
        private void DrawBox(SpriteBatch batch, TextBox box, Rectangle rect, string shown, bool multiline)
        {
            batch.Draw(KDefaultRes.DefaultTexture2D, rect, new Color(110, 150, 210));
            batch.Draw(KDefaultRes.DefaultTexture2D,
                       new Rectangle(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4),
                       box.Focused ? new Color(30, 38, 58) : new Color(20, 26, 40));

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
