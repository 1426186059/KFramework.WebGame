#nullable enable
using System;

namespace WebAssemblyBrowserApp.Engine;

/// <summary>
/// 3×2 2D 仿射变换矩阵（double 精度，列向量约定：v' = M * v，组合时先应用右边的矩阵）。
/// 行主序存储：
///   | M11 M12 0 |
///   | M21 M22 0 |
///   | M31 M32 1 |   （M31/M32 为平移）
/// 屏幕坐标系 Y 向下，正角度为顺时针旋转。
/// </summary>
public struct Matrix3x2 : IEquatable<Matrix3x2>
{
    public double M11, M12;
    public double M21, M22;
    public double M31, M32;

    public static Matrix3x2 Identity => new()
    {
        M11 = 1, M22 = 1,
    };

    public Matrix3x2(double m11, double m12, double m21, double m22, double m31, double m32)
    {
        M11 = m11; M12 = m12;
        M21 = m21; M22 = m22;
        M31 = m31; M32 = m32;
    }

    // ---------------- 构建 ----------------

    public static Matrix3x2 CreateTranslation(double tx, double ty)
    {
        var m = Identity;
        m.M31 = tx; m.M32 = ty;
        return m;
    }

    public static Matrix3x2 CreateScale(double sx, double sy)
    {
        var m = Identity;
        m.M11 = sx; m.M22 = sy;
        return m;
    }

    public static Matrix3x2 CreateScale(double uniform) => CreateScale(uniform, uniform);

    /// <summary>绕原点旋转（弧度，屏幕系下正角为顺时针）。</summary>
    public static Matrix3x2 CreateRotation(double angle)
    {
        double c = Math.Cos(angle), s = Math.Sin(angle);
        return new Matrix3x2(c, s, -s, c, 0, 0);
    }

    /// <summary>绕任意枢轴点旋转（= T(pivot) * R * T(-pivot)）。</summary>
    public static Matrix3x2 CreateRotationAround(double angle, double px, double py)
    {
        double c = Math.Cos(angle), s = Math.Sin(angle);
        return new Matrix3x2(
            c, s,
            -s, c,
            px - c * px + s * py,
            py - s * px - c * py);
    }

    public static Matrix3x2 CreateTRS(double tx, double ty, double rotation, double sx = 1, double sy = 1)
        => CreateTranslation(tx, ty) * CreateRotation(rotation) * CreateScale(sx, sy);

    // ---------------- 运算 ----------------

    public static Matrix3x2 operator *(Matrix3x2 a, Matrix3x2 b) => Multiply(a, b);

    public static Matrix3x2 Multiply(Matrix3x2 a, Matrix3x2 b) => new(
        a.M11 * b.M11 + a.M21 * b.M12,
        a.M12 * b.M11 + a.M22 * b.M12,
        a.M11 * b.M21 + a.M21 * b.M22,
        a.M12 * b.M21 + a.M22 * b.M22,
        a.M11 * b.M31 + a.M21 * b.M32 + b.M31,
        a.M12 * b.M31 + a.M22 * b.M32 + b.M32);

    /// <summary>变换点（含平移）。</summary>
    public Vector2 TransformPoint(Vector2 v) => new(
        M11 * v.X + M21 * v.Y + M31,
        M12 * v.X + M22 * v.Y + M32);

    public Vector2 TransformPoint(double x, double y) => new(
        M11 * x + M21 * y + M31,
        M12 * x + M22 * y + M32);

    /// <summary>变换向量（忽略平移）。</summary>
    public Vector2 TransformVector(Vector2 v) => new(
        M11 * v.X + M21 * v.Y,
        M12 * v.X + M22 * v.Y);

    public double Determinant => M11 * M22 - M12 * M21;

    /// <summary>求逆；行列式为 0 时返回单位矩阵。</summary>
    public Matrix3x2 Inverted()
    {
        if (!TryInvert(out var inv)) return Identity;
        return inv;
    }

    public bool TryInvert(out Matrix3x2 result)
    {
        double det = M11 * M22 - M12 * M21;
        if (Math.Abs(det) < 1e-12)
        {
            result = Identity;
            return false;
        }
        double invDet = 1.0 / det;
        result = new Matrix3x2(
            M22 * invDet,
            -M12 * invDet,
            -M21 * invDet,
            M11 * invDet,
            (M21 * M32 - M31 * M22) * invDet,
            (M31 * M12 - M11 * M32) * invDet);
        return true;
    }

    // ---------------- 相等 ----------------

    public bool Equals(Matrix3x2 other) =>
        M11 == other.M11 && M12 == other.M12 && M21 == other.M21 &&
        M22 == other.M22 && M31 == other.M31 && M32 == other.M32;

    public override bool Equals(object? obj) => obj is Matrix3x2 m && Equals(m);
    public override int GetHashCode() => HashCode.Combine(M11, M12, M21, M22, M31, M32);

    public static bool operator ==(Matrix3x2 a, Matrix3x2 b) => a.Equals(b);
    public static bool operator !=(Matrix3x2 a, Matrix3x2 b) => !a.Equals(b);

    public override string ToString() => $"[{M11:0.##},{M12:0.##}; {M21:0.##},{M22:0.##}; {M31:0.##},{M32:0.##}]";
}
