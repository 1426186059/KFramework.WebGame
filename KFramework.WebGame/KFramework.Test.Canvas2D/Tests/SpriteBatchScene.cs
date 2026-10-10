using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.Canvas2D.Tests
{

    /// <summary>
    /// 精灵批绘制：同一张纹理的精灵由 <see cref="SpriteBatch"/> 合并提交，Canvas2D 后端在提交时
    /// 逐四边形 drawImage（每个四边形用一个"单位方格 → 四边形"的仿射）。
    /// <para>
    /// 覆盖旋转 / 缩放 / 水平与垂直翻转 / 顶点色。顶点色会走"着色副本"路径 —— Canvas2D 没有逐绘制的
    /// tint，非白色会在 TS 侧按 (纹理 + RGB) 缓存一份着色后的离屏副本。
    /// </para>
    /// </summary>
    public sealed class SpriteBatchScene : DemoScene
    {
        public override string Title => "1) 精灵批绘制 / 旋转 / 缩放 / 翻转 / 顶点色";

        protected override string Description
            => "同纹理精灵合并提交；Canvas2D 用「单位方格 → 四边形」的仿射把每个四边形 drawImage 出来";

        private Texture2D? _tex;
        private int _frame;

        public override void Update() => _frame++;

        protected override void DrawBody(SpriteBatch batch, float top)
        {
            _tex ??= MakeChecker(32, new Color(90, 170, 250), new Color(28, 42, 78));
            Texture2D tex = _tex;

            float y = DrawLine(batch,
                $"后端 {Device.BackendName}    FPS {KTime.realFps:0.0}    DrawCall {Device.Metrics.DrawCount}    精灵 {Device.Metrics.SpriteCount}",
                28f, top, new Color(255, 206, 110));

            // ① 旋转网格（22×6 = 132 个精灵，同一张纹理）
            y = DrawLine(batch, "① 旋转（同纹理合并提交）", 28f, y + 4f, new Color(180, 220, 255));
            const int cols = 22;
            const int rows = 6;
            const float step = 34f;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    var center = new Vector2(28f + c * step + step * 0.5f, y + r * step + step * 0.5f);
                    float rotation = (r + c) * 0.05f + _frame * 0.01f;
                    batch.DrawCentered(tex, center, Color.White, rotation, 1f);
                }
            }
            y += rows * step + 8f;

            // ② 缩放（位置形态：scale 直接给）
            y = DrawLine(batch, "② 缩放 0.5 / 1 / 1.5 / 2（位置形态的 scale 参数）", 28f, y, new Color(180, 220, 255));
            float sx = 28f;
            float[] scales = [0.5f, 1f, 1.5f, 2f];
            foreach (float scale in scales)
            {
                batch.Draw(tex, new Vector2(sx, y), rotation: 0f, scale: new Vector2(scale, scale), color: Color.White);
                sx += 32f * scale + 18f;
            }
            y += 32f * 2f + 12f;

            // ③ 翻转（翻转只在"目标矩形形态"里给，位置形态没有 effects 参数）
            y = DrawLine(batch, "③ 翻转：正常 / 水平 / 垂直 / 水平+垂直", 28f, y, new Color(180, 220, 255));
            SpriteEffects[] flips =
            [
                SpriteEffects.None,
                SpriteEffects.FlipHorizontally,
                SpriteEffects.FlipVertically,
                SpriteEffects.FlipHorizontally | SpriteEffects.FlipVertically,
            ];
            sx = 28f;
            foreach (SpriteEffects effects in flips)
            {
                batch.Draw(tex, new Rectangle((int)sx, (int)y, 48, 48), null, Color.White,
                           0f, Vector2.Zero, null, effects, 0f);
                sx += 64f;
            }
            y += 62f;

            // ④ 顶点色（Canvas2D 下走"着色副本"缓存路径）
            y = DrawLine(batch, "④ 顶点色（非白色会走 Canvas2D 的着色副本路径）", 28f, y, new Color(180, 220, 255));
            for (int i = 0; i < 16; i++)
            {
                float hue = (i / 16f + _frame * 0.002f) % 1f;
                batch.Draw(tex, new Vector2(28f + i * 40f, y), color: Hsv(hue, 0.75f, 1f));
            }

            DrawLine(batch,
                "Canvas2D：每个四边形一次 drawImage；混合由 globalCompositeOperation 承担，裁剪由 clip 承担",
                28f, y + 56f, new Color(150, 165, 195));
        }

        /// <summary>HSV → RGB（色相 0..1，饱和 / 明度 0..1）。</summary>
        private static Color Hsv(float h, float s, float v)
        {
            float c = v * s;
            float x = c * (1f - MathF.Abs(h * 6f % 2f - 1f));
            float m = v - c;

            (float r, float g, float b) = (int)(h * 6f) switch
            {
                0 => (c, x, 0f),
                1 => (x, c, 0f),
                2 => (0f, c, x),
                3 => (0f, x, c),
                4 => (x, 0f, c),
                _ => (c, 0f, x),
            };

            return new Color((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
        }
    }

}
