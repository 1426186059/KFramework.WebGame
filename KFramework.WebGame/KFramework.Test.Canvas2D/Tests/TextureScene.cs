using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.Canvas2D.Tests
{

    /// <summary>
    /// 纹理路径：创建 / 整张上传 / <b>局部上传</b>（每帧更新一小块）/ 采样方式对比（Point ⇄ Linear）/
    /// 混合模式（加色 / 正片叠底）/ 读像素自检。
    /// <para>
    /// Canvas2D 后端里每张纹理就是一张离屏 canvas：整张上传是 putImageData，局部上传是带坐标的 putImageData，
    /// 采样开关对应 imageSmoothingEnabled，读像素走 getImageData。
    /// </para>
    /// <para>
    /// 本页重写 <see cref="DemoScene.Draw"/> 自己管 Begin / End —— 采样器与混合状态都挂在批次上，
    /// 要对照就必须分成多批。
    /// </para>
    /// </summary>
    public sealed class TextureScene : DemoScene
    {
        public override string Title => "3) 纹理上传与采样（整张 / 局部 / Point⇄Linear / 混合 / 读像素）";

        protected override string Description
            => "纹理 = 一张离屏 canvas；SetData 走 putImageData（含局部更新），采样开关对应 imageSmoothingEnabled";

        /// <summary>加色：源 alpha / 一（对应 Canvas2D 的 'lighter'）。</summary>
        private static readonly BlendState Additive = new(BlendMode.SrcAlpha, BlendMode.One, BlendMode.One, BlendMode.One);

        /// <summary>正片叠底：目标色 / 零（对应 Unity 的 Blend DstColor Zero，Canvas2D 的 'multiply'）。</summary>
        private static readonly BlendState Multiply = new(BlendMode.DstColor, BlendMode.Zero, BlendMode.One, BlendMode.One);

        private const int PatchSize = 24;

        private Texture2D? _tex;
        private int _frame;
        private string _probeText = "读像素：—";
        private readonly byte[] _patch = new byte[PatchSize * PatchSize * 4];

        public override void Update() => _frame++;

        public override void Draw()
        {
            _tex ??= MakeChecker(64, new Color(230, 130, 60), new Color(44, 32, 22));
            Texture2D tex = _tex;

            UpdatePatch(tex);

            SpriteBatch batch = Batch;

            // ① 点采样（像素风）
            batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp);
            DrawHeader(batch);
            DrawSamplerRow(batch, tex, 96f, "① 点采样 Point（放大 4 倍：硬边像素块）");
            batch.End();

            // ② 线性采样（同样的放大倍数，边缘平滑）
            batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.Linear);
            DrawSamplerRow(batch, tex, 244f, "② 线性采样 Linear（同样放大 4 倍：平滑过渡）");
            batch.End();

            // ③ 局部上传（每帧只改右下角一小块）
            batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp);
            DrawPatchDemo(batch, tex, 392f);
            batch.End();

            // ④ 混合模式：加色 / 正片叠底（同一对叠压精灵，只换混合状态）
            batch.Begin(SpriteSortMode.Deferred, Additive, SamplerState.PointClamp);
            DrawBlendDemo(batch, tex, 528f, "④ 加色 Additive（'lighter'）");
            batch.End();

            batch.Begin(SpriteSortMode.Deferred, Multiply, SamplerState.PointClamp);
            DrawBlendDemo(batch, tex, 528f + 150f, "⑤ 正片叠底 Multiply（'multiply'）");
            batch.End();

            // ⑥ 读像素自检
            batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp);
            DrawReadback(batch, tex);
            DrawFooter(batch);
            batch.End();
        }

        private void DrawSamplerRow(SpriteBatch batch, Texture2D tex, float y, string caption)
        {
            DrawLine(batch, caption, 28f, y, new Color(180, 220, 255));

            float x = 28f;
            for (int i = 0; i < 5; i++)
            {
                float scale = 1f + i;   // 原尺寸 → 5 倍
                batch.Draw(tex, new Vector2(x, y + 26f), rotation: 0f, scale: new Vector2(scale, scale), color: Color.White);
                x += 64f * scale + 14f;
            }
        }

        private void DrawPatchDemo(SpriteBatch batch, Texture2D tex, float y)
        {
            DrawLine(batch, "③ 局部上传：每帧用 SetData(..., x, y, 24, 24) 只更新右下角一小块（Canvas2D → putImageData）",
                     28f, y, new Color(180, 220, 255));
            batch.Draw(tex, new Vector2(28f, y + 26f), rotation: 0f, scale: new Vector2(3f, 3f), color: Color.White);
            DrawLine(batch, "整张仍是 1 次上传；局部更新不会重传整张纹理", 236f, y + 34f, new Color(150, 165, 195));
        }

        private void DrawBlendDemo(SpriteBatch batch, Texture2D tex, float y, string caption)
        {
            DrawLine(batch, caption, 28f, y, new Color(180, 220, 255));

            // 两个半透明精灵叠压：背景亮块 + 前景色块，混合模式的区别一眼可见
            batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(28, (int)y + 26, 90, 90), null, new Color(120, 170, 255, 220));
            for (int i = 0; i < 3; i++)
            {
                batch.Draw(tex, new Vector2(70f + i * 22f, y + 48f), color: new Color(255, 200, 90, 200));
            }
        }

        private void DrawReadback(SpriteBatch batch, Texture2D tex)
        {
            // 在固定位置画一块已知颜色，再把它读回来对照（Canvas2D 走 getImageData，画布内容即时可见）
            const int px = 620;
            int py = (int)(Device.Viewport.Height - 120f);
            Color expected = new(200, 70, 70);
            batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(px, py, 40, 40), null, expected);

            if ((_frame & 15) == 0)
            {
                Color got = Device.ReadPixel(px + 20, py + 20);
                _probeText = $"{(_probeText.StartsWith("读像素：✓") ? "读像素：✓ " + expected : "读像素：")}  期望 ({expected.R},{expected.G},{expected.B})  实读 ({got.R},{got.G},{got.B},{got.A})";
            }
            else
            {
                Color got = Device.ReadPixel(px + 20, py + 20);
                bool ok = Math.Abs(got.R - expected.R) <= 2 && Math.Abs(got.G - expected.G) <= 2 && Math.Abs(got.B - expected.B) <= 2;
                _probeText = $"{(ok ? "读像素：✓" : "读像素：✗")}  期望 ({expected.R},{expected.G},{expected.B})  实读 ({got.R},{got.G},{got.B},{got.A})";
            }

            batch.DrawString(Font, _probeText, new Vector2(28f, py + 8f), new Color(255, 206, 110));
        }

        /// <summary>把右下角那一小块填成随时间变化的颜色（局部上传的实际数据）。</summary>
        private void UpdatePatch(Texture2D tex)
        {
            if (_tex is null) return;

            int t = _frame / 2;
            byte r = (byte)(128 + 127 * Math.Sin(t * 0.07));
            byte g = (byte)(128 + 127 * Math.Sin(t * 0.11 + 2.1));
            byte b = (byte)(128 + 127 * Math.Sin(t * 0.13 + 4.2));

            for (int i = 0; i < PatchSize * PatchSize; i++)
            {
                _patch[i * 4 + 0] = r;
                _patch[i * 4 + 1] = g;
                _patch[i * 4 + 2] = b;
                _patch[i * 4 + 3] = 255;
            }

            // 位置固定在右下角：验证"只更新这一块"，其余部分保持首次上传的棋盘格
            tex.SetData(_patch, tex.Width - PatchSize, tex.Height - PatchSize, PatchSize, PatchSize);
        }
    }

}
