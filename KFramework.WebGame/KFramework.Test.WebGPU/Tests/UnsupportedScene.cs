using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.WebGPU.Tests
{

    /// <summary>
    /// WebGPU 后端「暂未实现」的能力清单页：现场调用并把结果（通常是 NotSupportedException）显示出来，
    /// 避免使用者到运行时才发现。
    /// <para>
    /// 这些属于步骤④：渲染目标需要「独立纹理 + 独立渲染通道 + resolve」，
    /// 与 WebGL 的 FBO + blitFramebuffer 模型不同，需要重新设计后端接口。
    /// </para>
    /// </summary>
    public sealed class UnsupportedScene : DemoScene
    {
        public override string Title => "3) WebGPU 暂未支持的能力";

        protected override string Description
            => "渲染目标 / 离屏渲染、读像素、纹理局部更新：当前会抛 NotSupportedException（步骤④）";

        private string _renderTargetResult = "（尚未测试）";
        private bool _tested;

        public override void Update()
        {
            if (_tested) return;
            _tested = true;

            try
            {
                using var rt = new RenderTarget2D(Device, 64, 64);
                _renderTargetResult = "RenderTarget2D：创建成功 —— 说明后端已支持离屏渲染。";
            }
            catch (NotSupportedException ex)
            {
                _renderTargetResult = "RenderTarget2D → NotSupportedException：" + ex.Message;
            }
            catch (Exception ex)
            {
                _renderTargetResult = $"RenderTarget2D → {ex.GetType().Name}：{ex.Message}";
            }
        }

        protected override void DrawBody(SpriteBatch batch, float top)
        {
            float y = DrawLine(batch, _renderTargetResult, 28f, top, new Color(255, 150, 150));
            y += 16f;

            y = DrawLine(batch, "未实现清单：", 28f, y, new Color(255, 206, 110));
            y = DrawLine(batch, "  · 渲染目标 / 离屏渲染（RenderTarget2D / SetRenderTarget）", 28f, y, new Color(150, 165, 195));
            y = DrawLine(batch, "  · 多重采样 MSAA（需 resolve）", 28f, y, new Color(150, 165, 195));
            y = DrawLine(batch, "  · 读像素（ReadPixel）", 28f, y, new Color(150, 165, 195));
            y = DrawLine(batch, "  · 纹理局部更新（SetData 子区域）", 28f, y, new Color(150, 165, 195));

            y += 16f;
            DrawLine(batch, "原因：WebGPU 没有 FBO / blitFramebuffer，离屏渲染要走「独立纹理 + 独立渲染通道 + resolve」，",
                28f, y, new Color(120, 140, 175));
        }
    }

}
