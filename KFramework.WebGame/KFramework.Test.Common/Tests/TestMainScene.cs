using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.Common.Tests
{
    /// <summary>
    /// 测试总屏：列出 <see cref="TestRegistry"/> 里登记的全部测试模块。
    /// 条目较多，整体放进一个 <see cref="KScrollView"/>。
    /// KCanvas 直接挂在场景自己的 <see cref="KSceneBase.SceneNodeRoot"/> 下（而非 KUIRoot）：
    /// 由框架（KSceneBase.Update/Draw 默认实现的递归遍历）自动 Update/Draw，且随场景切换自动销毁，无需手动摘离。
    /// 每个条目是一个 <see cref="KButton"/>（走 KInputMgr 事件分发，订阅 PointerClickEvent 进入），
    /// 不再用 Input 轮询自己算命中 —— 这样「拖滚动条」与「点卡片」是同一套事件体系，靠 Priority 区分，从根上杜绝穿透。
    /// </summary>
    public sealed class TestMainScene : KSceneBase
    {
        private KCanvas _canvas;
        private KScrollView _scroll;
        private readonly List<KButton> _cards = new();
        private IFont _font;
        private IFont _descFont;
        private bool _built;

        /// <summary>回到测试总屏（各测试场景的「返回」用）。</summary>
        public static void Open() => KSceneMgr.SetMainScene(new TestMainScene());

        public override void Update()
        {
            EnsureBuilt();
            base.Update(); // 递归驱动 SceneNodeRoot 下的 KCanvas → KScrollView（含滚轮 / 拖拽 / 矩阵刷新）

            // 数字键 1-9 作为前 9 个条目的快捷入口（键盘走 KInputMgr 查询，与指针事件互不干扰）
            for (int i = 0; i < TestRegistry.Entries.Count && i < 9; i++)
            {
                if (KInputMgr.GetKeyDown(new[]
                {
                    Keys.Digit1, Keys.Digit2, Keys.Digit3, Keys.Digit4, Keys.Digit5,
                    Keys.Digit6, Keys.Digit7, Keys.Digit8, Keys.Digit9,
                }[i]))
                {
                    Enter(i);
                    return;
                }
            }
        }

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            _font = KDefaultRes.DefaultSpriteFont;
            _descFont = new SpriteFont(KSceneMgr.Game.GraphicsDevice, 13f);

            // 关键：KCanvas 挂在场景自己的 SceneNodeRoot 下 —— 框架递归遍历会自动驱动它，且随场景切换销毁
            _canvas = new KCanvas { Parent = SceneNodeRoot };

            int vw = KSceneMgr.Game.GraphicsDevice.Viewport.Width;
            int vh = KSceneMgr.Game.GraphicsDevice.Viewport.Height;
            float marginX = 24f;
            float topY = 104f;
            float listW = Math.Min(820f, vw - marginX * 2f);
            float listH = Math.Max(200f, vh - topY - 24f);

            _scroll = new KScrollView
            {
                Parent = _canvas,
                Size = new Vector2(listW, listH),
                WorldPosition = new Vector2(marginX, topY),
            };

            // 卡片让出右侧滚动条宽度：滚动条区（与卡片右缘原本重叠）里就没有 KButton 了，
            // 该区域的按下自然落到 KScrollView（拖滚动条），不会误触卡片。
            float cardW = listW - _scroll.ScrollBarWidth;

            float accY = 0f;
            for (int i = 0; i < TestRegistry.Entries.Count; i++)
            {
                TestEntry entry = TestRegistry.Entries[i];

                // 条目 = KButton：自身即背景，走 KInputMgr 事件分发；Priority 高于 KScrollView(0)，
                // 内容区按下优先命中卡片，滚动条区因卡片不含而命中 KScrollView。
                var btn = new KButton
                {
                    Parent = _scroll.Content,
                    Size = new Vector2(cardW, 56),
                    LocalPosition = new Vector2(0, accY),
                    Priority = 1,
                };
                var bg = new Color(26, 32, 48);
                btn.Color = bg;
                btn.NormalColor = bg;
                btn.HighlightedColor = new Color(40, 48, 70);
                btn.PressedColor = new Color(54, 64, 92);

                // 左侧色条
                _ = new KImage
                {
                    Parent = btn,
                    Size = new Vector2(6, 56),
                    LocalPosition = Vector2.Zero,
                    Color = new Color(72, 150, 230),
                };
                _ = new KLabel($"[{i + 1}] {entry.Name}", Color.White, _font)
                {
                    Parent = btn,
                    Size = new Vector2(cardW - 24, 22),
                    LocalPosition = new Vector2(20, 7),
                    Alignment = TextAnchor.UpperLeft,
                };
                _ = new KLabel(entry.Desc, new Color(150, 165, 190), _descFont)
                {
                    Parent = btn,
                    Size = new Vector2(cardW - 24, 30),
                    LocalPosition = new Vector2(20, 28),
                    Alignment = TextAnchor.UpperLeft,
                    HorizontalWrap = HorizontalWrapMode.Wrap,
                };

                int idx = i; // 闭包捕获
                btn.PointerClickEvent += (s, e) => Enter(idx);

                _cards.Add(btn);
                accY += 56 + 6;
            }
        }

        private static void Enter(int index) => KSceneMgr.SetMainScene(TestRegistry.Entries[index].Factory());

        public override void Draw()
        {
            EnsureBuilt();
            SpriteBatch batch = KSceneMgr.SpriteBatch;
            IFont font = KDefaultRes.DefaultSpriteFont;

            // 页眉：列表由 base.Draw() 递归绘制（KCanvas → KScrollView，含 scissor 裁剪与右侧滚动条）
            batch.Begin();
            batch.DrawString(font, "KFramework 引擎测试", new Vector2(24, 40), new Color(126, 200, 255));
            batch.DrawString(font, "滚轮 / 拖右侧滚动条浏览全部；点击卡片或数字键 1-9 进入。",
                             new Vector2(24, 40 + font.LineSpacing + 8), Color.LightGray);
            batch.End();

            base.Draw(); // 递归绘制 SceneNodeRoot 下的 KCanvas（列表 + 滚动条）
        }

        public override void Dispose()
        {
            // 摘掉 KInputMgr 的全局注册（KButton / KScrollView 都是 IClickable，不摘会残留接收指针）
            foreach (var btn in _cards) KInputMgr.Unregister(btn);
            if (_scroll != null) KInputMgr.Unregister(_scroll);
            base.Dispose();
        }
    }
}
