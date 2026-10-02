using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.WebGL20.Tests
{

    /// <summary>
    /// 离屏渲染：把内容画进 <see cref="RenderTarget2D"/>，再把这张离屏纹理贴回屏幕。
    /// WebGL2 后端为「一组渲染目标绑定组合」缓存一个 FBO（照 MonoGame 的 glFramebuffers）。
    /// </summary>
    public sealed class RenderTargetScene : DemoScene
    {
        public override string Title => "2) 离屏渲染 RenderTarget2D";

        protected override string Description
            => "先渲染进离屏纹理，再把它贴回屏幕；FBO 由 GraphicsDevice 按绑定组合缓存复用";

        private RenderTarget2D? _rt;
        private Texture2D? _tex;
        private int _frame;

        public override void Update() => _frame++;

        protected override void RenderOffscreen(SpriteBatch batch)
        {
            _tex ??= MakeChecker(32, new Color(250, 190, 90), new Color(58, 38, 20));
            _rt ??= new RenderTarget2D(Device, 480, 320);

            // 切换渲染目标必须在 SpriteBatch 的 Begin / End 之外。
            Device.SetRenderTarget(_rt);
            Device.Clear(new Color(16, 20, 32));

            batch.Begin();
            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 12; c++)
                {
                    float rotation = (r + c) * 0.08f + _frame * 0.012f;
                    batch.Draw(_tex, new Vector2(20f + c * 38f, 20f + r * 38f),
                        Color.White, rotation, new Vector2(16f, 16f), 1f);
                }
            }
            batch.End();

            Device.SetRenderTarget(null);
        }

        protected override void DrawBody(SpriteBatch batch, float top)
        {
            if (_rt is null) return;

            batch.Draw(_rt, new Rectangle(28, (int)top, 480, 320), Color.White);
            DrawLine(batch, $"离屏尺寸 {_rt.Width}x{_rt.Height} | FPS {KTime.realFps:0.0}",
                28f, top + 336f, new Color(255, 206, 110));
        }
    }

}
