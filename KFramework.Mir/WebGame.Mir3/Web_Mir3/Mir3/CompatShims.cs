// 平台兼容垫片：浏览器 WASM 没有 System.Windows.Forms，原版渲染核心/客户端中用到的少量
// WinForms 类型在此提供最小占位，使工程能编译。待对应功能用 Canvas/浏览器 API 实现后移除。
namespace MirEngine
{
    /// <summary>
    /// 原版 <c>DisplayModeManager</c> 用 <c>Screen</c> 枚举显示器做全屏切换；浏览器由自身处理，
    /// 这里只保留编译所需的最小成员（DeviceName / Bounds / AllScreens / PrimaryScreen）。
    /// </summary>
    public sealed class Screen
    {
        public string DeviceName { get; set; } = string.Empty;
        public Rectangle Bounds { get; set; } = Rectangle.Empty;
        public static Screen PrimaryScreen => new Screen();
        public static Screen[] AllScreens => new[] { PrimaryScreen };
    }
}
