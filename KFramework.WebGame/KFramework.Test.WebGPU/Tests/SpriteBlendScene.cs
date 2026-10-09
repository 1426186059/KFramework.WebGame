using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.WebGPU.Tests
{

    /// <summary>
    /// 混合模式对比。
    /// <para>
    /// 与 WebGL 的关键差异：WebGPU 把混合因子<b>固化在 GPURenderPipeline 里</b>，
    /// 不能像 glBlendFunc 那样随时切换，因此每种混合对应一条独立管线 ——
    /// 后端按「四个混合因子」做键缓存管线，这里每换一个模式就是一次换管线。
    /// </para>
    /// </summary>
    public sealed class SpriteBlendScene : DemoScene
    {
        public override string Title => "2) 混合模式（每种混合 = 一条独立管线）";

        protected override string Description
            => "WebGPU 的混合因子固化在管线对象里，故每种混合各建一条管线（后端按混合参数缓存复用）";

        private Texture2D? _tex;

        private static readonly (string Name, BlendState State, Color Tint)[] Modes =
        [
            ("NonPremultiplied", BlendState.NonPremultiplied, new Color(255, 140, 0, 128)),
            ("AlphaBlend（预乘）", BlendState.AlphaBlend, new Color(255, 140, 0, 128)),
            ("Additive（加法发光）", BlendState.Additive, new Color(255, 160, 40, 255)),
            ("Opaque（覆盖）", BlendState.Opaque, new Color(120, 200, 255, 255)),
            ("Multiply（乘法压暗）", BlendState.Multiply, new Color(70, 80, 100, 255)),
        ];

        public override void Draw()
        {
            SpriteBatch batch = Batch;
            // 用 alpha 渐变图：不透明度从左到右递减，各混合模式的差异一眼可辨。
            _tex ??= MakeAlphaChart(48);

            batch.Begin();
            DrawHeader(batch);
            batch.End();

            float y = 110f;
            foreach ((string name, BlendState state, Color tint) in Modes)
            {
                // 每个混合模式一个独立批次（WebGPU 下即一条独立管线）。
                batch.Begin(SpriteSortMode.Deferred, state);
                batch.DrawString(Font, name, new Vector2(28f, y), new Color(220, 230, 250));
                for (int i = 0; i < 6; i++)
                    batch.Draw(_tex, new Vector2(280f + i * 56f, y - 6f), color: tint);
                batch.End();

                y += 62f;
            }

            batch.Begin();
            DrawFooter(batch);
            batch.End();
        }
    }

}
