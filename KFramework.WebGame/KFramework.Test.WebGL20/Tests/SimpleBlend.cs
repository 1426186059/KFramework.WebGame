using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.WebGL20.Tests
{
    /// <summary>
    /// SimpleBlend（独立模块）：大背景（颜色可调，直绘）+ 混合下层(lefttop_leftbg2) + 混合上层(lefttop_leftbg1)，
    /// 做两种混合对比：
    /// <para>① 直绘：背景 + 下 + 上 直接画到屏幕（默认混合 NonPremultiplied）。</para>
    /// <para>② RT 烘焙：背景直绘 + 把 下+上 渲染进一张 RT，再把该 RT 合成到屏幕（AlphaBlend 读预乘）。</para>
    /// <para>下层/上层用图集原图尺寸绘制（不拉伸），背景始终直绘。两列结果应完全一致。</para>
    /// </summary>
    public sealed class SimpleBlend : DemoScene
    {
        private Texture2D? _bgTex;   // 大背景（1x1 白，用 tint 上色，颜色可调，直绘）
        private Texture2D? _downTex; // 混合下层：Bundles/lefttop_leftbg2
        private Texture2D? _upTex;   // 混合上层：Bundles/lefttop_leftbg1
        private RenderTarget2D? _rt; // ② 烘焙 RT（只含 下+上）
        private bool _rtDirty = true;

        private static readonly Color[] BgColors =
        [
            new Color(40, 44, 60),
            new Color(92, 60, 50),
            new Color(50, 82, 60),
            new Color(82, 82, 94),
        ];
        private int _bgIndex;

        public override string Title => "SimpleBlend：直绘 vs RT烘焙（大背景直绘 + 下/上层原图尺寸）";

        protected override string Description
            => "① 直绘：背景+下+上直接画（NonPremultiplied）。② RT：背景直绘 + 下+上进RT→上屏（AlphaBlend）。按空格切背景色，两列应一致。";

        public override async Task LoadContentAsync()
        {
            await base.LoadContentAsync();
            var mBundle = await ContentManager.Default.LoadBundleAsync("Bundles");
            _downTex = mBundle.LoadTexture("Bundles/y_lefttop-sheet/lefttop_leftbg2.png");
            _upTex = mBundle.LoadTexture("Bundles/y_lefttop-sheet/lefttop_leftbg1.png");
            _bgTex = Device.CreateTexture(1, 1, new byte[] { 255, 255, 255, 255 });
        }

        public override void Update()
        {
            if (Input_KeyBoard.GetKeyDown(Keys.Space))
                _bgIndex = (_bgIndex + 1) % BgColors.Length;
        }

        public override void Draw()
        {
            SpriteBatch batch = Batch;

            if (_bgTex == null || _downTex == null || _upTex == null)
            {
                batch.Begin();
                DrawHeader(batch);
                batch.DrawString(Font, "正在加载 Bundles/lefttop_leftbg1 / lefttop_leftbg2 ...", new Vector2(20f, 110f), Color.White);
                DrawFooter(batch);
                batch.End();
                return;
            }

            float top = 96f;
            float gap = 40f, x0 = 24f;
            float colW = (Device.Viewport.Width - x0 * 2 - gap) / 2f;
            float colH = Device.Viewport.Height - top - 150f;
            Rectangle left = new((int)x0, (int)top, (int)colW, (int)colH);
            Rectangle right = new((int)(x0 + colW + gap), (int)top, (int)colW, (int)colH);

            // 懒创建/重建 RT（尺寸随列尺寸；仅尺寸变化时重建）
            int rw = (int)colW, rh = (int)colH;
            if (_rt == null || _rt.Width != rw || _rt.Height != rh)
            {
                _rt?.Dispose();
                _rt = new RenderTarget2D(Device, rw, rh);
                _rtDirty = true;
            }
            if (_rtDirty) { BakeRT(rw, rh); _rtDirty = false; }

            // —— ① 直绘：背景 + 下 + 上，全部 NonPremultiplied ——
            batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
            batch.Draw(_bgTex, left, null, BgColors[_bgIndex]); // 背景直绘
            DrawCentered(batch, _downTex, left);                // 下层（原图尺寸居中）
            DrawCentered(batch, _upTex, left);                  // 上层（原图尺寸居中）
            batch.End();

            // —— ② RT 烘焙：背景直绘 + 下+上进 RT → 上屏（AlphaBlend 读预乘）——
            batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
            batch.Draw(_bgTex, right, null, BgColors[_bgIndex]); // 背景直绘（不参与 RT）
            batch.End();
            batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
            batch.Draw(_rt!, right, null, Color.White);          // RT 合成（RT 内已是预乘 rgb）
            batch.End();

            // —— 标签 ——
            batch.Begin();
            DrawHeader(batch);
            DrawLine(batch, "① 直绘：背景 + 下 + 上 直接画（NonPremultiplied）", x0, top + colH + 8f, new Color(255, 206, 110));
            DrawLine(batch, "② RT：背景直绘 + 下+上进RT→上屏（NonPremultiplied 合成）", x0 + colW + gap, top + colH + 8f, new Color(255, 206, 110));
            DrawLine(batch,
                $"大背景颜色索引 {_bgIndex}（按空格切换）。两种混合结果应完全一致（半透明下/上层叠在背景上）。",
                x0, top + colH + 34f, new Color(150, 200, 255));
            DrawFooter(batch);
            batch.End();
        }

        // 把 下/上 按原图尺寸烘焙进 RT（NonPremultiplied：alpha 直线、颜色预乘进 RT）。
        private void BakeRT(int rw, int rh)
        {
            Device.SetRenderTarget(_rt!);
            Device.Clear(Color.Transparent);
            Batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
            DrawCentered(Batch, _downTex!, new Rectangle(0, 0, rw, rh));
            DrawCentered(Batch, _upTex!, new Rectangle(0, 0, rw, rh));
            Batch.End();
            Device.SetRenderTarget(null);
        }

        // 按纹理原图宽高，在容器内居中绘制（不拉伸）。
        private static void DrawCentered(SpriteBatch b, Texture2D tex, Rectangle container)
        {
            int w = tex.Width, h = tex.Height;
            int x = container.X + (container.Width - w) / 2;
            int y = container.Y + (container.Height - h) / 2;
            b.Draw(tex, new Rectangle(x, y, w, h), null, Color.White);
        }
    }
}
