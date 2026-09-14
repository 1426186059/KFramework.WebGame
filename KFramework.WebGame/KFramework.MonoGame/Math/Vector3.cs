using System.Runtime.CompilerServices;

namespace KFramework.MonoGame
{

    /// <summary>
    /// 三维向量。2D 游戏里通常只用到 X / Y，Z 用于层深或第三方移植代码。
    /// 同时提供 MonoGame 风格（X / Zero）与 Unity 风格（x / zero）两套成员，
    /// 便于直接吃下从 Unity 移植过来的工具代码。
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct Vector3 : IEquatable<Vector3>
    {
        public float X;
        public float Y;
        public float Z;

        public Vector3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public Vector3(float x, float y) : this(x, y, 0f) { }
        public Vector3(float value) : this(value, value, value) { }
        public Vector3(Vector2 xy, float z) : this(xy.X, xy.Y, z) { }

        // Unity 风格别名
        public float x { readonly get => X; set => X = value; }
        public float y { readonly get => Y; set => Y = value; }
        public float z { readonly get => Z; set => Z = value; }

        public static Vector3 Zero => default;
        public static Vector3 One => new(1f, 1f, 1f);
        public static Vector3 UnitX => new(1f, 0f, 0f);
        public static Vector3 UnitY => new(0f, 1f, 0f);
        public static Vector3 UnitZ => new(0f, 0f, 1f);

        // Unity 风格别名
        public static Vector3 zero => Zero;
        public static Vector3 one => One;
        public static Vector3 forward => UnitZ;
        public static Vector3 up => UnitY;
        public static Vector3 right => UnitX;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Length() => MathF.Sqrt(X * X + Y * Y + Z * Z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float LengthSquared() => X * X + Y * Y + Z * Z;

        public void Normalize()
        {
            float len = Length();
            if (len > 1e-8f) { X /= len; Y /= len; Z /= len; }
        }

        public static Vector3 Normalize(Vector3 v)
        {
            v.Normalize();
            return v;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Distance(Vector3 a, Vector3 b) => (a - b).Length();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Dot(Vector3 a, Vector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static Vector3 Cross(Vector3 a, Vector3 b)
            => new(a.Y * b.Z - a.Z * b.Y,
                   a.Z * b.X - a.X * b.Z,
                   a.X * b.Y - a.Y * b.X);

        public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
            => new(MathHelper.Lerp(a.X, b.X, t),
                   MathHelper.Lerp(a.Y, b.Y, t),
                   MathHelper.Lerp(a.Z, b.Z, t));

        /// <summary>
        /// Unity 的 LerpPrecise：先算 (1 - t) * a 再与 t * b 相加，
        /// 比 a + (b - a) * t 在端点处更精确（t=1 时能精确得到 b）。
        /// </summary>
        public static Vector3 LerpPrecise(Vector3 a, Vector3 b, float t)
            => new((1f - t) * a.X + t * b.X,
                   (1f - t) * a.Y + t * b.Y,
                   (1f - t) * a.Z + t * b.Z);

        public static Vector3 Clamp(Vector3 v, Vector3 min, Vector3 max)
            => new(MathHelper.Clamp(v.X, min.X, max.X),
                   MathHelper.Clamp(v.Y, min.Y, max.Y),
                   MathHelper.Clamp(v.Z, min.Z, max.Z));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 operator -(Vector3 v) => new(-v.X, -v.Y, -v.Z);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 operator *(Vector3 v, float s) => new(v.X * s, v.Y * s, v.Z * s);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 operator *(float s, Vector3 v) => new(v.X * s, v.Y * s, v.Z * s);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 operator *(Vector3 a, Vector3 b) => new(a.X * b.X, a.Y * b.Y, a.Z * b.Z);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 operator /(Vector3 v, float s) => new(v.X / s, v.Y / s, v.Z / s);

        public static bool operator ==(Vector3 a, Vector3 b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;
        public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);

        public static implicit operator Vector3(Vector2 v) => new(v.X, v.Y, 0f);
        public static explicit operator Vector2(Vector3 v) => new(v.X, v.Y);

        public bool Equals(Vector3 other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object? obj) => obj is Vector3 v && Equals(v);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
        public override string ToString() => $"({X}, {Y}, {Z})";
    }
}
