#nullable enable
using System;

namespace WebAssemblyBrowserApp.Engine;

/// <summary>3D 向量（double 精度），供矩阵运算使用。</summary>
public struct Vector3 : IEquatable<Vector3>
{
    public double X;
    public double Y;
    public double Z;

    public Vector3(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public static Vector3 Zero => new(0, 0, 0);
    public static Vector3 One => new(1, 1, 1);
    public static Vector3 UnitX => new(1, 0, 0);
    public static Vector3 UnitY => new(0, 1, 0);
    public static Vector3 UnitZ => new(0, 0, 1);

    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
    public double LengthSquared => X * X + Y * Y + Z * Z;

    public Vector3 Normalized()
    {
        double len = Length;
        return len > 1e-9 ? new Vector3(X / len, Y / len, Z / len) : Zero;
    }

    public double Dot(Vector3 other) => X * other.X + Y * other.Y + Z * other.Z;
    public Vector3 Cross(Vector3 other) => new(
        Y * other.Z - Z * other.Y,
        Z * other.X - X * other.Z,
        X * other.Y - Y * other.X);

    public static Vector3 Lerp(Vector3 a, Vector3 b, double t) => a + (b - a) * t;

    public bool Equals(Vector3 other) => X == other.X && Y == other.Y && Z == other.Z;
    public override bool Equals(object? obj) => obj is Vector3 v && Equals(v);
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    public static bool operator ==(Vector3 a, Vector3 b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;
    public static bool operator !=(Vector3 a, Vector3 b) => a.X != b.X || a.Y != b.Y || a.Z != b.Z;
    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3 operator *(Vector3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);
    public static Vector3 operator *(double s, Vector3 a) => new(a.X * s, a.Y * s, a.Z * s);
    public static Vector3 operator /(Vector3 a, double s) => new(a.X / s, a.Y / s, a.Z / s);
    public static Vector3 operator -(Vector3 v) => new(-v.X, -v.Y, -v.Z);

    public override string ToString() => $"({X:0.##}, {Y:0.##}, {Z:0.##})";
}
