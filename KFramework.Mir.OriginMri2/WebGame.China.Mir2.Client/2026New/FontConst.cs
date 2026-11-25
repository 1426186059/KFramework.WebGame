using MirEngine;
using FontStyle = KFramework.MonoGame.FontStyle;

namespace Client
{
    /// <summary>
    /// 全局共享的 Font 描述符。Font 本身只是 (Name, Size, Style) 轻量描述符，
    /// 真正的 SpriteFont 图集由 MirEngine.FontFactory 按该三元组缓存；
    /// 这里把上层用到的固定字号收敛为静态实例，避免到处 new Font 产生临时对象。
    /// 动态字号（DPI 缩放/外部传入）不走这里，直接构造即可（FontFactory 仍会复用图集）。
    /// </summary>
    public static class FontConst
    {
        public static readonly Font F7 = new Font(Settings.FontName, 7F);
        public static readonly Font F8 = new Font(Settings.FontName, 8F);
        public static readonly Font F9 = new Font(Settings.FontName, 9F);
        public static readonly Font F10 = new Font(Settings.FontName, 10F);
        public static readonly Font F11 = new Font(Settings.FontName, 11F);
        public static readonly Font F12 = new Font(Settings.FontName, 12F);
        public static readonly Font F13 = new Font(Settings.FontName, 13F);
        public static readonly Font F14 = new Font(Settings.FontName, 14F);
        public static readonly Font F15 = new Font(Settings.FontName, 15F);

        public static readonly Font F7Italic = new Font(Settings.FontName, 7F, FontStyle.Italic);
        public static readonly Font F8Bold = new Font(Settings.FontName, 8F, FontStyle.Bold);
        public static readonly Font F9Bold = new Font(Settings.FontName, 9F, FontStyle.Bold);
        public static readonly Font F10Bold = new Font(Settings.FontName, 10F, FontStyle.Bold);
        public static readonly Font F12Bold = new Font(Settings.FontName, 12F, FontStyle.Bold);
        public static readonly Font F14Bold = new Font(Settings.FontName, 14F, FontStyle.Bold);
    }
}
