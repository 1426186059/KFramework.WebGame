using System.Runtime.CompilerServices;

namespace KFramework;

/// <summary>RGBA 颜色，内存布局与 WebGL 顶点数据一致（每通道 1 字节）。</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public struct Color : IEquatable<Color>
{
    public byte R, G, B, A;

    public Color(byte r, byte g, byte b, byte a = 255) { R = r; G = g; B = b; A = a; }

    public Color(int r, int g, int b, int a = 255)
        : this((byte)MathHelper.Clamp(r, 0, 255), (byte)MathHelper.Clamp(g, 0, 255),
               (byte)MathHelper.Clamp(b, 0, 255), (byte)MathHelper.Clamp(a, 0, 255)) { }

    public Color(float r, float g, float b, float a = 1f)
        : this((int)(r * 255f), (int)(g * 255f), (int)(b * 255f), (int)(a * 255f)) { }

    /// <summary>从 0xAARRGGBB 构造（与 CSS 习惯一致）。</summary>
    public static Color FromArgb(uint argb)
        => new((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb, (byte)(argb >> 24));

    public uint PackedValue
        => ((uint)A << 24) | ((uint)R << 16) | ((uint)G << 8) | B;

    public static Color Transparent => new(0, 0, 0, 0);
    public static Color White => new(255, 255, 255);
    public static Color Black => new(0, 0, 0);
    public static Color Red => new(255, 0, 0);
    public static Color Green => new(0, 255, 0);
    public static Color Blue => new(0, 0, 255);
    public static Color Yellow => new(255, 255, 0);
    public static Color Cyan => new(0, 255, 255);
    public static Color Magenta => new(255, 0, 255);
    public static Color Orange => new(255, 165, 0);
    public static Color CornflowerBlue => new(100, 149, 237);
    public static Color Gray => new(128, 128, 128);
    public static Color DarkGray => new(64, 64, 64);
    public static Color LightGray => new(200, 200, 200);

    /// <summary>线性插值，用于闪烁/淡入淡出。</summary>
    public static Color Lerp(Color a, Color b, float t)
        => new((byte)MathHelper.Lerp(a.R, b.R, t),
               (byte)MathHelper.Lerp(a.G, b.G, t),
               (byte)MathHelper.Lerp(a.B, b.B, t),
               (byte)MathHelper.Lerp(a.A, b.A, t));

    public Color WithAlpha(byte a) => new(R, G, B, a);

    /// <summary>按 0..1 系数调整亮度。</summary>
    public Color Multiply(float factor)
        => new((byte)MathHelper.Clamp(R * factor, 0, 255),
               (byte)MathHelper.Clamp(G * factor, 0, 255),
               (byte)MathHelper.Clamp(B * factor, 0, 255), A);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Color operator *(Color c, float f) => c.Multiply(f);

    public static bool operator ==(Color a, Color b) => a.PackedValue == b.PackedValue;
    public static bool operator !=(Color a, Color b) => !(a == b);

    public bool Equals(Color other) => PackedValue == other.PackedValue;
    public override bool Equals(object? obj) => obj is Color c && Equals(c);
    public override int GetHashCode() => (int)PackedValue;
    public override string ToString() => $"#{PackedValue:X8}";
}
