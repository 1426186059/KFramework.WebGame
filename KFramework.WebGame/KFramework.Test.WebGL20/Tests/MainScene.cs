using KFramework.MonoGame;
using KFramework.MonoGameExtend;
using System.Collections.Generic;

namespace KFramework.Test.WebGL20.Tests
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
                Name = "精灵批绘制 / 旋转",
                Desc = "连续同纹理的精灵合并为一次 draw call；旋转 / 缩放都在 CPU 侧算好顶点",
                Factory = static () => new SpriteBatchScene(),
            },
            new TestEntry
            {
                Name = "离屏渲染 RenderTarget2D",
                Desc = "先渲染进离屏纹理，再把它贴回屏幕；FBO 由 GraphicsDevice 按绑定组合缓存复用",
                Factory = static () => new RenderTargetScene(),
            },
            new TestEntry
            {
                Name = "MSAA 多重采样",
                Desc = "离屏 MSAA=4 与 MSAA=0 并排，对比几何边缘抗锯齿（画布 MSAA 由上下文决定）",
                Factory = static () => new MsaaScene(),
            },
            new TestEntry
            {
                Name = "Texture2D 取像素（中转读回 / 同步往返）",
                Desc = "A 中转读回验证 WebGL 的 glReadPixels 与 WebGPU 的读回；B 验证可读纹理同步往返",
                Factory = static () => new Texture2DScene(),
            },
            new TestEntry
            {
                Name = "自定义 Effect（高斯模糊 / 描边发光 / 水波 / Metaball / 阴影 / 马赛克 / 反相 / 扫描线）",
                Desc = "SpriteBatch.Begin(effect: 自定义 GLSL 片元着色器) 套用效果；WebGL2 真实编译生效，空格键总览⇄单图",
                Factory = static () => new EffectScene(),
            },
            new TestEntry
            {
                Name = "材质属性（Material.SetXXX：Unity 风格）",
                Desc = "6 个格子共用一段片元着色器，各自只改一类材质属性：SetFloat / SetColor / SetInt / SetMatrix / SetTexture / SetVector",
                Factory = static () => new MaterialScene(),
            },
            new TestEntry
            {
                Name = "GPU 实例化（一次 DrawCall 画 N 个精灵）",
                Desc = "单位四边形 + 逐实例缓冲（divisor=1）+ drawElementsInstanced：N 个各有外观的精灵只产生 1 次 submit",
                Factory = static () => new InstancingScene(),
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

        /// <summary>
        /// 按当前视口算出每个条目的命中矩形（Update 与 Draw 共用，保证点击判定与画面一致）。
        /// 条目多于 6 个时自动分两列，并把行高压缩到视口装得下 —— 否则后面的条目会落到屏幕外，既看不见也点不到。
        /// </summary>
        private void LayoutRows()
        {
            _rows.Clear();

            const float x0 = 28f;
            const float y0 = 92f;
            const float gapX = 16f;
            const float gapY = 10f;

            int count = TestRegistry.Entries.Count;
            float availableWidth = Math.Max(240f, Device.Viewport.Width - 56f);
            float availableHeight = Math.Max(120f, Device.Viewport.Height - y0 - 56f);

            int columns = count <= 6 ? 1 : 2;
            int rowsPerColumn = (int)MathF.Ceiling(count / (float)columns);

            float naturalHeight = (Font.LineSpacing + 6f) * 2 + 18f;
            float minHeight = (Font.LineSpacing + 6f) * 2 + 4f;
            float rowHeight = Math.Clamp(availableHeight / rowsPerColumn - gapY, minHeight, naturalHeight);
            float rowWidth = (availableWidth - gapX * (columns - 1)) / columns;

            for (int i = 0; i < count; i++)
            {
                int column = i / rowsPerColumn;   // 先填满第一列再换列
                int row = i % rowsPerColumn;
                float x = x0 + column * (rowWidth + gapX);
                float y = y0 + row * (rowHeight + gapY);
                _rows.Add(new Rectangle((int)x, (int)y, (int)rowWidth, (int)rowHeight));
            }
        }

        public override void Draw()
        {
            SpriteBatch batch = Batch;
            LayoutRows();

            batch.Begin();

            batch.DrawString(Font, "KFramework 例子2 — WebGL2 渲染测试", new Vector2(28f, 20f), new Color(126, 200, 255));
            batch.DrawString(Font, $"当前后端：{Device.BackendName}    （点条目或按数字键进入；各页内 Esc 返回本页）",
                new Vector2(28f, 48f), new Color(150, 165, 195));

            for (int i = 0; i < TestRegistry.Entries.Count; i++)
            {
                TestEntry entry = TestRegistry.Entries[i];
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
