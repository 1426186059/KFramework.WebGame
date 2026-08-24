#nullable enable
using System;

namespace WebAssemblyBrowserApp.Engine;

/// <summary>2D 向量（double 精度）。</summary>
public struct Vector2 : IEquatable<Vector2>
{
    public double X;
    public double Y;

    public Vector2(double x, double y)
    {
        X = x;
        Y = y;
    }

    public static Vector2 Zero => new(0, 0);
    public static Vector2 One => new(1, 1);
    public static Vector2 UnitX => new(1, 0);
    public static Vector2 UnitY => new(0, 1);

    public double Length => Math.Sqrt(X * X + Y * Y);
    public double LengthSquared => X * X + Y * Y;

    public Vector2 Normalized()
    {
        double len = Length;
        return len > 1e-9 ? new Vector2(X / len, Y / len) : Zero;
    }

    public double Dot(Vector2 other) => X * other.X + Y * other.Y;

    /// <summary>2D 叉积（z 分量），用于方向判断。</summary>
    public double Cross(Vector2 other) => X * other.Y - Y * other.X;

    public Vector2 Perp() => new(-Y, X);

    /// <summary>绕原点旋转（angle 弧度，顺时针为正，与屏幕坐标系一致）。</summary>
    public Vector2 Rotated(double angle)
    {
        double c = Math.Cos(angle), s = Math.Sin(angle);
        return new Vector2(X * c - Y * s, X * s + Y * c);
    }

    public static double Distance(Vector2 a, Vector2 b) => (a - b).Length;
    public static double DistanceSquared(Vector2 a, Vector2 b) => (a - b).LengthSquared;

    public static Vector2 Lerp(Vector2 a, Vector2 b, double t) => a + (b - a) * t;

    public bool Equals(Vector2 other) => X == other.X && Y == other.Y;
    public override bool Equals(object? obj) => obj is Vector2 v && Equals(v);
    public override int GetHashCode() => HashCode.Combine(X, Y);

    public static bool operator ==(Vector2 a, Vector2 b) => a.X == b.X && a.Y == b.Y;
    public static bool operator !=(Vector2 a, Vector2 b) => a.X != b.X || a.Y != b.Y;
    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2 operator *(Vector2 a, double s) => new(a.X * s, a.Y * s);
    public static Vector2 operator *(double s, Vector2 a) => new(a.X * s, a.Y * s);
    public static Vector2 operator /(Vector2 a, double s) => new(a.X / s, a.Y / s);
    public static Vector2 operator -(Vector2 v) => new(-v.X, -v.Y);

    public override string ToString() => $"({X:0.##}, {Y:0.##})";
}
