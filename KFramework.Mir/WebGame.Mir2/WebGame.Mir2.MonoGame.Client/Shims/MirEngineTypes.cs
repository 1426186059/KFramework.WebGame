using System;

namespace MirEngine
{
    // ===== 几何值类型（原 Web_Mir2.Engine/MirEngine/Shims 的 Point/Size/Rectangle 系列，纯数学、无 Browser 依赖）=====

    public struct Point
    {
        public static readonly Point Empty = new Point(0, 0);

        private int x;
        private int y;

        public bool IsEmpty => x == 0 && y == 0;

        public int X { get => x; set => x = value; }
        public int Y { get => y; set => y = value; }

        public Point(int x, int y) { this.x = x; this.y = y; }
        public Point(Size sz) { x = sz.Width; y = sz.Height; }
        public Point(int dw) { x = (short)LOWORD(dw); y = (short)HIWORD(dw); }

        public static implicit operator PointF(Point p) => new PointF(p.X, p.Y);
        public static explicit operator Size(Point p) => new Size(p.X, p.Y);

        public static Point operator +(Point pt, Size sz) => Add(pt, sz);
        public static Point operator -(Point pt, Size sz) => Subtract(pt, sz);

        public static bool operator ==(Point left, Point right) => left.X == right.X && left.Y == right.Y;
        public static bool operator !=(Point left, Point right) => !(left == right);

        public static Point Add(Point pt, Size sz) => new Point(pt.X + sz.Width, pt.Y + sz.Height);
        public static Point Subtract(Point pt, Size sz) => new Point(pt.X - sz.Width, pt.Y - sz.Height);

        public static Point Ceiling(PointF value) => new Point((int)Math.Ceiling(value.X), (int)Math.Ceiling(value.Y));
        public static Point Truncate(PointF value) => new Point((int)value.X, (int)value.Y);
        public static Point Round(PointF value) => new Point((int)Math.Round(value.X), (int)Math.Round(value.Y));

        // 与 KFramework.MonoGame 的类型互通：边界代码（输入桥接等）可直接以 MG.Vector2 / MG.Point 参与运算，
        // 无需把全工程的 MirEngine.Point 重写成另一套类型。
        // （KFramework.MonoGame.Point 是最小 int 点，无 Empty/IsEmpty/Offset/与 Size 的运算符；
        // 游戏几何层仍用功能更完整的 MirEngine.Point。）
        public static implicit operator KFramework.MonoGame.Vector2(Point p) => new KFramework.MonoGame.Vector2(p.X, p.Y);
        public static implicit operator Point(KFramework.MonoGame.Vector2 v) => new Point((int)v.X, (int)v.Y);
        public static implicit operator KFramework.MonoGame.Point(Point p) => new KFramework.MonoGame.Point(p.X, p.Y);
        public static implicit operator Point(KFramework.MonoGame.Point p) => new Point(p.X, p.Y);

        public override bool Equals(object obj)
        {
            if (!(obj is Point)) return false;
            Point point = (Point)obj;
            return point.X == X && point.Y == Y;
        }

        public override int GetHashCode() => x ^ y;

        public void Offset(int dx, int dy) { X += dx; Y += dy; }
        public void Offset(Point p) { Offset(p.X, p.Y); }

        public override string ToString() => $"({X}, {Y})";

        private static int HIWORD(int n) => (n >> 16) & 0xFFFF;
        private static int LOWORD(int n) => n & 0xFFFF;
    }

    public struct PointF
    {
        public static readonly PointF Empty;

        private float x;
        private float y;

        public bool IsEmpty => x == 0f && y == 0f;

        public float X { get => x; set => x = value; }
        public float Y { get => y; set => y = value; }

        public PointF(float x, float y) { this.x = x; this.y = y; }

        public static PointF operator +(PointF pt, Size sz) => Add(pt, sz);
        public static PointF operator -(PointF pt, Size sz) => Subtract(pt, sz);
        public static PointF operator +(PointF pt, SizeF sz) => Add(pt, sz);
        public static PointF operator -(PointF pt, SizeF sz) => Subtract(pt, sz);

        public static bool operator ==(PointF left, PointF right) => left.X == right.X && left.Y == right.Y;
        public static bool operator !=(PointF left, PointF right) => !(left == right);

        public static PointF Add(PointF pt, Size sz) => new PointF(pt.X + (float)sz.Width, pt.Y + (float)sz.Height);
        public static PointF Subtract(PointF pt, Size sz) => new PointF(pt.X - (float)sz.Width, pt.Y - (float)sz.Height);
        public static PointF Add(PointF pt, SizeF sz) => new PointF(pt.X + sz.Width, pt.Y + sz.Height);
        public static PointF Subtract(PointF pt, SizeF sz) => new PointF(pt.X - sz.Width, pt.Y - sz.Height);

        public override bool Equals(object obj)
        {
            if (!(obj is PointF)) return false;
            PointF pointF = (PointF)obj;
            return pointF.X == X && pointF.Y == Y && pointF.GetType().Equals(GetType());
        }

        public override int GetHashCode() => base.GetHashCode();
        public override string ToString() => $"({X}, {Y})";
    }

    public struct Size
    {
        public static readonly Size Empty;

        private int width;
        private int height;

        public bool IsEmpty => width == 0 && height == 0;

        public int Width { get => width; set => width = value; }
        public int Height { get => height; set => height = value; }

        public Size(Point pt) { width = pt.X; height = pt.Y; }
        public Size(int width, int height) { this.width = width; this.height = height; }

        public static implicit operator SizeF(Size p) => new SizeF(p.Width, p.Height);

        // 引擎通用文本库（KFramework.MonoGame.TextRenderer）的度量结果可直接当作本工程的 Size 使用，
        // 例如 Size s = TextRenderer.MeasureText(...)，避免调用处逐个字段转换。
        public static implicit operator Size(KFramework.MonoGame.Size s)
            => new Size(s.Width, s.Height);

        public static Size operator +(Size sz1, Size sz2) => Add(sz1, sz2);
        public static Size operator -(Size sz1, Size sz2) => Subtract(sz1, sz2);

        public static bool operator ==(Size sz1, Size sz2) => sz1.Width == sz2.Width && sz1.Height == sz2.Height;
        public static bool operator !=(Size sz1, Size sz2) => !(sz1 == sz2);

        public static explicit operator Point(Size size) => new Point(size.Width, size.Height);

        public static Size Add(Size sz1, Size sz2) => new Size(sz1.Width + sz2.Width, sz1.Height + sz2.Height);
        public static Size Ceiling(SizeF value) => new Size((int)Math.Ceiling(value.Width), (int)Math.Ceiling(value.Height));
        public static Size Subtract(Size sz1, Size sz2) => new Size(sz1.Width - sz2.Width, sz1.Height - sz2.Height);
        public static Size Truncate(SizeF value) => new Size((int)value.Width, (int)value.Height);
        public static Size Round(SizeF value) => new Size((int)Math.Round(value.Width), (int)Math.Round(value.Height));

        public override bool Equals(object obj)
        {
            if (!(obj is Size)) return false;
            Size size = (Size)obj;
            return size.width == width && size.height == height;
        }

        public override int GetHashCode() => width ^ height;
        public override string ToString() => $"{{Width={Width}, Height={Height}}}";
    }

    public struct SizeF
    {
        public static readonly SizeF Empty;

        private float width;
        private float height;

        public bool IsEmpty => width == 0f && height == 0f;

        public float Width { get => width; set => width = value; }
        public float Height { get => height; set => height = value; }

        public SizeF(SizeF size) { width = size.width; height = size.height; }
        public SizeF(PointF pt) { width = pt.X; height = pt.Y; }
        public SizeF(float width, float height) { this.width = width; this.height = height; }

        public static SizeF operator +(SizeF sz1, SizeF sz2) => Add(sz1, sz2);
        public static SizeF operator -(SizeF sz1, SizeF sz2) => Subtract(sz1, sz2);

        public static bool operator ==(SizeF sz1, SizeF sz2) => sz1.Width == sz2.Width && sz1.Height == sz2.Height;
        public static bool operator !=(SizeF sz1, SizeF sz2) => !(sz1 == sz2);

        public static explicit operator PointF(SizeF size) => new PointF(size.Width, size.Height);

        public static SizeF Add(SizeF sz1, SizeF sz2) => new SizeF(sz1.Width + sz2.Width, sz1.Height + sz2.Height);
        public static SizeF Subtract(SizeF sz1, SizeF sz2) => new SizeF(sz1.Width - sz2.Width, sz1.Height - sz2.Height);

        public override bool Equals(object obj)
        {
            if (!(obj is SizeF)) return false;
            SizeF sizeF = (SizeF)obj;
            return sizeF.Width == Width && sizeF.Height == Height && sizeF.GetType().Equals(GetType());
        }

        public override int GetHashCode() => base.GetHashCode();

        public PointF ToPointF() => (PointF)this;
        public Size ToSize() => Size.Truncate(this);
        public override string ToString() => $"{{Width={Width}, Height={Height}}}";
    }

    public struct Rectangle
    {
        public static readonly Rectangle Empty;

        private int x;
        private int y;
        private int width;
        private int height;

        public Point Location { get => new Point(X, Y); set { X = value.X; Y = value.Y; } }
        public Size Size { get => new Size(Width, Height); set { Width = value.Width; Height = value.Height; } }

        public int X { get => x; set => x = value; }
        public int Y { get => y; set => y = value; }
        public int Width { get => width; set => width = value; }
        public int Height { get => height; set => height = value; }

        public int Left => X;
        public int Top => Y;
        public int Right => X + Width;
        public int Bottom => Y + Height;

        public bool IsEmpty => height == 0 && width == 0 && x == 0 && y == 0;

        public Rectangle(int x, int y, int width, int height) { this.x = x; this.y = y; this.width = width; this.height = height; }
        public Rectangle(Point location, Size size) { x = location.X; y = location.Y; width = size.Width; height = size.Height; }

        public static Rectangle FromLTRB(int left, int top, int right, int bottom) => new Rectangle(left, top, right - left, bottom - top);

        public override bool Equals(object obj)
        {
            if (!(obj is Rectangle)) return false;
            Rectangle r = (Rectangle)obj;
            return r.X == X && r.Y == Y && r.Width == Width && r.Height == Height;
        }

        public static bool operator ==(Rectangle left, Rectangle right) => left.X == right.X && left.Y == right.Y && left.Width == right.Width && left.Height == right.Height;
        public static bool operator !=(Rectangle left, Rectangle right) => !(left == right);

        public static Rectangle Ceiling(RectangleF value) => new Rectangle((int)Math.Ceiling(value.X), (int)Math.Ceiling(value.Y), (int)Math.Ceiling(value.Width), (int)Math.Ceiling(value.Height));
        public static Rectangle Truncate(RectangleF value) => new Rectangle((int)value.X, (int)value.Y, (int)value.Width, (int)value.Height);
        public static Rectangle Round(RectangleF value) => new Rectangle((int)Math.Round(value.X), (int)Math.Round(value.Y), (int)Math.Round(value.Width), (int)Math.Round(value.Height));

        public bool Contains(int x, int y) => X <= x && x < X + Width && Y <= y && y < Y + Height;
        public bool Contains(Point pt) => Contains(pt.X, pt.Y);
        public bool Contains(Rectangle rect) => X <= rect.X && rect.X + rect.Width <= X + Width && Y <= rect.Y && rect.Y + rect.Height <= Y + Height;

        public override int GetHashCode() => X ^ (Y << 13) ^ (Width << 26) ^ (Height << 7);

        public void Inflate(int width, int height) { X -= width; Y -= height; Width += 2 * width; Height += 2 * height; }
        public void Inflate(Size size) => Inflate(size.Width, size.Height);
        public static Rectangle Inflate(Rectangle rect, int x, int y) { Rectangle r = rect; r.Inflate(x, y); return r; }

        public void Intersect(Rectangle rect) { Rectangle r = Intersect(rect, this); X = r.X; Y = r.Y; Width = r.Width; Height = r.Height; }
        public static Rectangle Intersect(Rectangle a, Rectangle b)
        {
            int num = Math.Max(a.X, b.X);
            int num2 = Math.Min(a.X + a.Width, b.X + b.Width);
            int num3 = Math.Max(a.Y, b.Y);
            int num4 = Math.Min(a.Y + a.Height, b.Y + b.Height);
            if (num2 >= num && num4 >= num3) return new Rectangle(num, num3, num2 - num, num4 - num3);
            return Empty;
        }

        public bool IntersectsWith(Rectangle rect) => rect.X < X + Width && X < rect.X + rect.Width && rect.Y < Y + Height && Y < rect.Y + rect.Height;

        public static Rectangle Union(Rectangle a, Rectangle b)
        {
            int num = Math.Min(a.X, b.X);
            int num2 = Math.Max(a.X + a.Width, b.X + b.Width);
            int num3 = Math.Min(a.Y, b.Y);
            int num4 = Math.Max(a.Y + a.Height, b.Y + b.Height);
            return new Rectangle(num, num3, num2 - num, num4 - num3);
        }

        public void Offset(Point pos) => Offset(pos.X, pos.Y);
        public void Offset(int x, int y) { X += x; Y += y; }

        public override string ToString() => $"({X}, {Y}, {Width}, {Height})";
    }

    public struct RectangleF
    {
        public static readonly RectangleF Empty;

        private float x;
        private float y;
        private float width;
        private float height;

        public PointF Location { get => new PointF(X, Y); set { X = value.X; Y = value.Y; } }
        public SizeF Size { get => new SizeF(Width, Height); set { Width = value.Width; Height = value.Height; } }

        public float X { get => x; set => x = value; }
        public float Y { get => y; set => y = value; }
        public float Width { get => width; set => width = value; }
        public float Height { get => height; set => height = value; }

        public float Left => X;
        public float Top => Y;
        public float Right => X + Width;
        public float Bottom => Y + Height;

        public bool IsEmpty => !(Width > 0f) && Height <= 0f;

        public RectangleF(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; }
        public RectangleF(PointF location, SizeF size) { x = location.X; y = location.Y; width = size.Width; height = size.Height; }

        public static RectangleF FromLTRB(float left, float top, float right, float bottom) => new RectangleF(left, top, right - left, bottom - top);

        public override bool Equals(object obj)
        {
            if (!(obj is RectangleF)) return false;
            RectangleF r = (RectangleF)obj;
            return r.X == X && r.Y == Y && r.Width == Width && r.Height == Height;
        }

        public static bool operator ==(RectangleF left, RectangleF right) => left.X == right.X && left.Y == right.Y && left.Width == right.Width && left.Height == right.Height;
        public static bool operator !=(RectangleF left, RectangleF right) => !(left == right);

        public bool Contains(float x, float y) => X <= x && x < X + Width && Y <= y && y < Y + Height;
        public bool Contains(PointF pt) => Contains(pt.X, pt.Y);
        public bool Contains(RectangleF rect) => X <= rect.X && rect.X + rect.Width <= X + Width && Y <= rect.Y && rect.Y + rect.Height <= Y + Height;

        public override int GetHashCode() => X.GetHashCode() ^ Y.GetHashCode() ^ Width.GetHashCode() ^ Height.GetHashCode();

        public void Inflate(float x, float y) { X -= x; Y -= y; Width += 2f * x; Height += 2f * y; }
        public void Inflate(SizeF size) => Inflate(size.Width, size.Height);
        public static RectangleF Inflate(RectangleF rect, float x, float y) { RectangleF r = rect; r.Inflate(x, y); return r; }

        public void Intersect(RectangleF rect) { RectangleF r = Intersect(rect, this); X = r.X; Y = r.Y; Width = r.Width; Height = r.Height; }
        public static RectangleF Intersect(RectangleF a, RectangleF b)
        {
            float num = Math.Max(a.X, b.X);
            float num2 = Math.Min(a.X + a.Width, b.X + b.Width);
            float num3 = Math.Max(a.Y, b.Y);
            float num4 = Math.Min(a.Y + a.Height, b.Y + b.Height);
            if (num2 >= num && num4 >= num3) return new RectangleF(num, num3, num2 - num, num4 - num3);
            return Empty;
        }

        public bool IntersectsWith(RectangleF rect) => rect.X < X + Width && X < rect.X + rect.Width && rect.Y < Y + Height && Y < rect.Y + rect.Height;

        public static RectangleF Union(RectangleF a, RectangleF b)
        {
            float num = Math.Min(a.X, b.X);
            float num2 = Math.Max(a.X + a.Width, b.X + b.Width);
            float num3 = Math.Min(a.Y, b.Y);
            float num4 = Math.Max(a.Y + a.Height, b.Y + b.Height);
            return new RectangleF(num, num3, num2 - num, num4 - num3);
        }

        public void Offset(PointF pos) => Offset(pos.X, pos.Y);
        public void Offset(float x, float y) { X += x; Y += y; }

        public static implicit operator RectangleF(Rectangle r) => new RectangleF(r.X, r.Y, r.Width, r.Height);

        public override string ToString() => $"({X}, {Y}, {Width}, {Height})";
    }

    // ===== 像素格式 / 数据矩形（原 Imaging.cs）=====
    public enum PixelFormat
    {
        Alpha, Canonical, DontCare, Extended, Format16bppArgb1555, Format16bppGrayScale,
        Format16bppRgb555, Format16bppRgb565, Format1bppIndexed, Format24bppRgb,
        Format32bppArgb, Format32bppPArgb, Format32bppRgb, Format48bppRgb, Format4bppIndexed,
        Format64bppArgb, Format64bppPArgb, Format8bppIndexed, Gdi, Indexed, Max, PAlpha, Undefined
    }

    public struct DataRectangle
    {
        public int Pitch;
        public IntPtr DataPointer;
    }
}
