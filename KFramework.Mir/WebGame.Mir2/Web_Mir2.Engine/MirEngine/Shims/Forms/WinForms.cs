// System.Windows.Forms 兼容壳：公共 shim 类型（原散落在各宿主工程的 systemWindowsForms.cs）。
// 统一放在通用库，宿主工程（Web_Mir2 / Web_Mir3 等）不应再自行定义这些类型。
//
// 说明：Control.ModifierKeys 依赖宿主的 Client.CMain 修饰键状态，属于宿主业务层，
// 因此不在此处提供，由各宿主工程自行保留。
namespace MirEngine
{
    using System;

    // 注意：位值必须与真实 TextFormatFlags 保持一致，
    // 因为 Client 代码（DXLabel/DXButton 等）把 (int)DrawFormat 直接传给 Canvas 端
    // drawLabel / MeasureText，后者按 .NET 标准位（HorizontalCenter=1, VerticalCenter=4,
    // Right=2, Bottom=8, WordBreak=16）解读。
    [Flags]
    public enum TextFormatFlags
    {
        Default = 0,
        Left = 0x0,
        Top = 0x0,
        HorizontalCenter = 0x1,
        Right = 0x2,
        VerticalCenter = 0x4,
        Bottom = 0x8,
        WordBreak = 0x10,
        SingleLine = 0x20,
        ExpandTabs = 0x400,
        NoClipping = 0x100,
        ExternalLeading = 0x200,
        NoPrefix = 0x800,
        Internal = 0x1000,
        TextBoxControl = 0x2000,
        PathEllipsis = 0x4000,
        EndEllipsis = 0x8000,
        ModifyString = 0x10000,
        RightToLeft = 0x20000,
        NoFullWidthCharacterBreak = 0x80000,
        HidePrefix = 0x100000,
        NoPadding = 0x1000000,
        LeftAndRightPadding = 0x2000000,
        WordEllipsis = 0x40000,
    }

    public struct Message
    {
        public IntPtr HWnd { get; set; }
        public int Msg { get; set; }
        public IntPtr WParam { get; set; }
        public IntPtr LParam { get; set; }
        public IntPtr Result { get; set; }
    }

    public class Cursor
    {
        public static Cursor Current { get; set; } = new Cursor();
    }

    public static class Cursors
    {
        public static Cursor Arrow { get; } = new Cursor();
        public static Cursor Default { get; } = new Cursor();
        public static Cursor IBeam { get; } = new Cursor();
        public static Cursor WaitCursor { get; } = new Cursor();
        public static Cursor Cross { get; } = new Cursor();
        public static Cursor Hand { get; } = new Cursor();
        public static Cursor SizeAll { get; } = new Cursor();
        public static Cursor SizeWE { get; } = new Cursor();
        public static Cursor SizeNWSE { get; } = new Cursor();
        public static Cursor SizeNESW { get; } = new Cursor();
        public static Cursor SizeNS { get; } = new Cursor();
        public static Cursor No { get; } = new Cursor();
        public static Cursor Help { get; } = new Cursor();
    }

    public class Timer
    {
        public Timer() { }

        public event EventHandler Tick;
        public int Interval { get; set; }
        public bool Enabled { get; set; }

        public void Start() { }
        public void Stop() { }
        public void Dispose() { }
    }

    public static class Application
    {
        public static string ExecutablePath => string.Empty;
        public static string StartupPath => ".";
        public static string ProductVersion => "1.0.0.0";
        public static void DoEvents() { }
        public static void Exit() { }
        public static void Run() { }
    }

    public static class TextRenderer
    {
        public static Size MeasureText(string text, MirEngine.Font font) => Size.Empty;
        public static Size MeasureText(string text, MirEngine.Font font, Size proposedSize) => Size.Empty;
        public static Size MeasureText(string text, MirEngine.Font font, TextFormatFlags flags) => Size.Empty;
        public static Size MeasureText(string text, MirEngine.Font font, Size proposedSize, TextFormatFlags flags) => Size.Empty;

        public static void DrawText(Graphics g, string text, MirEngine.Font font, Point pt, Color foreColor) { }
        public static void DrawText(Graphics g, string text, MirEngine.Font font, Point pt, Color foreColor, TextFormatFlags flags) { }
        public static void DrawText(Graphics g, string text, MirEngine.Font font, Rectangle bounds, Color foreColor) { }
        public static void DrawText(Graphics g, string text, MirEngine.Font font, Rectangle bounds, Color foreColor, TextFormatFlags flags) { }

        public static Size MeasureText(object dc, string text, Font font) => MirEngine.BrowserCanvas.MeasureText(text, MirEngine.BrowserCanvas.FontToCss(font), int.MaxValue);
        public static Size MeasureText(object dc, string text, Font font, Size proposedSize) => MirEngine.BrowserCanvas.MeasureText(text, MirEngine.BrowserCanvas.FontToCss(font), proposedSize.Width);
        public static Size MeasureText(object dc, string text, Font font, TextFormatFlags flags) => MirEngine.BrowserCanvas.MeasureText(text, MirEngine.BrowserCanvas.FontToCss(font), int.MaxValue);
        public static Size MeasureText(object dc, string text, Font font, Size proposedSize, TextFormatFlags flags) => MirEngine.BrowserCanvas.MeasureText(text, MirEngine.BrowserCanvas.FontToCss(font), proposedSize.Width);
    }
}
