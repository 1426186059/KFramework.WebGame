using KFramework.MonoGame;

namespace KFramework.Test.WebGL20.Tests
{
    public sealed class BlendTestScene : DemoScene
    {
        private const int Big = 256;   // RT / 纹理边长

        private Texture2D? _bottomTex; // 下层：不透明土色大底（全组共用）
        private Texture2D? _blackTex;  // ④⑤ 半透黑遮罩源（模拟传奇 半黑 dim）
        private RenderTarget2D? _rtDimBug;  // ④⑤ 中间 RT：只装半透黑遮罩（NonPremultiplied 烘 → 预乘内容）
        private Texture2D? _worldTex;  // ④⑤ 明亮世界底（衬托半透黑）
        private bool _rtDirty = true;

        public override string Title => "混合对照：三组用同一张上层图/下层图，只差底层是否不透明";

        protected override string Description => "";

        public override async Task LoadContentAsync()
        {
            await base.LoadContentAsync();
            var mBundle = await ContentManager.Default.LoadBundleAsync("Bundles");
            _bottomTex = Device.CreateTexture(Big, Big, Solid(168, 124, 80, 255)); // 不透明土色大底（全组共用）
            _blackTex = Device.CreateTexture(Big, Big, Solid(50, 50, 50, 130)); // 50 灰, ~0.51 alpha（模拟半黑 dim）
            _rtDimBug = new RenderTarget2D(Device, Big, Big);
            _worldTex = Device.CreateTexture(Big, Big, Solid(225, 225, 230, 255)); // 明亮世界底
        }

        public override void Draw()
        {
            SpriteBatch batch = Batch;

            if (_bottomTex == null || _blackTex == null || _rtDimBug == null || _worldTex == null)
            {
                batch.Begin();
                DrawHeader(batch);
                batch.DrawString(Font, "正在加载 Bundles/lefttopbg.png ...", new Vector2(20f, 110f), Color.White);
                DrawFooter(batch);
                batch.End();
                return;
            }

            if (_rtDirty) { BakeRTs(); _rtDirty = false; }

            // ===== 严格对照：三组用【同一张上层图 A】+【同一张下层图 B】，只差“底层是否不透明” =====
            float rowTop = 110f;
            float rowH = 168f;
            float boxTop = 18f;
            float aX = 165f, aSz = 104f;             // A 上层图
            float bX = 300f, bSz = 104f;             // B 下层图
            float cX = 440f, cW = 188f, cH = 104f;   // C 最终

            // 居中半透小图（显示尺寸 = 盒子一半）
            Rectangle half(Rectangle big)
            {
                int sz = (int)(big.Width / 2);
                int o = (int)((big.Width - sz) / 2);
                return new Rectangle(big.X + o, big.Y + o, sz, sz);
            }
            Rectangle aBox(float y) => new((int)aX, (int)(y + boxTop), (int)aSz, (int)aSz);
            Rectangle bBox(float y) => new((int)bX, (int)(y + boxTop), (int)bSz, (int)bSz);
            Rectangle cBox(float y) => new((int)cX, (int)(y + boxTop), (int)cW, (int)cH);

            // —— 文字层：只标 A/B/C 与组号，删掉大段说明 ——
            batch.Begin();
            DrawHeader(batch);
            void Lab(float x, float y, string s, Color c) => batch.DrawString(Font, s, new Vector2(x, y), c);
            for (int i = 0; i < 3; i++)
            {
                float y = rowTop + i * rowH;
                string name = i == 0 ? "① 参考" : i == 1 ? "④ 传奇现状(发白)" : "⑤ 修正(不透明底)";
                Color nc = i == 1 ? new Color(255, 120, 120) : i == 2 ? new Color(120, 255, 120) : new Color(255, 206, 110);
                Lab(24f, y + 44f, name, nc);
                Lab(aX, y + 2f, "A 上层图", new Color(180, 180, 180));
                Lab(bX, y + 2f, "B 下层图", new Color(180, 180, 180));
                Lab(cX, y + 2f, "C 最终", new Color(180, 180, 180));
                Lab(aX + aSz + 10f, y + boxTop + aSz / 2f - 8f, "+", Color.LightGray);
                Lab(bX + bSz + 12f, y + boxTop + bSz / 2f - 8f, "->", Color.LightGray);
                if (i == 1) Lab(bX + 6f, y + boxTop + 40f, "透明", new Color(150, 150, 150)); // ④ 下层无底层
            }
            DrawFooter(batch);
            batch.End();

            // —— 纹理层：A 全组相同(_rtDimBug)，B 全组相同(_bottomTex)，只差 C 的合成 ——
            // ① 参考：B(不透明) + A，NonPremult
            batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
            batch.Draw(_rtDimBug, aBox(rowTop), null, Color.White);
            batch.Draw(_bottomTex, bBox(rowTop), null, Color.White);
            batch.Draw(_bottomTex, cBox(rowTop), null, Color.White);
            batch.Draw(_rtDimBug, half(cBox(rowTop)), null, Color.White);
            batch.End();

            // ④ 现状(bug)：无透明底层，半透 A 上屏后 alpha<1，白底(页面)透出 → 发白
            float y4 = rowTop + rowH;
            batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
            batch.Draw(_rtDimBug, aBox(y4), null, Color.White);
            batch.Draw(_worldTex, cBox(y4), null, Color.White); // 白底(页面)
            batch.Draw(_rtDimBug, half(cBox(y4)), null, Color.White);
            batch.End();

            // ⑤ 修正：B(不透明) + A，NonPremult（与①同图，证明“底层不透明即可”）
            float y5 = rowTop + 2 * rowH;
            batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
            batch.Draw(_rtDimBug, aBox(y5), null, Color.White);
            batch.Draw(_bottomTex, bBox(y5), null, Color.White);
            batch.Draw(_bottomTex, cBox(y5), null, Color.White);
            batch.Draw(_rtDimBug, half(cBox(y5)), null, Color.White);
            batch.End();
        }

        // 折行：优先在空格处断（英文单词不乱切），否则按字符断（中文无空格也能折行，不再整行溢出）。
        private static List<string> Wrap(IFont font, string text, float maxW)
        {
            var lines = new List<string>();
            var sb = "";
            foreach (char ch in text)
            {
                if (ch == ' ' && sb.Length > 0)
                {
                    lines.Add(sb);
                    sb = "";
                    continue;
                }
                string test = sb + ch;
                if (font.Measure(test).X > maxW && sb.Length > 0)
                {
                    lines.Add(sb);
                    sb = ch == ' ' ? "" : ch.ToString();
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

        // 烘焙 RT：只烘 ④⑤ 用的 dim RT（半透黑遮罩，NonPremultiplied 烘 → 预乘内容）
        private void BakeRTs()
        {
            Device.SetRenderTarget(_rtDimBug);
            Device.Clear(Color.Transparent);
            Batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);
            Batch.Draw(_blackTex, new Rectangle(0, 0, Big, Big), null, Color.White);
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
