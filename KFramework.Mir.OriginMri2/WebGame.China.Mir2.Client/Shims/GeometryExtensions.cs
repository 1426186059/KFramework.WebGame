using System;

public static class GeometryExtensions
{
    // Point 是值类型，扩展方法必须 this ref 才能改到调用方的变量本身；
    // 否则 p 只是副本，Offset 变成 no-op（会导致移动、定位等全部失效）。
    public static void Offset(this ref Point p, int dx, int dy)
    {
        p.X += dx;
        p.Y += dy;
    }

    public static void Offset(this ref Point p, Point v)
    {
        p.X += v.X;
        p.Y += v.Y;
    }

    public static Size ToSize(this Point p) => new Size(p.X, p.Y);
    public static Point ToPoint(this Size s) => new Point(s.Width, s.Height);

    public static Rectangle FromLTRB(int left, int top, int right, int bottom)
        => new Rectangle(left, top, right - left, bottom - top);
}
