using System;

namespace WebAssemblyBrowserApp.Engine;

/// <summary>2D 向量。</summary>
public struct Vector2
{
    public double X;
    public double Y;

    public Vector2(double x, double y)
    {
        X = x;
        Y = y;
    }

    public static Vector2 Zero => new(0, 0);

    public double Length => Math.Sqrt(X * X + Y * Y);
    public double LengthSquared => X * X + Y * Y;

    public Vector2 Normalized()
    {
        double len = Length;
        return len > 1e-9 ? new Vector2(X / len, Y / len) : Zero;
    }

    public double Dot(Vector2 other) => X * other.X + Y * other.Y;
    public Vector2 Perp() => new(-Y, X);

    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2 operator *(Vector2 a, double s) => new(a.X * s, a.Y * s);
    public static Vector2 operator /(Vector2 a, double s) => new(a.X / s, a.Y / s);
    public static Vector2 operator -(Vector2 v) => new(-v.X, -v.Y);
}
