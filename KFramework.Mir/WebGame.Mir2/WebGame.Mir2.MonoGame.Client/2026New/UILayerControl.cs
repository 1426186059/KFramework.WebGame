using Client;

namespace WebGame.Mir2.MonoGame.Client
{
    // UI 层：承载对话框 / HUD 等界面控件。
    // 按高度统一缩放并 pinned 到屏幕（逻辑参考分辨率 = KSetting.UIReferenceHeight），
    // 即“UI 映射到相机空间”后的屏幕固定坐标，使 UI 等比铺满窗口。
    // UI 参考分辨率集中配置在 KSetting，不再依赖 Settings.ScreenWidth/Height（那只是地图逻辑尺寸）。
    public sealed class UILayerControl : LayerControl
    {
        protected override KFramework.MonoGame.Matrix4x4 GetLayerTransform(int viewportWidth, int viewportHeight)
        {
            float s = (float)viewportHeight / KSetting.UIReferenceHeight;
            return KFramework.MonoGame.Matrix4x4.CreateScaleTranslation(s, s, 0, 0f);
        }
    }
}
