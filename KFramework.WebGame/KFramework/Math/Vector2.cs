using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace KFramework;

/// <summary>二维向量（SIMD 友好的 64 位结构，可直接传给 WebGL）。</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public struct Vector2 : IEquatable<Vector2>
{
    public float X;
    public float Y;

    public Vector2(float x, float y) { X = x; Y = y; }
    public Vector2(float value) { X = value; Y = value; }

    public static Vector2 Zero => default;
    public static Vector2 One => new(1f, 1f);
    public static Vector2 UnitX => new(1f, 0f);
    public static Vector2 UnitY => new(0f, 1f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Length() => MathF.Sqrt(X * X + Y * Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float LengthSquared() => X * X + Y * Y;

    public void Normalize()
    {
        float len = Length();
        if (len > 1e-8f) { X /= len; Y /= len; }
    }

    public static Vector2 Normalize(Vector2 v)
    {
        v.Normalize();
        return v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Distance(Vector2 a, Vector2 b) => (a - b).Length();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DistanceSquared(Vector2 a, Vector2 b) => (a - b).LengthSquared();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Dot(Vector2 a, Vector2 b) => a.X * b.X + a.Y * b.Y;

    /// <summary>绕原点旋转（顺时针为屏幕坐标系下的正方向）。</summary>
    public static Vector2 Rotate(Vector2 v, float radians)
    {
        float c = MathF.Cos(radians), s = MathF.Sin(radians);
        return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
    }

    public static Vector2 Lerp(Vector2 a, Vector2 b, float t)
        => new(MathHelper.Lerp(a.X, b.X, t), MathHelper.Lerp(a.Y, b.Y, t));

    public static Vector2 Clamp(Vector2 v, Vector2 min, Vector2 max)
        => new(MathHelper.Clamp(v.X, min.X, max.X), MathHelper.Clamp(v.Y, min.Y, max.Y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 operator -(Vector2 v) => new(-v.X, -v.Y);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 operator *(Vector2 v, float s) => new(v.X * s, v.Y * s);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 operator *(float s, Vector2 v) => new(v.X * s, v.Y * s);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 operator *(Vector2 a, Vector2 b) => new(a.X * b.X, a.Y * b.Y);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 operator /(Vector2 v, float s) => new(v.X / s, v.Y / s);

    public static bool operator ==(Vector2 a, Vector2 b) => a.X == b.X && a.Y == b.Y;
    public static bool operator !=(Vector2 a, Vector2 b) => !(a == b);

    public bool Equals(Vector2 other) => X == other.X && Y == other.Y;
    public override bool Equals(object? obj) => obj is Vector2 v && Equals(v);
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public override string ToString() => $"({X}, {Y})";

    public static implicit operator Vector64<float>(Vector2 v)
        => Vector64.Create(v.X, v.Y);
}
