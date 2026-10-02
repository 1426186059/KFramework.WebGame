using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.WebGL20.Tests
{

    /// <summary>
    /// 离屏 MSAA 对比：同一内容分别渲染进 MultiSampleCount=0 与 =4 的
    /// <see cref="RenderTarget2D"/>，并排对比边缘锯齿。
    /// <para>
    /// 这是 <b>渲染目标级别</b>的 MSAA，与画布 <c>getContext(antialias)</c> 无关：
    /// WebGL2 后端用「多重采样 renderbuffer + MSAA FBO → blitFramebuffer 解析到纹理」实现。
    /// </para>
    /// </summary>
    public sealed class MsaaScene : DemoScene
    {
        public override string Title => "3) 离屏 MSAA 对比（多重采样 FBO + blit 解析）";

        protected override string Description
            => "左：MultiSampleCount=0（带锯齿）    右：MultiSampleCount=4（解析后平滑）";

        private const int TargetWidth = 400;
        private const int TargetHeight = 300;

        private RenderTarget2D? _plain;
        private RenderTarget2D? _msaa;
        private Texture2D? _tex;
        private int _frame;

        public override void Update() => _frame++;

        protected override void RenderOffscreen(SpriteBatch batch)
        {
            _tex ??= MakeChecker(24, new Color(120, 235, 170), new Color(24, 60, 48));

            _plain ??= new RenderTarget2D(Device, TargetWidth, TargetHeight, false,
                SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.DiscardContents);
            _msaa ??= new RenderTarget2D(Device, TargetWidth, TargetHeight, false,
                SurfaceFormat.Color, DepthFormat.None, 4, RenderTargetUsage.DiscardContents);

            DrawInto(_plain, batch);
            DrawInto(_msaa, batch);
        }

        private void DrawInto(RenderTarget2D target, SpriteBatch batch)
        {
            Device.SetRenderTarget(target);
            Device.Clear(new Color(12, 16, 26));

            batch.Begin();
            for (int r = 0; r < 7; r++)
            {
                for (int c = 0; c < 10; c++)
                {
                    float rotation = (r + c) * 0.12f + _frame * 0.008f;
                    batch.Draw(_tex, new Vector2(24f + c * 38f, 24f + r * 38f),
                        Color.White, rotation, new Vector2(12f, 12f), 1f);
                }
            }
            batch.End();

            Device.SetRenderTarget(null);
        }

        protected override void DrawBody(SpriteBatch batch, float top)
        {
            if (_plain is null || _msaa is null) return;

            batch.Draw(_plain, new Rectangle(28, (int)top, TargetWidth, TargetHeight), Color.White);
            batch.Draw(_msaa, new Rectangle(28 + TargetWidth + 24, (int)top, TargetWidth, TargetHeight), Color.White);

            DrawLine(batch, $"FPS: {KTime.realFps:0.0}",
                28f, top + TargetHeight + 16f, new Color(255, 206, 110));
        }
    }

}
