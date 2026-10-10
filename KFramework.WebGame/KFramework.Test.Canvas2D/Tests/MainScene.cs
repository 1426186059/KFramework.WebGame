using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.Canvas2D.Tests
{

    /// <summary>一个测试项：名称、说明、以及创建对应场景的工厂。</summary>
    public sealed class TestEntry
    {
        public required string Name { get; init; }

        public required string Desc { get; init; }

        public required Func<KSceneBase> Factory { get; init; }
    }

    /// <summary>测试项注册表：新增一个测试页时在这里登记一项即可。</summary>
    public static class TestRegistry
    {
        public static IReadOnlyList<TestEntry> Entries { get; } =
        [
            new TestEntry
            {
                Name = "精灵批绘制 / 旋转 / 缩放 / 翻转 / 顶点色",
                Desc = "同纹理精灵合并提交，Canvas2D 逐四边形 drawImage（单位方格 → 四边形的仿射）；非白色顶点色会走按颜色缓存的着色副本",
                Factory = static () => new SpriteBatchScene(),
            },
            new TestEntry
            {
                Name = "文字与字形（SpriteFont）",
                Desc = "字形由浏览器光栅化进图集，再作为普通精灵绘制：多字号 / 测量 / 着色 / TextRenderer 折行居中 / 字形图集页数",
                Factory = static () => new TextScene(),
            },
            new TestEntry
            {
                Name = "纹理上传与采样（整张 / 局部 / Point⇄Linear / 混合 / 读像素）",
                Desc = "每张纹理就是一张离屏 canvas：SetData → putImageData（含局部更新），采样开关 → imageSmoothingEnabled，读像素 → getImageData",
                Factory = static () => new TextureScene(),
            },
            new TestEntry
            {
                Name = "混合模式（引擎 6 个 BlendState 对照）",
                Desc = "同一份内容分别用 NonPremultiplied / Additive / AdditiveFull / Multiply / AlphaBlend / Opaque 画一遍，对照 globalCompositeOperation",
                Factory = static () => new BlendScene(),
            },
            new TestEntry
            {
                Name = "渲染目标（RenderTarget2D）专项",
                Desc = "6 个画面切换：离屏上屏 · 无MSAA 负向验证 · RT→RT 合成 · 纹理合成 A-F（含切走再切回）· 画布来回切 · PreserveContents 累积",
                Factory = static () => new OffscreenScene(),
            },
        ];
    }

    /// <summary>
    /// 总纲页面：列出全部测试项，鼠标点条目（或按数字键）进入对应测试页。
    /// <para>
    /// 本工程只收录 Canvas2D 后端<b>支持</b>的测试：自定义着色器、GPU 实例化、URP 与画布级 MSAA
    /// 在 Canvas2D 下会抛 <see cref="NotSupportedException"/>，故不在列表里；
    /// 渲染目标（离屏 canvas）是支持的，见「离屏渲染」页。
    /// </para>
    /// </summary>
    public sealed class MainScene : KSceneBase
    {
        private readonly List<Rectangle> _rows = [];

        /// <summary>回到总纲（供各测试页的「← 总纲」与 Esc 调用）。</summary>
        public static void Open() => KSceneMgr.SetMainScene(new MainScene());

        private GraphicsDevice Device => KSceneMgr.Game.GraphicsDevice;

        private SpriteBatch Batch => KSceneMgr.SpriteBatch;

        private IFont Font => KDefaultRes.DefaultSpriteFont;

        public override void Update()
        {
            // 命中矩形必须在这里也布一次：Update 先于 Draw 执行，若只在 Draw 里填充，首帧点击会落空。
            LayoutRows();

            if (Input_Mouse.GetButtonDown(MouseButton.Left))
            {
                Vector2 p = Input_Mouse.Position;
                for (int i = 0; i < _rows.Count; i++)
                {
                    if (_rows[i].Contains(p))
                    {
                        KSceneMgr.SetMainScene(TestRegistry.Entries[i].Factory());
                        return;
                    }
                }
            }

            Keys[] digits = [Keys.Digit1, Keys.Digit2, Keys.Digit3, Keys.Digit4, Keys.Digit5, Keys.Digit6, Keys.Digit7, Keys.Digit8, Keys.Digit9];
            for (int i = 0; i < TestRegistry.Entries.Count && i < digits.Length; i++)
            {
                if (Input_KeyBoard.GetKeyDown(digits[i]))
                {
                    KSceneMgr.SetMainScene(TestRegistry.Entries[i].Factory());
                    return;
                }
            }
        }

        /// <summary>按当前视口算出每个条目的命中矩形（Update 与 Draw 共用，保证点击判定与画面一致）。</summary>
        private void LayoutRows()
        {
            _rows.Clear();

            const float x0 = 28f;
            const float y0 = 116f;
            const float gapY = 10f;

            int count = TestRegistry.Entries.Count;
            float availableWidth = Math.Max(240f, Device.Viewport.Width - 56f);
            float availableHeight = Math.Max(120f, Device.Viewport.Height - y0 - 56f);

            float naturalHeight = (Font.LineSpacing + 6f) * 2 + 18f;
            float minHeight = (Font.LineSpacing + 6f) * 2 + 4f;
            float rowHeight = Math.Clamp(availableHeight / Math.Max(1, count) - gapY, minHeight, naturalHeight);

            for (int i = 0; i < count; i++)
            {
                float y = y0 + i * (rowHeight + gapY);
                _rows.Add(new Rectangle((int)x0, (int)y, (int)availableWidth, (int)rowHeight));
            }
        }

        public override void Draw()
        {
            SpriteBatch batch = Batch;
            LayoutRows();

            batch.Begin();

            batch.DrawString(Font, "KFramework — Canvas2D 渲染测试", new Vector2(28f, 20f), new Color(126, 200, 255));
            batch.DrawString(Font, $"当前后端：{Device.BackendName}    （点条目或按数字键进入；各页内 Esc 返回本页）",
                new Vector2(28f, 48f), new Color(150, 165, 195));
            batch.DrawString(Font, "本后端不做自定义着色器 / GPU 实例化 / URP / MRT / RT 级 MSAA：相关入口会抛 NotSupportedException 或被忽略",
                new Vector2(28f, 74f), new Color(255, 206, 110));

            for (int i = 0; i < TestRegistry.Entries.Count; i++)
            {
                TestEntry entry = TestRegistry.Entries[i];
                Rectangle rect = _rows[i];

                bool hover = rect.Contains(Input_Mouse.Position);
                batch.Draw(KDefaultRes.DefaultTexture2D, rect, null, hover ? new Color(46, 66, 104) : new Color(30, 38, 58));

                batch.DrawString(Font, $"{i + 1}. {entry.Name}",
                    new Vector2(rect.X + 16f, rect.Y + 10f), new Color(210, 230, 255));
                batch.DrawString(Font, entry.Desc,
                    new Vector2(rect.X + 16f, rect.Y + 10f + Font.LineSpacing + 6f), new Color(140, 158, 190));
            }

            batch.End();
        }
    }

}
