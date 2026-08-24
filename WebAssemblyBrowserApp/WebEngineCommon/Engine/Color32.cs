#nullable enable
using System;

namespace WebAssemblyBrowserApp.Engine;

/// <summary>8bit 每通道的颜色（适合打包成 RGBA8888 像素 / 纹理数据）。</summary>
public struct Color32 : IEquatable<Color32>
{
    public byte R;
    public byte G;
    public byte B;
    public byte A;

    public Color32(byte r, byte g, byte b, byte a = 255)
    {
        R = r; G = g; B = b; A = a;
    }

    public static Color32 White => new(255, 255, 255, 255);
    public static Color32 Black => new(0, 0, 0, 255);
    public static Color32 Transparent => new(0, 0, 0, 0);

    public static implicit operator Color(Color32 c) => new(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);

    /// <summary>打包为 uint（RGBA 顺序）。</summary>
    public uint ToUInt32() => (uint)((R << 24) | (G << 16) | (B << 8) | A);

    public static Color32 FromUInt32(uint rgba) => new(
        (byte)(rgba >> 24), (byte)(rgba >> 16), (byte)(rgba >> 8), (byte)rgba);

    public bool Equals(Color32 other) => R == other.R && G == other.G && B == other.B && A == other.A;
    public override bool Equals(object? obj) => obj is Color32 c && Equals(c);
    public override int GetHashCode() => HashCode.Combine(R, G, B, A);

    public static bool operator ==(Color32 a, Color32 b) => a.Equals(b);
    public static bool operator !=(Color32 a, Color32 b) => !a.Equals(b);

    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}{A:X2}";
}
