using MirEngine;

namespace MirEngine;

/// <summary>
/// 临时探针：验证 Primitives（Color/Point/Size/Rectangle 及 F 版本）
/// 在 browser-wasm 上是否可用。结论出来后删除。
/// </summary>
internal static class PrimitivesProbe
{
    internal static Point P => new(1, 2);
    internal static Size S => new(3, 4);
    internal static Rectangle R => new(0, 0, 10, 20);
    internal static Color C => Color.FromArgb(255, 1, 2, 3);
    internal static PointF Pf => new(1.5f, 2.5f);
    internal static SizeF Sf => new(3.5f, 4.5f);
    internal static RectangleF Rf => new(1f, 2f, 3f, 4f);

    internal static string Report()
    {
        Point p = new(1, 2);
        p.Offset(1, 1);
        Rectangle r = new(0, 0, 10, 20);
        bool hit = r.Contains(p);
        Color c = Color.FromArgb(255, 10, 20, 30);
        return $"P={p.X},{p.Y} R={r.Width}x{r.Height} hit={hit} ARGB={c.ToArgb():X8}";
    }
}
