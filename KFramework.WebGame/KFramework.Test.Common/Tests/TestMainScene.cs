using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.Common.Tests
{

    /// <summary>
    /// 测试总屏：列出 <see cref="TestRegistry"/> 里登记的全部测试模块，鼠标点击或按数字键 1..9 进入。
    /// </summary>
    public sealed class TestMainScene : KSceneBase
    {
        private readonly List<Rectangle> _cards = [];
        private IFont _descFont;

        /// <summary>回到测试总屏（各测试场景的「返回」用）。</summary>
        public static void Open() => KSceneMgr.SetMainScene(new TestMainScene());

        public override void Update()
        {
            Layout();

            if (Input_Mouse.GetButtonDown(MouseButton.Left))
            {
                Vector2 p = Input_Mouse.Position;
                for (int i = 0; i < _cards.Count; i++)
                {
                    if (_cards[i].Contains(p))
                    {
                        Enter(i);
                        return;
                    }
                }
            }

            for (int i = 0; i < TestRegistry.Entries.Count && i < 9; i++)
            {
                if (Input_KeyBoard.GetKeyDown(new[] { Keys.Digit1, Keys.Digit2, Keys.Digit3, Keys.Digit4, Keys.Digit5, Keys.Digit6, Keys.Digit7, Keys.Digit8, Keys.Digit9 }[i]))
                {
                    Enter(i);
                    return;
                }
            }
        }

        private static void Enter(int index) => KSceneMgr.SetMainScene(TestRegistry.Entries[index].Factory());

        private void Layout()
        {
            _cards.Clear();

            int viewWidth = KSceneMgr.Game.GraphicsDevice.Viewport.Width;
            int width = Math.Min(760, viewWidth - 48);
            int x = Math.Max(24, (viewWidth - width) / 2);
            int y = 106;

            foreach (TestEntry entry in TestRegistry.Entries)
            {
                _cards.Add(new Rectangle(x, y, width, 52));
                y += 52 + 6;
            }
        }

        public override void Draw()
        {
            SpriteBatch batch = KSceneMgr.SpriteBatch;
            IFont font = KDefaultRes.DefaultSpriteFont;
            _descFont ??= new SpriteFont(KSceneMgr.Game.GraphicsDevice, 13f);

            batch.Begin();

            float headerX = _cards.Count > 0 ? _cards[0].X : 28;
            batch.DrawString(font, "KFramework 引擎测试（例子1）", new Vector2(headerX, 40), new Color(126, 200, 255));
            batch.DrawString(font, "点击卡片或用数字键进入；各测试页按 Esc / 点「← 返回」回到本页。",
                             new Vector2(headerX, 40 + font.LineSpacing + 8), Color.LightGray);

            for (int i = 0; i < _cards.Count; i++)
            {
                TestEntry entry = TestRegistry.Entries[i];
                Rectangle rect = _cards[i];

                bool hover = rect.Contains(Input_Mouse.Position);
                batch.Draw(KDefaultRes.DefaultTexture2D, rect, hover ? new Color(40, 62, 104) : new Color(26, 32, 48));
                // 左侧色条
                batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(rect.X, rect.Y, 6, rect.Height), new Color(72, 150, 230));

                batch.DrawString(font, $"[{i + 1}] {entry.Name}", new Vector2(rect.X + 20, rect.Y + 9), Color.White);
                batch.DrawString(_descFont, entry.Desc, new Vector2(rect.X + 20, rect.Y + 9 + font.LineSpacing + 3), new Color(150, 165, 190));
            }

            batch.End();
        }
    }

}
