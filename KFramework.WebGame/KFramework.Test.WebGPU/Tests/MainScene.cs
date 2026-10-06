using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.WebGPU.Tests
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
                Name = "后端信息 / 精灵绘制通路",
                Desc = "显示当前生效的渲染后端（WebGPU 或回落后的 WebGL2）、Renderer、视口与 FPS，并跑一次精灵批绘制验证画面通路",
                Factory = static () => new BackendInfoScene(),
            },
            new TestEntry
            {
                Name = "混合模式",
                Desc = "BlendState 各预设对比。WebGPU 把混合因子固化在管线里，故每种混合对应一条独立管线",
                Factory = static () => new SpriteBlendScene(),
            },
            new TestEntry
            {
                Name = "渲染目标（RenderTarget2D）专项",
                Desc = "A 基本离屏 · B MSAA 解析 · C 嵌套合成（先画画布→切RT→切回继续画，传奇每帧的用法）· D PreserveContents 累积",
                Factory = static () => new OffscreenScene(),
            },
            new TestEntry
            {
                Name = "Texture2D 取像素（中转读回 / 同步往返）",
                Desc = "A 中转读回验证 WebGPU 的 copyTextureToBuffer+mapAsync 与 WebGL 的 readPixels；B 验证可读纹理同步往返",
                Factory = static () => new Texture2DScene(),
            },
        ];
    }

    /// <summary>
    /// 总纲页面：列出全部测试项，鼠标点条目（或按数字键）进入对应测试页。
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
            // 命中矩形必须在这里也布一次：Update 先于 Draw 执行，
            // 若只在 Draw 里填充，首帧的点击会落空（_rows 还是空的）。
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

            // 数字键直达（1..3）。Keys 是枚举，不能做 +i 算术，故显式列出。
            Keys[] digits = [Keys.Digit1, Keys.Digit2, Keys.Digit3];
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

            float y = 92f;
            float x = 28f;
            float width = Math.Max(240f, Device.Viewport.Width - 56f);
            float rowHeight = (Font.LineSpacing + 6f) * 2 + 18f;

            for (int i = 0; i < TestRegistry.Entries.Count; i++)
                _rows.Add(new Rectangle((int)x, (int)y + i * (int)(rowHeight + 10f), (int)width, (int)rowHeight));
        }

        public override void Draw()
        {
            SpriteBatch batch = Batch;
            LayoutRows();

            batch.Begin();

            batch.DrawString(Font, "KFramework 例子3 — WebGPU 渲染测试", new Vector2(28f, 20f), new Color(126, 200, 255));
            batch.DrawString(Font, $"当前后端：{Device.BackendName}    （点条目或按数字键进入；各页内 Esc 返回本页）",
                new Vector2(28f, 48f), new Color(150, 165, 195));

            for (int i = 0; i < TestRegistry.Entries.Count; i++)
            {
                TestEntry entry = TestRegistry.Entries[i];

                // 直接用 LayoutRows 算出的矩形，保证"画在哪"与"判定在哪"严格一致
                // （早先这里另算一份并再 Add 一次，结果 _rows 被加了两遍，且两份矩形因浮点累加会有偏差）。
                Rectangle rect = _rows[i];

                bool hover = rect.Contains(Input_Mouse.Position);
                batch.Draw(KDefaultRes.DefaultTexture2D, rect, hover ? new Color(46, 66, 104) : new Color(30, 38, 58));

                batch.DrawString(Font, $"{i + 1}. {entry.Name}",
                    new Vector2(rect.X + 16f, rect.Y + 10f), new Color(210, 230, 255));
                batch.DrawString(Font, entry.Desc,
                    new Vector2(rect.X + 16f, rect.Y + 10f + Font.LineSpacing + 6f), new Color(140, 158, 190));
            }

            batch.End();
        }
    }

}
