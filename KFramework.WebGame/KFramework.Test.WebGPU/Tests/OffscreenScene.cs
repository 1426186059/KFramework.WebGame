using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.WebGPU.Tests
{

    /// <summary>
    /// 离屏渲染 + MSAA 解析对比（与 Test.WebGL20 的 MsaaScene 对齐，便于两个后端并排对比）。
    /// <para>
    /// 同一内容分别渲染进 <c>MultiSampleCount = 0</c> 与 <c>= 4</c> 的 RenderTarget2D，并排比较边缘锯齿。
    /// </para>
    /// <para>
    /// <b>与 WebGL 的建模差异</b>：WebGL 走「多重采样 FBO → blitFramebuffer 解析」；
    /// 而 WebGPU 与 Vulkan 同构 —— 解析目标（<c>resolveTarget</c>）必须在<b>通道创建时</b>就指定，
    /// 由实现在通道结束时自动解析，不存在事后的 blit 步骤。
    /// </para>
    /// </summary>
    public sealed class OffscreenScene : DemoScene
    {
        public override string Title => "3) 离屏渲染 + MSAA 解析";

        protected override string Description
            => "左：MultiSampleCount=0（带锯齿）    右：MultiSampleCount=4（通道结束时自动解析）";

        private const int TargetWidth = 400;
        private const int TargetHeight = 300;

        private RenderTarget2D? _plain;
        private RenderTarget2D? _msaa;
        private Texture2D? _tex;
        private int _frame;

        public override void Update() => _frame++;

        protected override void RenderOffscreen(SpriteBatch batch)
        {
            // 用综合参考图：圆环的曲线硬边缘最能体现 MSAA 差异，
            // 四角标记还能顺带验证离屏渲染没有发生翻转。
            _tex ??= MakeTestChart(64);

            _plain ??= new RenderTarget2D(Device, TargetWidth, TargetHeight, false,
                SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.DiscardContents);
            _msaa ??= new RenderTarget2D(Device, TargetWidth, TargetHeight, false,
                SurfaceFormat.Color, DepthFormat.None, 4, RenderTargetUsage.DiscardContents);

            DrawInto(_plain, batch);
            DrawInto(_msaa, batch);
        }

        private void DrawInto(RenderTarget2D target, SpriteBatch batch)
        {
            // 切换渲染目标必须在 SpriteBatch 的 Begin / End 之外；
            // WebGPU 后端会在切换时自动结束当前通道（解析也在此完成）。
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

            float y = top + TargetHeight + 16f;
            y = DrawLine(batch, $"FPS: {KTime.realFps:0.0}", 28f, y, new Color(255, 206, 110));
            DrawLine(batch, "解析由通道的 resolveTarget 在通道结束时完成（Vulkan 的 pResolveAttachments 语义）",
                28f, y, new Color(150, 165, 195));
        }
    }

}
