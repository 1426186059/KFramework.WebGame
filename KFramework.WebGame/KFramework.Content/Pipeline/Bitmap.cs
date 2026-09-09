namespace KFramework.Content.Pipeline;

/// <summary>RGBA 颜色（内容管线内部使用，不依赖引擎，便于工具端独立运行）。</summary>
public readonly struct Rgba(byte r, byte g, byte b, byte a = 255) : IEquatable<Rgba>
{
    public readonly byte R = r;
    public readonly byte G = g;
    public readonly byte B = b;
    public readonly byte A = a;

    public static Rgba Transparent => new(0, 0, 0, 0);

    /// <summary>从 #RGB / #RGBA / #RRGGBB / #RRGGBBAA 解析。</summary>
    public static Rgba Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Transparent;

        ReadOnlySpan<char> text = value.AsSpan().Trim();
        if (text[0] == '#') text = text[1..];

        return text.Length switch
        {
            3 => new Rgba(Expand(text[0]), Expand(text[1]), Expand(text[2]), 255),
            4 => new Rgba(Expand(text[0]), Expand(text[1]), Expand(text[2]), Expand(text[3])),
            6 => new Rgba(Pair(text[0], text[1]), Pair(text[2], text[3]), Pair(text[4], text[5]), 255),
            8 => new Rgba(Pair(text[0], text[1]), Pair(text[2], text[3]), Pair(text[4], text[5]), Pair(text[6], text[7])),
            _ => throw new FormatException($"无法解析颜色 “{value}”。"),
        };

        static byte Expand(char c)
        {
            int v = Convert.ToInt32(c.ToString(), 16);
            return (byte)(v * 17);
        }

        static byte Pair(char hi, char lo)
            => (byte)((Convert.ToInt32(hi.ToString(), 16) << 4) | Convert.ToInt32(lo.ToString(), 16));
    }

    public bool Equals(Rgba other) => R == other.R && G == other.G && B == other.B && A == other.A;
    public override bool Equals(object? obj) => obj is Rgba c && Equals(c);
    public override int GetHashCode() => HashCode.Combine(R, G, B, A);
    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}{A:X2}";
}

/// <summary>内容管线内的浮点二维点。</summary>
public readonly struct Vec2f(float x, float y)
{
    public readonly float X = x;
    public readonly float Y = y;
}

/// <summary>
/// RGBA8 位图，行优先、左上原点。提供带 3x3 超采样的图元光栅化，
/// 用于把矢量描述（.sprite.json）离线烘焙成像素资源。
/// </summary>
public sealed class Bitmap
{
    public readonly int Width;
    public readonly int Height;
    public readonly byte[] Pixels;

    public Bitmap(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        Width = width;
        Height = height;
        Pixels = new byte[width * height * 4];
    }

    private Bitmap(int width, int height, byte[] pixels)
    {
        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public static Bitmap FromRgba(byte[] pixels, int width, int height)
    {
        if (pixels.Length != width * height * 4)
            throw new ArgumentException("像素数据长度与尺寸不匹配。", nameof(pixels));
        return new Bitmap(width, height, pixels);
    }

    public void Clear() => Array.Clear(Pixels);

    public Rgba GetPixel(int x, int y)
    {
        int i = (y * Width + x) * 4;
        return new Rgba(Pixels[i], Pixels[i + 1], Pixels[i + 2], Pixels[i + 3]);
    }

    /// <summary>以 "source over" 方式混合一个像素。</summary>
    public void BlendPixel(int x, int y, Rgba color, float coverage = 1f)
    {
        if (coverage <= 0f || x < 0 || y < 0 || x >= Width || y >= Height) return;

        float sourceAlpha = color.A / 255f * coverage;
        if (sourceAlpha <= 0f) return;

        int i = (y * Width + x) * 4;
        float dstAlpha = Pixels[i + 3] / 255f;
        float outAlpha = sourceAlpha + dstAlpha * (1f - sourceAlpha);
        if (outAlpha <= 0f) return;

        float dstWeight = dstAlpha * (1f - sourceAlpha);
        Pixels[i + 0] = (byte)((color.R * sourceAlpha + Pixels[i + 0] * dstWeight) / outAlpha);
        Pixels[i + 1] = (byte)((color.G * sourceAlpha + Pixels[i + 1] * dstWeight) / outAlpha);
        Pixels[i + 2] = (byte)((color.B * sourceAlpha + Pixels[i + 2] * dstWeight) / outAlpha);
        Pixels[i + 3] = (byte)(outAlpha * 255f);
    }

    /// <summary>不透明覆盖（比 BlendPixel 快，用于实心块）。</summary>
    public void SetPixel(int x, int y, Rgba color)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return;
        int i = (y * Width + x) * 4;
        Pixels[i + 0] = color.R;
        Pixels[i + 1] = color.G;
        Pixels[i + 2] = color.B;
        Pixels[i + 3] = color.A;
    }

    #region 图元

    public void FillRect(int x, int y, int width, int height, Rgba color)
    {
        int x1 = Math.Max(0, x), y1 = Math.Max(0, y);
        int x2 = Math.Min(Width, x + width), y2 = Math.Min(Height, y + height);
        for (int py = y1; py < y2; py++)
            for (int px = x1; px < x2; px++)
                BlendPixel(px, py, color);
    }

    public void FillCircle(float cx, float cy, float radius, Rgba color)
        => FillShape(new CircleShape(cx, cy, radius), color);

    public void FillEllipse(float cx, float cy, float radiusX, float radiusY, Rgba color)
        => FillShape(new EllipseShape(cx, cy, radiusX, radiusY), color);

    public void FillTriangle(Vec2f a, Vec2f b, Vec2f c, Rgba color)
        => FillShape(new TriangleShape(a, b, c), color);

    public void FillPolygon(ReadOnlySpan<Vec2f> points, Rgba color)
    {
        if (points.Length < 3) return;
        FillShape(new PolygonShape(points.ToArray()), color);
    }

    /// <summary>带宽度的直线（端点为圆头，用于绘制进度条、光束）。</summary>
    public void DrawLine(Vec2f from, Vec2f to, float width, Rgba color)
    {
        float dx = to.X - from.X, dy = to.Y - from.Y;
        float length = MathF.Sqrt(dx * dx + dy * dy);
        if (length < 1e-4f) { FillCircle(from.X, from.Y, width * 0.5f, color); return; }

        float ux = dx / length, uy = dy / length;
        float hx = -uy * width * 0.5f, hy = ux * width * 0.5f;

        FillShape(new PolygonShape(new[]
        {
            new Vec2f(from.X + hx, from.Y + hy),
            new Vec2f(to.X + hx, to.Y + hy),
            new Vec2f(to.X - hx, to.Y - hy),
            new Vec2f(from.X - hx, from.Y - hy),
        }), color);

        FillCircle(from.X, from.Y, width * 0.5f, color);
        FillCircle(to.X, to.Y, width * 0.5f, color);
    }

    private const int Supersample = 3;

    /// <summary>对形状做 3x3 超采样，得到抗锯齿边缘。</summary>
    private void FillShape<TShape>(in TShape shape, Rgba color) where TShape : struct, IShape
    {
        float step = 1f / Supersample;
        float inv = 1f / (Supersample * Supersample);

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int hits = 0;
                for (int sy = 0; sy < Supersample; sy++)
                {
                    float py = y + (sy + 0.5f) * step;
                    for (int sx = 0; sx < Supersample; sx++)
                    {
                        float px = x + (sx + 0.5f) * step;
                        if (shape.Contains(px, py)) hits++;
                    }
                }
                if (hits > 0) BlendPixel(x, y, color, hits * inv);
            }
        }
    }

    private interface IShape
    {
        bool Contains(float x, float y);
    }

    private readonly struct CircleShape(float cx, float cy, float r) : IShape
    {
        private readonly float _r2 = r * r;

        public bool Contains(float x, float y)
        {
            float dx = x - cx, dy = y - cy;
            return dx * dx + dy * dy <= _r2;
        }
    }

    private readonly struct EllipseShape(float cx, float cy, float rx, float ry) : IShape
    {
        public bool Contains(float x, float y)
        {
            float dx = (x - cx) / rx, dy = (y - cy) / ry;
            return dx * dx + dy * dy <= 1f;
        }
    }

    private readonly struct TriangleShape(Vec2f a, Vec2f b, Vec2f c) : IShape
    {
        public bool Contains(float x, float y)
        {
            float d1 = Sign(x, y, a, b);
            float d2 = Sign(x, y, b, c);
            float d3 = Sign(x, y, c, a);
            bool hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
            bool hasPos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(hasNeg && hasPos);
        }

        private static float Sign(float px, float py, Vec2f a, Vec2f b)
            => (px - b.X) * (a.Y - b.Y) - (a.X - b.X) * (py - b.Y);
    }

    private readonly struct PolygonShape(Vec2f[] points) : IShape
    {
        private readonly Vec2f[] _points = points;

        /// <summary>奇偶规则（even-odd），可处理凹多边形。</summary>
        public bool Contains(float x, float y)
        {
            bool inside = false;
            Vec2f[] pts = _points;
            for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
            {
                if ((pts[i].Y > y) != (pts[j].Y > y))
                {
                    float t = (y - pts[i].Y) / (pts[j].Y - pts[i].Y);
                    if (x < pts[i].X + t * (pts[j].X - pts[i].X)) inside = !inside;
                }
            }
            return inside;
        }
    }

    #endregion

    /// <summary>把另一张位图绘制到指定位置（不做缩放）。</summary>
    public void Blit(Bitmap source, int x, int y)
    {
        for (int sy = 0; sy < source.Height; sy++)
        {
            int dy = y + sy;
            if (dy < 0 || dy >= Height) continue;
            for (int sx = 0; sx < source.Width; sx++)
            {
                int dx = x + sx;
                if (dx < 0 || dx >= Width) continue;
                BlendPixel(dx, dy, source.GetPixel(sx, sy));
            }
        }
    }

    /// <summary>裁掉四周完全透明的行列，减小图集占用。</summary>
    public Bitmap Trim()
    {
        int left = Width, top = Height, right = 0, bottom = 0;

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (GetPixel(x, y).A == 0) continue;
                if (x < left) left = x;
                if (x > right) right = x;
                if (y < top) top = y;
                if (y > bottom) bottom = y;
            }
        }

        if (right < left) return new Bitmap(1, 1);

        int w = right - left + 1;
        int h = bottom - top + 1;
        var result = new Bitmap(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                result.SetPixel(x, y, GetPixel(left + x, top + y));
        return result;
    }

    public bool IsEmpty()
    {
        foreach (byte a in Pixels)
            if (a != 0) return false;
        return true;
    }
}
