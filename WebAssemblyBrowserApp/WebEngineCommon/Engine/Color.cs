#nullable enable
using System;
using System.Globalization;

namespace WebAssemblyBrowserApp.Engine;

/// <summary>
/// 颜色（float 精度，R/G/B/A 取值范围 0..1）。
/// 支持 Hex 解析（#rgb / #rgba / #rrggbb / #rrggbbaa）、HSB 转换、
/// 预置色、加法/乘法混合，以及 CSS 字符串输出（可直传 Canvas2D / WebGL / WebGPU 层）。
/// </summary>
public struct Color : IEquatable<Color>
{
    public float R;
    public float G;
    public float B;
    public float A;

    public Color(float r, float g, float b, float a = 1f)
    {
        R = r; G = g; B = b; A = a;
    }

    // ---------------- 预置色 ----------------

    public static Color Transparent => new(0, 0, 0, 0);
    public static Color White => new(1, 1, 1, 1);
    public static Color Black => new(0, 0, 0, 1);
    public static Color Red => new(1, 0, 0, 1);
    public static Color Green => new(0, 1, 0, 1);
    public static Color Blue => new(0, 0, 1, 1);
    public static Color Yellow => new(1, 1, 0, 1);
    public static Color Cyan => new(0, 1, 1, 1);
    public static Color Magenta => new(1, 0, 1, 1);
    public static Color Gray => new(0.5f, 0.5f, 0.5f, 1);
    public static Color DarkGray => new(0.25f, 0.25f, 0.25f, 1);
    public static Color LightGray => new(0.75f, 0.75f, 0.75f, 1);
    public static Color Orange => new(1f, 0.65f, 0f, 1);
    public static Color Purple => new(0.5f, 0f, 0.5f, 1);
    public static Color Brown => new(0.55f, 0.27f, 0.07f, 1);
    public static Color Pink => new(1f, 0.75f, 0.8f, 1);

    // ---------------- 构建 ----------------

    /// <summary>8bit 分量构造。</summary>
    public static Color FromRgb(byte r, byte g, byte b) => FromRgba(r, g, b, 255);
    public static Color FromRgba(byte r, byte g, byte b, byte a) => new(r / 255f, g / 255f, b / 255f, a / 255f);

    /// <summary>解析 "#rgb"、"#rgba"、"#rrggbb"、"#rrggbbaa"（可带或不带 #，不区分大小写）。失败返回 null。</summary>
    public static Color? TryFromHex(string? hex)
    {
        if (string.IsNullOrEmpty(hex)) return null;
        string h = hex.TrimStart('#');
        if (h.Length is not (3 or 4 or 6 or 8)) return null;

        try
        {
            int[] c = new int[h.Length / 2 + h.Length % 2];
            for (int i = 0; i < c.Length; i++)
            {
                if (h.Length == 3 || h.Length == 4)
                {
                    string ch = h.Substring(i, 1);
                    c[i] = int.Parse(ch + ch, NumberStyles.HexNumber);
                }
                else
                {
                    c[i] = int.Parse(h.Substring(i * 2, 2), NumberStyles.HexNumber);
                }
            }
            return new Color(
                c[0] / 255f, c[1] / 255f, c[2] / 255f,
                c.Length > 3 ? c[3] / 255f : 1f);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>解析 Hex，失败时返回 fallback（默认 White）。</summary>
    public static Color FromHex(string? hex, Color fallback = default)
    {
        if (fallback.A == 0 && fallback.R == 0 && fallback.G == 0 && fallback.B == 0 && string.IsNullOrEmpty(hex))
            return White;
        return TryFromHex(hex) ?? fallback;
    }

    /// <summary>HSB 构造。h ∈ [0,360)，s/b ∈ [0,1]。</summary>
    public static Color FromHsb(float h, float s, float b, float a = 1f)
    {
        h = ((h % 360) + 360) % 360;
        float c = b * s;
        float x = c * (1 - MathF.Abs((h / 60) % 2 - 1));
        float m = b - c;
        float r = 0, g = 0, bl = 0;
        if (h < 60) { r = c; g = x; }
        else if (h < 120) { r = x; g = c; }
        else if (h < 180) { g = c; bl = x; }
        else if (h < 240) { g = x; bl = c; }
        else if (h < 300) { r = x; bl = c; }
        else { r = c; bl = x; }
        return new Color(r + m, g + m, bl + m, a);
    }

    // ---------------- HSB ----------------

    /// <summary>色相（度，[0,360)）。</summary>
    public float Hue
    {
        get
        {
            float max = Math.Max(R, Math.Max(G, B));
            float min = Math.Min(R, Math.Min(G, B));
            float d = max - min;
            if (d == 0) return 0;
            float h;
            if (max == R) h = 60 * (((G - B) / d) % 6);
            else if (max == G) h = 60 * (((B - R) / d) + 2);
            else h = 60 * (((R - G) / d) + 4);
            return h < 0 ? h + 360 : h;
        }
    }

    public float Saturation
    {
        get
        {
            float max = Math.Max(R, Math.Max(G, B));
            float min = Math.Min(R, Math.Min(G, B));
            float d = max - min;
            return max == 0 ? 0 : d / max;
        }
    }

    public float Brightness => Math.Max(R, Math.Max(G, B));

    /// <summary>感知亮度（0..1），用于判断文字用深色还是浅色。</summary>
    public float Luminance => 0.299f * R + 0.587f * G + 0.114f * B;

    /// <summary>判断该颜色是否偏暗（深色背景上适合用白字）。</summary>
    public bool IsDark => Luminance < 0.5;

    // ---------------- 运算 ----------------

    public Color WithAlpha(float a) => new(R, G, B, a);

    public static Color operator +(Color a, Color b) => new(a.R + b.R, a.G + b.G, a.B + b.B, a.A + b.A);
    public static Color operator *(Color a, float s) => new(a.R * s, a.G * s, a.B * s, a.A * s);
    public static Color operator *(Color a, Color b) => new(a.R * b.R, a.G * b.G, a.B * b.B, a.A * b.A);

    /// <summary>线性插值混合。</summary>
    public static Color Lerp(Color a, Color b, float t) => new(
        a.R + (b.R - a.R) * t,
        a.G + (b.G - a.G) * t,
        a.B + (b.B - a.B) * t,
        a.A + (b.A - a.A) * t);

    /// <summary>乘以亮度系数（0..1），用于"变暗/变亮"。</summary>
    public Color Scaled(float factor) => new(
        Math.Clamp(R * factor, 0, 1),
        Math.Clamp(G * factor, 0, 1),
        Math.Clamp(B * factor, 0, 1),
        A);

    // ---------------- 输出 ----------------

    /// <summary>CSS 字符串（rgba() 或 rgb()）。可直传 Canvas2D fillStyle / WebGL 色值解析。</summary>
    public string ToCssString()
    {
        int r = Math.Clamp((int)Math.Round(R * 255), 0, 255);
        int g = Math.Clamp((int)Math.Round(G * 255), 0, 255);
        int b = Math.Clamp((int)Math.Round(B * 255), 0, 255);
        return A >= 1f ? $"rgb({r},{g},{b})" : $"rgba({r},{g},{b},{A:0.###})";
    }

    /// <summary>#rrggbbaa 十六进制串（始终 8 位）。</summary>
    public string ToHexString() => $"#{ToByte(R):X2}{ToByte(G):X2}{ToByte(B):X2}{ToByte(A):X2}";

    private static int ToByte(float v) => Math.Clamp((int)Math.Round(v * 255), 0, 255);

    public static implicit operator string(Color c) => c.ToCssString();

    public override string ToString() => A >= 1f
        ? $"Color(#{ToByte(R):X2}{ToByte(G):X2}{ToByte(B):X2})"
        : $"Color(#{ToByte(R):X2}{ToByte(G):X2}{ToByte(B):X2}{ToByte(A):X2})";

    // ---------------- 相等 ----------------

    public bool Equals(Color other) => R == other.R && G == other.G && B == other.B && A == other.A;
    public override bool Equals(object? obj) => obj is Color c && Equals(c);
    public override int GetHashCode() => HashCode.Combine(R, G, B, A);

    public static bool operator ==(Color a, Color b) => a.Equals(b);
    public static bool operator !=(Color a, Color b) => !a.Equals(b);
}
