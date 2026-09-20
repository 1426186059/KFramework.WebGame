using Client;

namespace WebGame.Mir2.MonoGame.Client
{
    // UI 层：承载对话框 / HUD 等界面控件。
    // UI 控件按【逻辑参考分辨率】Settings.ScreenWidth/Height(=1024x768) 布局，
    // 本层用“非均匀拉伸(铺满)”把它映射到真实窗口：逻辑(0,0) 对齐屏(0,0)，
    // sx = 视口宽 / ScreenWidth、sy = 视口高 / ScreenHeight。这样窗口尺寸变化时，
    // 层变换一次性把整套 UI 拉伸铺满整个画布（控件布局本身无需重排），与
    // MirScene.DrawControl 的"拉伸铺满画布"意图一致，也复刻原版 Mir2 把 1024x768
    // 后台缓冲拉伸到窗口的行为。逆变换见 KCamera.ScreenToWorldPos（用同样的 sx/sy），
    // 保证鼠标命中坐标不错位。
    // 注意：Settings.ScreenWidth/Height 是固定逻辑分辨率(1024x768)，并非真实画布尺寸；
    // 之前错误地用 1:1 透传，导致窗口≠1024x768 时 UI 不随窗口缩放/铺满（"UI 层没适配屏幕"）。
    public sealed class UILayerControl : LayerControl
    {
        protected override KFramework.MonoGame.Matrix4x4 GetLayerTransform(int viewportWidth, int viewportHeight)
        {
            // 非均匀拉伸铺满：逻辑 1024x768 → 真实窗口，与 KCamera 逆变换严格一致。
            float sx = Settings.ScreenWidth > 0 ? (float)viewportWidth / Settings.ScreenWidth : 1f;
            float sy = Settings.ScreenHeight > 0 ? (float)viewportHeight / Settings.ScreenHeight : 1f;
            return KFramework.MonoGame.Matrix4x4.CreateScaleTranslation(sx, sy, 0, 0f);
        }
    }
}
