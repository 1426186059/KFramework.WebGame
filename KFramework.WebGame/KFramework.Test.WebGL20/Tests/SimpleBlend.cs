using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.WebGL20.Tests
{
    /// <summary>
    /// SimpleBlend（独立模块）：大背景（颜色可调，直绘）+ 混合下层(lefttop_leftbg2) + 混合上层(lefttop_leftbg1)，
    /// 三种情况对照：
    /// <para>① 直绘 Bundle：背景 + 下(Bundle) + 上(Bundle) 直接画（DefaultBlend）。</para>
    /// <para>② RT 烘焙 Bundle：背景直绘 + 把 下+上(Bundle) 渲染进 RT，再把 RT 合成到屏幕（DefaultBlend 读预乘）。</para>
    /// <para>③ 直绘 外部PNG：背景 + 下(外部原始PNG，绕开Bundle打包) + 上(Bundle) 直接画（DefaultBlend）。</para>
    /// <para>下层/上层用图集原图尺寸绘制（不拉伸），背景始终直绘。①③同为直绘、仅下层来源不同，用于对照白框清晰度。</para>
    /// </summary>
    public sealed class SimpleBlend : DemoScene
    {
        private Texture2D? _bgTex;       // 大背景（1x1 白，用 tint 上色，颜色可调，直绘）
        private Texture2D? _downTex;     // 混合下层：Bundles/y_lefttop-sheet/lefttop_leftbg2
        private Texture2D? _upTex;       // 混合上层：Bundles/y_lefttop-sheet/lefttop_leftbg1
        private Texture2D? _extDownTex;  // 混合下层（对照）：Bundle 之外的原始 PNG（内容管线原样输出，不解码不预乘）
        private RenderTarget2D? _rt;     // ② 烘焙 RT（只含 下+上）
        private bool _rtDirty = true;

        private static readonly Color[] BgColors =
        [
            new Color(40, 44, 60),
            new Color(92, 60, 50),
            new Color(50, 82, 60),
            new Color(82, 82, 94),
        ];
        private int _bgIndex;

        public override string Title => "SimpleBlend：直绘Bundle vs RT烘焙 vs 直绘外部PNG";

        protected override string Description
            => "① 直绘Bundle ② RT烘焙Bundle ③ 直绘外部PNG（同图绕开Bundle打包）。按空格切背景色。①③对照白框清晰度。";

        public override async Task LoadContentAsync()
        {
            await base.LoadContentAsync();
            var mBundle = await ContentManager.Default.LoadBundleAsync("Bundles");
            _downTex = mBundle.LoadTexture("Bundles/y_lefttop-sheet/lefttop_leftbg2.png");
            _upTex = mBundle.LoadTexture("Bundles/y_lefttop-sheet/lefttop_leftbg1.png");
            _bgTex = Device.CreateTexture(1, 1, new byte[] { 255, 255, 255, 255 });

            // 第③种：Bundle 之外的原始 PNG。内容管线 CopyRawAssets 会把它原样输出到
            // hot_update_res/y_lefttop-sheet/（不解码、不预乘），运行时由浏览器解码成非预乘 RGBA，
            // 从而绕开 Bundle 打包这一环 —— 与 ① 的唯一区别就是“是否经过 Bundle”。
            _extDownTex = await ContentManager.Default.LoadTexture2DAsync("y_lefttop-sheet/lefttop_leftbg2.png", Device);
        }

        public override void Update()
        {
            if (Input_KeyBoard.GetKeyDown(Keys.Space))
                _bgIndex = (_bgIndex + 1) % BgColors.Length;
        }

        public override void Draw()
        {
            SpriteBatch batch = Batch;

            if (_bgTex == null || _downTex == null || _upTex == null || _extDownTex == null)
            {
                batch.Begin();
                DrawHeader(batch);
                batch.DrawString(Font, "正在加载资源（Bundles + 外部PNG）...", new Vector2(20f, 110f), Color.White);
                DrawFooter(batch);
                batch.End();
                return;
            }

            float top = 96f;
            float gap = 24f, x0 = 24f;
            const int n = 3;
            float colW = (Device.Viewport.Width - x0 * 2 - gap * (n - 1)) / n;
            float colH = Device.Viewport.Height - top - 150f;
            var cols = new RectangleF[n];
            for (int i = 0; i < n; i++)
                cols[i] = new RectangleF(x0 + i * (colW + gap), top, colW, colH);

            // 懒创建/重建 RT（尺寸随列尺寸；仅尺寸变化时重建）
            int rw = (int)colW, rh = (int)colH;
            if (_rt == null || _rt.Width != rw || _rt.Height != rh)
            {
                _rt?.Dispose();
                _rt = new RenderTarget2D(Device, rw, rh);
                _rtDirty = true;
            }
            if (_rtDirty) { BakeRT(rw, rh); _rtDirty = false; }

            // —— ① 直绘 Bundle：背景 + 下(Bundle) + 上(Bundle) ——
            batch.Begin(SpriteSortMode.Deferred, BlendState.DefaultBlend);
            batch.Draw(_bgTex, cols[0], null, BgColors[_bgIndex]);
            DrawCentered(batch, _downTex, cols[0]);
            DrawCentered(batch, _upTex, cols[0]);
            batch.End();

            // —— ② RT 烘焙 Bundle：背景直绘 + 下+上(Bundle)进 RT → 上屏（DefaultBlend 读预乘）——
            batch.Begin(SpriteSortMode.Deferred, BlendState.DefaultBlend);
            batch.Draw(_bgTex, cols[1], null, BgColors[_bgIndex]); // 背景直绘（不参与 RT）
            batch.End();
            batch.Begin(SpriteSortMode.Deferred, BlendState.DefaultBlend);
            batch.Draw(_rt!, cols[1], null, Color.White);          // RT 合成（RT 内已是预乘 rgb）
            batch.End();

            // —— ③ 直绘 外部PNG：背景 + 下(外部) + 上(Bundle) ——
            batch.Begin(SpriteSortMode.Deferred, BlendState.DefaultBlend);
            batch.Draw(_bgTex, cols[2], null, BgColors[_bgIndex]);
            DrawCentered(batch, _extDownTex, cols[2]);
            DrawCentered(batch, _upTex, cols[2]);
            batch.End();

            // —— 标签 ——
            batch.Begin();
            DrawHeader(batch);
            DrawLine(batch, "① 直绘 Bundle（leftbg2 来自Bundle）", cols[0].X, top + colH + 8f, new Color(255, 206, 110));
            DrawLine(batch, "② RT 烘焙 Bundle", cols[1].X, top + colH + 8f, new Color(255, 206, 110));
            DrawLine(batch, "③ 直绘 外部PNG（leftbg2 未打包）", cols[2].X, top + colH + 8f, new Color(255, 206, 110));
            DrawLine(batch,
                $"空格切背景 {_bgIndex}。①与③同为直绘、仅下层来源不同（Bundle vs 外部PNG），对照白框清晰度是否一致。",
                x0, top + colH + 34f, new Color(150, 200, 255));
            DrawFooter(batch);
            batch.End();
        }

        // 把 下/上(Bundle) 按原图尺寸烘焙进 RT（alpha 直线、颜色预乘进 RT）。
        private void BakeRT(int rw, int rh)
        {
            Device.SetRenderTarget(_rt!);
            Device.Clear(Color.Transparent);
            Batch.Begin(SpriteSortMode.Deferred, BlendState.DefaultBlend);
            DrawCentered(Batch, _downTex!, new Rectangle(0, 0, rw, rh));
            DrawCentered(Batch, _upTex!, new Rectangle(0, 0, rw, rh));
            Batch.End();
            Device.SetRenderTarget(null);
        }

        // 按纹理原图宽高，在容器内居中绘制（不拉伸）。容器为浮点 RectangleF，保留亚像素精度。
        private static void DrawCentered(SpriteBatch b, Texture2D tex, RectangleF container)
        {
            float w = tex.Width, h = tex.Height;
            float x = container.X + (container.Width - w) / 2;
            float y = container.Y + (container.Height - h) / 2;
            b.Draw(tex, new RectangleF(x, y, w, h), null, Color.White);
        }
    }
}
