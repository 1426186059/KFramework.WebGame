using KFramework.MonoGame;

namespace KFramework.Test.WebGL20.Tests
{
    public sealed class BlendTestScene : DemoScene
    {
        private const int Big = 256;   // 底层 RT / 底层纹理边长（大）
        private const int Small = 128; // 上层 RT 边长（小，明显小于底层）

        private Texture2D? _tex;       // 上层：Bundles/lefttopbg.png（半透明黑图）
        private Texture2D? _bottomTex; // 下层：不透明纯色大底（土色，衬托半透明）
        private RenderTarget2D? _rtBottom;  // ②/③ 底层 RT（大）
        private RenderTarget2D? _rtTop;     // ②/③ 上层 RT（小）
        private RenderTarget2D? _rtCombined;// ③ 嵌套合成后的底层 RT
        private bool _rtDirty = true;

        public override string Title => "混合对比：① 正常直绘 / ② 两个RT / ③ 嵌套RT";

        protected override string Description
            => "只用默认混合：直绘=NonPremultiplied；合成RT=AlphaBlend。下层比上层大，混合居中。三种情况结果应一致。";

        public override async Task LoadContentAsync()
        {
            await base.LoadContentAsync();
            var mBundle = await ContentManager.Default.LoadBundleAsync("Bundles");
            _tex = mBundle.LoadTexture("Bundles/lefttopbg.png");
            _bottomTex = Device.CreateTexture(Big, Big, Solid(168, 124, 80, 255)); // 不透明土色大底
            _rtBottom = new RenderTarget2D(Device, Big, Big);
            _rtTop = new RenderTarget2D(Device, Small, Small);
            _rtCombined = new RenderTarget2D(Device, Big, Big);
        }

        public override void Draw()
        {
            SpriteBatch batch = Batch;

            if (_tex == null || _bottomTex == null || _rtBottom == null || _rtTop == null || _rtCombined == null)
            {
                batch.Begin();
                DrawHeader(batch);
                batch.DrawString(Font, "正在加载 Bundles/lefttopbg.png ...", new Vector2(20f, 110f), Color.White);
                DrawFooter(batch);
                batch.End();
                return;
            }

            if (_rtDirty) { BakeRTs(); _rtDirty = false; }

            float top = 96f;
            float cell = 300f, gap = 46f;
            float x0 = 30f;
            Rectangle rc1 = new((int)x0, (int)top, (int)cell, (int)cell);
            Rectangle rc2 = new((int)(x0 + cell + gap), (int)top, (int)cell, (int)cell);
            Rectangle rc3 = new((int)(x0 + 2 * (cell + gap)), (int)top, (int)cell, (int)cell);

            // 上层小图在小矩形内居中（显示尺寸 = 底层显示的一半，保证「下层比上层大」）
            int smallView = (int)(cell / 2);
            int off = (int)((cell - smallView) / 2);
            Rectangle smallAt(Rectangle big) => new(big.X + off, big.Y + off, smallView, smallView);

            // —— ① 正常直绘（默认混合 NonPremultiplied）——
            batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
            batch.Draw(_bottomTex, rc1, null, Color.White); // 大底
            batch.Draw(_tex, smallAt(rc1), null, Color.White); // 小上层（居中）
            batch.End();

            // —— ② 两个 RT 分别合成到屏幕（RT 内预乘，合成用 AlphaBlend）——
            batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            batch.Draw(_rtBottom, rc2, null, Color.White);
            batch.Draw(_rtTop, smallAt(rc2), null, Color.White);
            batch.End();

            // —— ③ 嵌套 RT：最终只呈现底层 RT（上层 RT 已合成进其中）——
            batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            batch.Draw(_rtCombined, rc3, null, Color.White);
            batch.End();

            // —— 标签与说明（自动折行，避免横向重叠）——
            batch.Begin();
            DrawHeader(batch);

            float lh = Font.LineSpacing + 4f;
            float titleY = top + cell + 8f;
            float descY = titleY + lh + 4f;
            float colW = cell;

            // 列标题（短，单行）
            batch.DrawString(Font, "① 正常直绘", new Vector2(x0, titleY), new Color(255, 206, 110));
            batch.DrawString(Font, "② 两个RT", new Vector2(x0 + cell + gap, titleY), new Color(255, 206, 110));
            batch.DrawString(Font, "③ 嵌套RT", new Vector2(x0 + 2 * (cell + gap), titleY), new Color(255, 206, 110));

            // 列说明（折行，限制在本列宽度内）
            DrawWrapped(batch, "大底 + 小上层 直接画屏幕（NonPremultiplied）", new Vector2(x0, descY), Color.LightGray, colW);
            DrawWrapped(batch, "底层RT + 上层RT 分别合成到屏幕（AlphaBlend）", new Vector2(x0 + cell + gap, descY), Color.LightGray, colW);
            DrawWrapped(batch, "把上层RT绘进底层RT，最终只呈现底层RT", new Vector2(x0 + 2 * (cell + gap), descY), Color.LightGray, colW);

            // 底部总结（横跨，折行）
            float sumY = descY + 3 * lh + 12f;
            float sumW = (x0 + 3 * (cell + gap)) - x0 - 10f;
            DrawWrapped(batch,
                "下层(大)比上层(小)大，混合居中；三种情况结果应完全一致（半透明黑压暗大底中央）。",
                new Vector2(x0, sumY), new Color(150, 200, 255), sumW);
            DrawWrapped(batch,
                "区别：直绘默认 = NonPremultiplied；合成 RT = AlphaBlend（RT 内预乘 rgb，颜色源=One 读，不能二次预乘）。",
                new Vector2(x0, sumY + 2 * lh + 6f), Color.LightGray, sumW);

            DrawFooter(batch);
            batch.End();
        }

        // 按最大宽度折行：以空格分词，超过 maxW 则换行。
        private static List<string> Wrap(IFont font, string text, float maxW)
        {
            var lines = new List<string>();
            var sb = "";
            foreach (string word in text.Split(' '))
            {
                string test = sb.Length == 0 ? word : sb + " " + word;
                if (font.Measure(test).X > maxW && sb.Length > 0)
                {
                    lines.Add(sb);
                    sb = word;
                }
                else sb = test;
            }
            if (sb.Length > 0) lines.Add(sb);
            return lines;
        }

        private void DrawWrapped(SpriteBatch b, string text, Vector2 pos, Color color, float maxW)
        {
            float lh = Font.LineSpacing + 4f;
            foreach (string ln in Wrap(Font, text, maxW))
            {
                b.DrawString(Font, ln, pos, color);
                pos.Y += lh;
            }
        }

        // 烘焙 RT：底层（大土色）、上层（小 lefttopbg）、以及「嵌套合成」的底层 RT。
        // 全部用 NonPremultiplied 烘焙（保证 RT 内 alpha 直线、颜色预乘）；嵌套那一步改用 AlphaBlend 读预乘上层 RT。
        private void BakeRTs()
        {
            // 底层 RT（大，土色，不透明）
            Device.SetRenderTarget(_rtBottom);
            Device.Clear(Color.Transparent);
            Batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
            Batch.Draw(_bottomTex, new Rectangle(0, 0, Big, Big), null, Color.White);
            Batch.End();
            Device.SetRenderTarget(null);

            // 上层 RT（小，lefttopbg 半透明）
            Device.SetRenderTarget(_rtTop);
            Device.Clear(Color.Transparent);
            Batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
            Batch.Draw(_tex, new Rectangle(0, 0, Small, Small), null, Color.White);
            Batch.End();
            Device.SetRenderTarget(null);

            // 嵌套 RT：土色大底 + 把上层 RT 绘制进底层 RT（在 RT 上再合成一层）
            Device.SetRenderTarget(_rtCombined);
            Device.Clear(Color.Transparent);
            Batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
            Batch.Draw(_bottomTex, new Rectangle(0, 0, Big, Big), null, Color.White);
            Batch.End();
            // 底层 RT 仍激活：用 AlphaBlend 把上层 RT（预乘 rgb）合成进来
            int m = (Big - Small) / 2;
            Batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            Batch.Draw(_rtTop, new Rectangle(m, m, Small, Small), null, Color.White);
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
