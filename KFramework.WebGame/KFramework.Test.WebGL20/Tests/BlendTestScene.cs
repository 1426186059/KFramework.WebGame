using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.WebGL20.Tests
{
    /// <summary>
    /// 混合测试：把一张"半透明黑"图（RGB=0,0,0，A&lt;255）以不同的 BlendState 画到棋盘格背景上，
    /// 直观对比它在各种混合下"到底是什么颜色"。
    /// <list type="bullet">
    ///   <item><description>AlphaBlend / NonPremultiplied：白底被 a 加权压暗 → 灰色（标准透明）。</description></item>
    ///   <item><description>Additive / AdditiveFull：黑色加性无贡献 → 背景几乎不动（图"看不见"）。</description></item>
    ///   <item><description>Multiply：背景 × 黑(0) → 整格黑。</description></item>
    ///   <item><description>Opaque：关闭混合、整像素覆盖 → 纯黑（忽略 alpha）。</description></item>
    /// </list>
    /// </summary>
    public sealed class BlendTestScene : DemoScene
    {
        public override string Title => "混合测试：lefttopbg 半透明黑 × 各 BlendState";

        protected override string Description
            => "同一张半透明黑图(RGB=0,0,0, A<255)以不同混合画到棋盘底；白底→灰=标准透明，加性→看不见，乘性→黑，Opaque→纯黑";

        // 每个格子：名称 + 混合状态 + 一句话说明（在棋盘底上的可见颜色）。
        // 土色纯底色：便于看清半透明黑在不同混合下的颜色（替代棋盘格）。
        private static readonly Color EarthColor = new Color(168, 124, 80);

        private static readonly (string Name, BlendState Blend, string Note)[] Modes =
        [
            ("AlphaBlend(预乘)", BlendState.AlphaBlend, "土底被a压暗→深土"),
            ("NonPremultiplied", BlendState.NonPremultiplied, "标准透明→深土"),
            ("Old_NonPremultiplied", BlendState.Old_NonPremultiplied, "颜色同深土(α²仅坑烘焙)"),
            ("Additive", BlendState.Additive, "黑无贡献→图看不见"),
            ("AdditiveFull", BlendState.AdditiveFull, "黑+土底→不变"),
            ("Multiply", BlendState.Multiply, "土底×黑→整格黑"),
            ("Opaque", BlendState.Opaque, "关混合→纯黑(忽略a)"),
        ];

        private Texture2D? _tex;
        private Texture2D? _white;


        public override async Task LoadContentAsync()
        {
            await base.LoadContentAsync();
            var mBundle = await ContentManager.Default.LoadBundleAsync("Bundles");
            _tex = mBundle.LoadTexture("Bundles/lefttopbg.png");
        }

        public override void Draw()
        {
            SpriteBatch batch = Batch;

            _white ??= Device.CreateTexture(1, 1, new byte[] { 255, 255, 255, 255 });

            float top = 92f;
            float gapX = 16f, gapY = 64f;
            // 自适应列数（上限 4 列，避免每图被压得太小），行数随之；单图尽量大
            int cols = Math.Min(4, Modes.Length);
            cols = Math.Max(1, Math.Min(cols, (int)((Device.Viewport.Width - 40f) / 300f)));
            int rows = (Modes.Length + cols - 1) / cols;
            float availW = Device.Viewport.Width - 40f - (cols - 1) * gapX;
            float availH = (Device.Viewport.Height - 40f) - top - (rows - 1) * gapY;
            float cellW = availW / cols;
            float cellH = Math.Min(availH / rows, cellW * 0.85f);

            // —— 背景层（默认混合，不透明）：画棋盘底 + 名称条 ——
            batch.Begin();
            DrawHeader(batch);
            for (int i = 0; i < Modes.Length; i++)
            {
                var (x, y) = Cell(i, top, cellW, cellH, gapX, gapY, cols);
                batch.Draw(_white, new Rectangle((int)x, (int)y, (int)cellW, (int)cellH), null, EarthColor);
            }
            DrawFooter(batch);
            batch.End();

            // —— 混合层：每个格子用对应 BlendState 画测试图（与棋盘底混合）——
            if (_tex != null)
            {
                for (int i = 0; i < Modes.Length; i++)
                {
                    var (x, y) = Cell(i, top, cellW, cellH, gapX, gapY, cols);
                    batch.Begin(SpriteSortMode.Deferred, Modes[i].Blend);
                    batch.Draw(_tex, new Rectangle((int)x, (int)y, _tex.Width, _tex.Height), null, Color.White);
                    batch.End();
                }
            }
            else
            {
                batch.Begin();
                batch.DrawString(Font, "正在加载测试图 lefttopbg.png ...", new Vector2(20f, top), Color.White);
                batch.End();
            }

            // —— 标签层（默认混合，画在混合图之上）：名称 + 说明 ——
            batch.Begin();
            for (int i = 0; i < Modes.Length; i++)
            {
                var (x, y) = Cell(i, top, cellW, cellH, gapX, gapY, cols);
                batch.DrawString(Font, Modes[i].Name, new Vector2(x, y - 19f), new Color(126, 200, 255));
                batch.DrawString(Font, Modes[i].Note, new Vector2(x, y + cellH + 4f), new Color(210, 220, 235));
            }
            batch.End();
        }

        private static (float X, float Y) Cell(int i, float top, float cellW, float cellH, float gapX, float gapY, int cols)
        {
            int r = i / cols, c = i % cols;
            return (20f + c * (cellW + gapX), top + r * (cellH + gapY));
        }
    }
}
