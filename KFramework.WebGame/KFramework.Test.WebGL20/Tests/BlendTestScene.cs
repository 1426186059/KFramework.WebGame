using KFramework.MonoGame;

namespace KFramework.Test.WebGL20.Tests
{
    // 复现“传奇半黑发白”的真正根因（用 RT 合成）：
    // dim 遮罩先烘进一张 RT，RT 用 Color.Transparent 清屏；再把这个 RT 叠到地面/世界上。
    // 清屏色写进了 RT 的透明像素——传奇的 Color.Transparent≈(255,255,255,0)白透，
    // 透明像素存成白，叠到地面上时白底透出 → 发白；引擎的=(0,0,0,0)黑透，透出黑 → 半黑正常。
    // 三组共用同一张上层图(_dim)与同一张下层图(_map)，只差 RT 的清屏色。
    public sealed class BlendTestScene : DemoScene
    {
        private const int Big = 256;

        private Texture2D? _dim;        // 上层图 A：游戏实际用的半透黑遮罩（Bundles/lefttopbg.png）
        private Texture2D? _map;        // 下层图 B：不透明土黄色地图
        private RenderTarget2D? _rtRef; // ① 参考：RT 用黑透(0,0,0,0)清屏 + dim
        private RenderTarget2D? _rtBug; // ② 现状：RT 用白透(255,255,255,0)清屏 + dim（传奇的 Transparent）
        private RenderTarget2D? _rtFix; // ③ 修正：RT 用黑透(0,0,0,0)清屏 + dim（引擎的 Transparent / 修法）
        private bool _rtDirty = true;

        public override string Title => "根因(RT合成)：清屏色 Transparent 两边不一致（白透→发白，黑透→半黑）";

        protected override string Description => "";

        public override async Task LoadContentAsync()
        {
            await base.LoadContentAsync();
            var mBundle = await ContentManager.Default.LoadBundleAsync("Bundles");
            _dim = mBundle.LoadTexture("Bundles/lefttopbg.png");                  // 上层图 A
            _map = Device.CreateTexture(Big, Big, Solid(168, 124, 80, 255));      // 下层图 B：土黄色地图
            _rtRef = new RenderTarget2D(Device, Big, Big);
            _rtBug = new RenderTarget2D(Device, Big, Big);
            _rtFix = new RenderTarget2D(Device, Big, Big);
        }

        public override void Draw()
        {
            SpriteBatch batch = Batch;

            if (_dim == null || _map == null || _rtRef == null || _rtBug == null || _rtFix == null)
            {
                batch.Begin();
                DrawHeader(batch);
                batch.DrawString(Font, "正在加载 ...", new Vector2(20f, 110f), Color.White);
                DrawFooter(batch);
                batch.End();
                return;
            }

            if (_rtDirty) { BakeRTs(); _rtDirty = false; }

            // ===== 每行：A 上层图(小) | B 下层图(大) | 中间RT(小) | C 最终(大地层大+居中半透小覆盖) =====
            // 严格一致：中间与最终里，下层(大地)都大、上层(dim)都小，方便看出“是否混合”
            float rowTop = 116f, rowH = 172f, boxTop = 22f;
            float aX = 130f, bX = 250f, rtX = 470f, cX = 600f;
            float small = 100f, bigW = 200f, bigH = 116f;

            Rectangle A(int i) => new((int)aX, (int)(rowTop + i * rowH + boxTop), (int)small, (int)small);
            Rectangle B(int i) => new((int)bX, (int)(rowTop + i * rowH + boxTop), (int)bigW, (int)bigH);
            Rectangle RT(int i) => new((int)rtX, (int)(rowTop + i * rowH + boxTop), (int)small, (int)small);
            Rectangle C(int i) => new((int)cX, (int)(rowTop + i * rowH + boxTop), (int)bigW, (int)bigH);

            // —— 文字层：A/B/C + 中间RT 标注 ——
            batch.Begin();
            DrawHeader(batch);
            void Lab(float x, float y, string s, Color c) => batch.DrawString(Font, s, new Vector2(x, y), c);
            for (int i = 0; i < 3; i++)
            {
                float y = rowTop + i * rowH;
                string name = i == 0 ? "① 参考(黑透清屏)"
                              : i == 1 ? "② 传奇现状: 白透清屏 (发白)"
                                       : "③ 修正: 黑透清屏 (半黑)";
                Color nc = i == 1 ? new Color(255, 120, 120) : i == 2 ? new Color(120, 255, 120) : new Color(255, 206, 110);
                Lab(24f, y + 44f, name, nc);
                Lab(aX, y + 2f, "A 上层图", new Color(180, 180, 180));
                Lab(bX, y + 2f, "B 下层图", new Color(180, 180, 180));
                Lab(rtX, y + 2f, "中间RT(dim)", new Color(180, 180, 180));
                Lab(cX, y + 2f, "C 最终", new Color(180, 180, 180));
                Lab(rtX + small + 8f, y + boxTop + small / 2f - 8f, "->", Color.LightGray);
            }
            DrawFooter(batch);
            batch.End();

            // —— 纹理层：全部 NonPremultiplied（变量不是混合因子，是 RT 清屏色）——
            // C：大地层(满框，大) + 居中半透小覆盖(小)，这样能看出“是否混合”
            Rectangle Cover(Rectangle big)
            {
                int m = 30;
                return new Rectangle(big.X + m, big.Y + m, big.Width - 2 * m, big.Height - 2 * m);
            }
            batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
            for (int i = 0; i < 3; i++)
            {
                RenderTarget2D rt = i == 0 ? _rtRef! : i == 1 ? _rtBug! : _rtFix!;
                batch.Draw(_dim, A(i), null, Color.White);   // 上层图（相同）
                batch.Draw(_map, B(i), null, Color.White);   // 下层图（相同）
                batch.Draw(rt, RT(i), null, Color.White);    // 中间RT：清屏色已写进透明像素
                batch.Draw(_map, C(i), null, Color.White);   // C：大地层（满框，大）
                batch.Draw(rt, Cover(C(i)), null, Color.White); // C：居中半透小覆盖（小）→ ② 白底透出=发白
            }
            batch.End();
        }

        // 烘焙：RT 用指定清屏色清屏，再把上层图(dim)绘进去。清屏色因此留在 RT 的透明像素里。
        private void BakeRTs()
        {
            Bake(_rtRef, new Color(0, 0, 0, 0));        // 引擎 / 修正：黑透
            Bake(_rtBug, new Color(255, 255, 255, 0));  // 传奇：白透（Transparent 解析成白）
            Bake(_rtFix, new Color(0, 0, 0, 0));        // 修正：黑透
        }

        private void Bake(RenderTarget2D rt, Color clear)
        {
            Device.SetRenderTarget(rt);
            Device.Clear(clear);
            Batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
            Batch.Draw(_dim, new Rectangle(0, 0, Big, Big), null, Color.White);
            Batch.End();
            Device.SetRenderTarget(null);
        }

        private static byte[] Solid(byte r, byte g, byte b, byte a)
        {
            byte[] d = new byte[Big * Big * 4];
            for (int i = 0; i < d.Length; i += 4)
            {
                d[i] = r; d[i + 1] = g; d[i + 2] = b; d[i + 3] = a;
            }
            return d;
        }
    }
}
