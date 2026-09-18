using System;
using System.IO;

namespace MirEngine
{
    // 原 Web_Mir2.Engine/MirEngine/Shims/Font.cs + Pen.cs + Text.cs + ColorTranslator.cs + Gdi.cs（纯托管 shim）

    public enum FontStyle
    {
        Regular = 0,
        Bold = 1,
        Italic = 2,
        Underline = 4,
        Strikeout = 8
    }

    public enum GraphicsUnit
    {
        World, Display, Pixel, Point, Inch, Document, Millimeter
    }

    public class Font : IDisposable
    {
        public string Name;
        public float Size;
        public FontStyle Style;
        public GraphicsUnit Unit;

        public int Height => (int)Math.Ceiling(Size * 4f / 3f);
        public bool Bold => (Style & FontStyle.Bold) != 0;
        public bool Italic => (Style & FontStyle.Italic) != 0;
        public bool Underline => (Style & FontStyle.Underline) != 0;
        public bool Strikeout => (Style & FontStyle.Strikeout) != 0;

        public Font(string name, float size, FontStyle style = FontStyle.Regular) { Name = name; Size = size; Style = style; Unit = GraphicsUnit.Point; }
        public Font(string name, float size, GraphicsUnit unit) { Name = name; Size = size; Unit = unit; }
        public Font(float size) { Name = "Arial"; Size = size; Unit = GraphicsUnit.Point; }
        public Font(float size, FontStyle style) { Name = "Arial"; Size = size; Style = style; Unit = GraphicsUnit.Point; }

        public static Font FromFont(Font f) => new Font(f.Name, f.Size, f.Style);

        public void Dispose() { }

        public string ToCss()
        {
            string s = string.Empty;
            if ((Style & FontStyle.Bold) != 0) s += "bold ";
            if ((Style & FontStyle.Italic) != 0) s += "italic ";
            if ((Style & FontStyle.Underline) != 0) s += "underline ";
            if ((Style & FontStyle.Strikeout) != 0) s += "line-through ";
            float px = Unit == GraphicsUnit.Point ? Size * 4f / 3f : Size;
            return $"{s}{px}px {Name}";
        }
    }

    public class Pen : IDisposable
    {
        public Color Color;
        public float Width;
        public Pen(Color color, float width = 1f) { Color = color; Width = width; }
        public void Dispose() { }
    }

    public enum TextRenderingHint
    {
        SystemDefault,
        SingleBitPerPixelGridFit,
        SingleBitPerPixel,
        AntiAliasGridFit,
        AntiAlias,
        ClearTypeGridFit
    }

    public static class ColorTranslator
    {
        public static Color FromHtml(string htmlColor)
        {
            if (string.IsNullOrEmpty(htmlColor)) return Color.Empty;
            string h = htmlColor.TrimStart('#');
            if (h.Length == 3) h = $"{h[0]}{h[0]}{h[1]}{h[1]}{h[2]}{h[2]}";
            if (h.Length == 6 && int.TryParse(h, System.Globalization.NumberStyles.HexNumber, null, out int v))
                return Color.FromArgb(255, (v >> 16) & 0xFF, (v >> 8) & 0xFF, v & 0xFF);
            return Color.FromName(htmlColor);
        }

        public static string ToHtml(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    }

    public enum ImageLockMode { ReadOnly, WriteOnly, ReadWrite }

    public abstract class Image : IDisposable
    {
        public int Width;
        public int Height;
        public PixelFormat PixelFormat;

        public static Image FromFile(string filename)
        {
            try { return new Bitmap(filename); } catch { return new Bitmap(1, 1); }
        }
        public static Image FromStream(Stream stream) => new Bitmap(stream);

        public void Dispose() { }
    }

    public class Bitmap : Image
    {
        public Bitmap(int width, int height) { Width = width; Height = height; PixelFormat = PixelFormat.Format32bppArgb; }
        public Bitmap(string filename)
        {
            try
            {
                byte[] data = System.IO.File.ReadAllBytes(filename);
                using var ms = new MemoryStream(data);
                var bmp = new Bitmap(ms);
                Width = bmp.Width; Height = bmp.Height;
            }
            catch { Width = 1; Height = 1; }
        }
        public Bitmap(Stream stream) { Width = 1; Height = 1; }

        public BitmapData LockBits(Rectangle rect, ImageLockMode flags, PixelFormat format) => new BitmapData();
        public void UnlockBits(BitmapData data) { }
    }

    public class BitmapData
    {
        public IntPtr Scan0;
        public int Stride;
        public int Width;
        public int Height;
        public PixelFormat PixelFormat;
        public int Reserved;
    }

    public enum StringAlignment { Near, Center, Far }

    public enum StringFormatFlags
    {
        DirectionRightToLeft = 0x1,
        DirectionVertical = 0x2,
        FitBlackBox = 0x4,
        DisplayFormatControl = 0x8,
        NoFontFallback = 0x10,
        NoWrap = 0x1000,
        LineLimit = 0x2000,
        NoClip = 0x4000
    }

    public class StringFormat : IDisposable
    {
        public StringAlignment Alignment;
        public StringAlignment LineAlignment;
        public StringFormat() { }
        public StringFormat(StringFormatFlags f) { }
        public void Dispose() { }
    }

    public class CharacterRange
    {
        public CharacterRange(int first, int length) { First = first; Length = length; }
        public int First;
        public int Length;
    }

    public class Brush : IDisposable { public void Dispose() { } }

    public class Region : IDisposable { public void Dispose() { } }

    public class SolidBrush : Brush
    {
        public SolidBrush(Color color) { Color = color; }
        public Color Color { get; set; }
    }

    public class Graphics : IDisposable
    {
        public Region Clip;
        public GraphicsUnit PageUnit;

        public static Graphics FromImage(Image image) => new Graphics();
        public static Graphics FromHwnd(IntPtr hwnd) => new Graphics();

        public void DrawImage(Image image, Point point) { }
        public void DrawImage(Image image, PointF point) { }
        public void DrawImage(Image image, Rectangle rect) { }
        public void DrawImage(Image image, int x, int y) { }
        public void DrawImage(Image image, float x, float y) { }

        public void FillRectangle(Brush brush, Rectangle rect) { }
        public void FillRectangle(Brush brush, int x, int y, int width, int height) { }
        public void FillRectangle(Brush brush, float x, float y, float width, float height) { }

        public void DrawString(string s, Font font, Brush brush, PointF point) { }
        public void DrawString(string s, Font font, Brush brush, float x, float y) { }
        public void DrawLine(Pen pen, Point p1, Point p2) { }

        public SizeF MeasureString(string text, Font font) => SizeF.Empty;
        public SizeF MeasureString(string text, Font font, SizeF layoutArea) => SizeF.Empty;

        public void Clear(Color color) { }
        public void Dispose() { }
    }
}
