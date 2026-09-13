// System.Windows.Forms.SystemInformation 兼容壳（公共 shim，统一由通用库提供）。
namespace MirEngine
{
    public class SystemInformation
    {
        public static int MouseButtons => 3;
        public static int VerticalScrollBarWidth => 17;
        public static int HorizontalScrollBarHeight => 17;
        public static int Border3DSize => 2;
        public static int CaptionHeight => 23;
        public static int FrameBorderSize => 4;
        public static int DoubleClickTime => 500;
        public static int MouseWheelScrollDelta => 120;
        public static Size PrimaryMonitorSize => new Size(1024, 768);
        public static Rectangle VirtualScreen => new Rectangle(0, 0, 1024, 768);
    }
}
