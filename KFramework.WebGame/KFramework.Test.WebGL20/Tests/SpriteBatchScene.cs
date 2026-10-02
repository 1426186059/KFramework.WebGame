using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.WebGL20.Tests
{

    /// <summary>
    /// 精灵批绘制：连续使用同一张纹理的精灵会被 SpriteBatcher 合并为一次 draw call。
    /// WebGL2 后端走「动态顶点缓冲 BufferSubData + 静态四边形索引 DrawElements」。
    /// </summary>
    public sealed class SpriteBatchScene : DemoScene
    {
        public override string Title => "1) 精灵批绘制 / 旋转";

        protected override string Description
            => "连续同纹理的精灵合并为一次 draw call；下方网格共 220 个精灵，只产生极少量提交";

        private Texture2D? _tex;
        private int _frame;

        public override void Update() => _frame++;

        protected override void DrawBody(SpriteBatch batch, float top)
        {
            _tex ??= MakeChecker(32, new Color(90, 170, 250), new Color(28, 42, 78));

            float y = DrawLine(batch, $"FPS: {KTime.realFps:0.0}", 28f, top, new Color(255, 206, 110));
            y += 18f;

            const int cols = 22;
            const int rows = 10;
            const float step = 34f;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    var pos = new Vector2(28f + c * step, y + r * step);
                    float rotation = (r + c) * 0.05f + _frame * 0.01f;
                    batch.Draw(_tex, pos, Color.White, rotation, new Vector2(16f, 16f), 1f);
                }
            }

            DrawLine(batch, "旋转 / 缩放都在 CPU 侧算好顶点，GPU 只负责按索引绘制四边形",
                28f, y + rows * step + 14f, new Color(150, 165, 195));
        }
    }

}
