using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.WebGPU.Tests
{

    /// <summary>
    /// 后端信息 + 精灵批绘制通路验证。
    /// <para>
    /// 若浏览器支持并成功取到 GPUDevice，<see cref="GraphicsDevice.BackendName"/> 为 "WebGPU"；
    /// 不支持时 <see cref="GraphicsDevice.CreateAsync"/> 已回落到 WebGL 2.0，这里会显示 "WebGL2"。
    /// </para>
    /// </summary>
    public sealed class BackendInfoScene : DemoScene
    {
        public override string Title => "1) 后端信息 / 精灵绘制通路";

        protected override string Description
            => "确认当前生效的渲染后端，并做一次精灵批绘制验证画面通路";

        private Texture2D? _tex;
        private int _frame;

        public override void Update() => _frame++;

        protected override void DrawBody(SpriteBatch batch, float top)
        {
            _tex ??= MakeChecker(32, new Color(130, 200, 255), new Color(28, 42, 78));

            float y = top;
            y = DrawLine(batch, $"后端 BackendName : {Device.BackendName}", 28f, y, new Color(255, 206, 110));
            y = DrawLine(batch, $"Renderer         : {Device.Renderer}", 28f, y, new Color(200, 220, 255));
            y = DrawLine(batch, $"视口             : {Device.Viewport.Width} x {Device.Viewport.Height}", 28f, y, new Color(200, 220, 255));
            y = DrawLine(batch, $"FPS              : {KTime.realFps:0.0}", 28f, y, new Color(200, 220, 255));
            y += 18f;

            for (int r = 0; r < 6; r++)
            {
                for (int c = 0; c < 14; c++)
                {
                    float rotation = (r + c) * 0.06f + _frame * 0.01f;
                    batch.Draw(_tex, new Vector2(28f + c * 40f, y + r * 40f),
                        Color.White, rotation, new Vector2(16f, 16f), 1f);
                }
            }
        }
    }

}
