
public static class KSetting
{
    // UI 设计参考分辨率：UI 控件坐标基于此空间布局，再按实际视口高度等比缩放上屏。
    // 默认与历史 UI 布局坐标一致（1024x768）。窗口尺寸变化时由 CMain.SetResolution
    // 同步为实际画布尺寸（全屏），使 UI 以原生分辨率渲染、不再按固定 768 缩放。
    public const int UIReferenceWidth = 1024;
    public const int UIReferenceHeight = 768;
}
