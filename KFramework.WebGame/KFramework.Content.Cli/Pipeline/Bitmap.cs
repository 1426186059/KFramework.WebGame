namespace KFramework.Content.Build;

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

/// <summary>
/// RGBA8 位图，行优先、左上原点。
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
