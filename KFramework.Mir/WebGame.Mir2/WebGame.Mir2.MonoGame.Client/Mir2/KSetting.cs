using System;

namespace Client
{
    /// <summary>
    /// 渲染分层分辨率配置。
    /// 设计原则（见对话共识）：地图层按真实 ViewPort 渲染（世界坐标、与分辨率无关），
    /// 只有 UI 层需要一个固定的“参考分辨率”来做等比布局。此文件集中配置该参考分辨率，
    /// 不再把 1024x768 之类写死在散落的 Settings.ScreenWidth/Height 里。
    /// </summary>
    public static class KSetting
    {
        // UI 设计参考分辨率：UI 控件坐标基于此空间布局，再按实际视口高度等比缩放上屏。
        // 当前与历史 UI 布局坐标一致（1024x768），切换后 UI 显示完全不变。
        public const int UIReferenceWidth = 1024;
        public const int UIReferenceHeight = 768;
    }
}
