using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.Canvas2D.Tests
{

    /// <summary>
    /// 文字与字形：<see cref="SpriteFont"/> 的字形由浏览器 Canvas2D 光栅化后写进引擎图集，
    /// 绘制时与普通精灵同路（白色字形 + 顶点色着色）—— 在 Canvas2D 后端就是逐个字形 drawImage。
    /// <para>
    /// 顺带验证三件事：字形图集的<b>页数</b>（多图集）、<b>局部上传</b>（每个新字形一次 putImageData）、
    /// 以及<b>着色路径</b>（彩色文字会生成着色副本）。
    /// </para>
    /// </summary>
    public sealed class TextScene : DemoScene
    {
        public override string Title => "2) 文字与字形（SpriteFont）";

        protected override string Description
            => "字形由浏览器光栅化进图集，再作为普通精灵绘制；白色字形靠顶点色着色";

        private SpriteFont? _f24;
        private SpriteFont? _f32;
        private SpriteFont? _f48;
        private SpriteFont? _fBold;
        private int _frame;

        public override void LoadContent()
        {
            _f24 = new SpriteFont(Device, 24f);
            _f32 = new SpriteFont(Device, 32f);
            _f48 = new SpriteFont(Device, 48f);
            _fBold = new SpriteFont(Device, 28f, "system-ui, sans-serif", FontStyle.Bold);
        }

        public override void Dispose()
        {
            _f24?.Dispose();
            _f32?.Dispose();
            _f48?.Dispose();
            _fBold?.Dispose();
            base.Dispose();
        }

        public override void Update() => _frame++;

        protected override void DrawBody(SpriteBatch batch, float top)
        {
            if (_f24 is null) return;

            float y = DrawLine(batch,
                $"后端 {Device.BackendName}    FPS {KTime.realFps:0.0}    字形图集 {_f24.AtlasCount} 页（1024²）    字符数 {Device.Metrics.SpriteCount}",
                28f, top, new Color(255, 206, 110));

            y = DrawLine(batch, "① 字号与测量（Measure 的宽高即文字实际占位）", 28f, y + 4f, new Color(180, 220, 255));

            const string sample = "The quick brown fox 0123456789 · 中文混排：你好，世界！";
            y += DrawGlyphLine(batch, _f24, sample, 28f, y, Color.White);
            y += DrawGlyphLine(batch, _f32, sample, 28f, y, Color.White);
            y += DrawGlyphLine(batch, _f48, "SpriteFont 48px 中文 你好", 28f, y, Color.White);

            Vector2 size = _f24.Measure(sample);
            y = DrawLine(batch, $"Measure(24px) = {size.X:F0} × {size.Y:F0}    行高 {_f24.LineSpacing:F1}", 28f, y, Color.LightGray);

            // ② 着色：验证 Canvas2D 的"按颜色缓存着色副本"路径（含随时间变化的颜色）
            y = DrawLine(batch, "② 顶点色着色（每个颜色会在 TS 侧缓存一份着色副本）", 28f, y + 8f, new Color(180, 220, 255));
            Color[] palette = [Color.White, Color.Red, Color.Green, Color.Cyan, new Color(255, 206, 110), new Color(180, 150, 255)];
            float x = 28f;
            foreach (Color color in palette)
            {
                x = DrawInline(batch, _f24, "色", x, y, color);
            }
            y += _f24.LineSpacing + 10f;

            float hue = (_frame * 0.004f) % 1f;
            batch.DrawString(_fBold!, "粗体 + 随时间变化的颜色", new Vector2(28f, y), FromHsv(hue));
            y += _fBold!.LineSpacing + 12f;

            // ③ TextRenderer：WordBreak 自动折行 + 居中（与 WinForms 语义对齐）
            y = DrawLine(batch, "③ TextRenderer：WordBreak 折行 + 水平居中 + 底色", 28f, y, new Color(180, 220, 255));
            var panel = new Rectangle((int)28f, (int)y, Math.Max(200, Device.Viewport.Width - 56), 110);
            batch.Draw(KDefaultRes.DefaultTexture2D, panel, null, new Color(24, 30, 46));
            TextRenderer.DrawText(batch,
                "这是一段用于演示自动折行的中文与 English 混排文本，超出宽度时按词 / 逐字换行，"
                + "并在面板里水平居中。Canvas2D 后端下每个字形同样是一次 drawImage。",
                _f24, new Rectangle(panel.X + 10, panel.Y + 8, panel.Width - 20, panel.Height - 16),
                Color.White, TextFormatFlags.WordBreak | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        /// <summary>用某个字体画一行（含测量出的高度），返回占用高度。</summary>
        private static float DrawGlyphLine(SpriteBatch batch, SpriteFont font, string text, float x, float y, Color color)
        {
            batch.DrawString(font, text, new Vector2(x, y), color);
            return font.LineSpacing + 6f;
        }

        private static Color FromHsv(float h)
        {
            float cs = MathF.Cos(h * MathF.Tau);
            float sn = MathF.Sin(h * MathF.Tau);
            return new Color((byte)(128 + 127 * cs), (byte)(128 + 127 * sn), (byte)(200), (byte)255);
        }
    }

}
