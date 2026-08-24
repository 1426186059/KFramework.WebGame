#nullable enable
using System;

namespace WebAssemblyBrowserApp.Engine;

/// <summary>
/// 4×4 矩阵（float 精度，列向量约定：v' = M * v，组合时先应用右边的矩阵）。
/// 行主序存储 M11..M44，支持按 OpenGL/WebGL/WebGPU 的列主序导出（ToArrayColumnMajor）。
/// </summary>
public struct Matrix4x4 : IEquatable<Matrix4x4>
{
    public float M11, M12, M13, M14;
    public float M21, M22, M23, M24;
    public float M31, M32, M33, M34;
    public float M41, M42, M43, M44;

    public static Matrix4x4 Identity => new()
    {
        M11 = 1, M22 = 1, M33 = 1, M44 = 1,
    };

    // ---------------- 构建 ----------------

    public static Matrix4x4 CreateTranslation(float x, float y, float z)
    {
        var m = Identity;
        m.M41 = x; m.M42 = y; m.M43 = z;
        return m;
    }

    public static Matrix4x4 CreateTranslation(Vector3 v) => CreateTranslation(v.X, v.Y, v.Z);

    public static Matrix4x4 CreateScale(float x, float y, float z)
    {
        var m = Identity;
        m.M11 = x; m.M22 = y; m.M33 = z;
        return m;
    }

    public static Matrix4x4 CreateScale(Vector3 v) => CreateScale(v.X, v.Y, v.Z);

    public static Matrix4x4 CreateRotationX(float rad)
    {
        float c = MathF.Cos(rad), s = MathF.Sin(rad);
        var m = Identity;
        m.M22 = c; m.M23 = s;
        m.M32 = -s; m.M33 = c;
        return m;
    }

    public static Matrix4x4 CreateRotationY(float rad)
    {
        float c = MathF.Cos(rad), s = MathF.Sin(rad);
        var m = Identity;
        m.M11 = c; m.M13 = -s;
        m.M31 = s; m.M33 = c;
        return m;
    }

    public static Matrix4x4 CreateRotationZ(float rad)
    {
        float c = MathF.Cos(rad), s = MathF.Sin(rad);
        var m = Identity;
        m.M11 = c; m.M12 = s;
        m.M21 = -s; m.M22 = c;
        return m;
    }

    /// <summary>任意轴旋转（轴需为单位向量）。</summary>
    public static Matrix4x4 CreateRotation(Vector3 axis, float rad)
    {
        float c = MathF.Cos(rad), s = MathF.Sin(rad), t = 1 - c;
        float x = axis.X, y = axis.Y, z = axis.Z;
        return new Matrix4x4
        {
            M11 = t * x * x + c,        M12 = t * x * y + s * z,  M13 = t * x * z - s * y,  M14 = 0,
            M21 = t * x * y - s * z,    M22 = t * y * y + c,      M23 = t * y * z + s * x,  M24 = 0,
            M31 = t * x * z + s * y,    M32 = t * y * z - s * x,  M33 = t * z * z + c,      M34 = 0,
            M41 = 0, M42 = 0, M43 = 0, M44 = 1,
        };
    }

    /// <summary>TRS 组合（先缩放，后旋转，再平移）。rotationRad 为 Z→Y→X 顺序的欧拉角（弧度）。</summary>
    public static Matrix4x4 CreateTRS(Vector3 translation, Vector3 rotationRad, Vector3 scale)
        => CreateTranslation(translation) * CreateRotationZ(rotationRad.Z) * CreateRotationY(rotationRad.Y) * CreateRotationX(rotationRad.X) * CreateScale(scale);

    /// <summary>正交投影（NDC：x/y ∈ [-1,1]，z ∈ [-1,1]）。</summary>
    public static Matrix4x4 CreateOrthographic(float left, float right, float bottom, float top, float near, float far)
    {
        var m = Identity;
        m.M11 = 2f / (right - left);
        m.M22 = 2f / (top - bottom);
        m.M33 = -2f / (far - near);
        m.M41 = -(right + left) / (right - left);
        m.M42 = -(top + bottom) / (top - bottom);
        m.M43 = -(far + near) / (far - near);
        return m;
    }

    /// <summary>标准 2D 正交投影：逻辑像素 → NDC（Y 向下，原点在左上角）。</summary>
    public static Matrix4x4 CreateOrthographic2D(float width, float height)
        => CreateOrthographic(0, width, height, 0, -1, 1);

    /// <summary>透视投影（fovY 弧度，near &gt; 0）。</summary>
    public static Matrix4x4 CreatePerspective(float fovY, float aspect, float near, float far)
    {
        float f = 1f / MathF.Tan(fovY / 2f);
        return new Matrix4x4
        {
            M11 = f / aspect, M12 = 0, M13 = 0, M14 = 0,
            M21 = 0, M22 = f, M23 = 0, M24 = 0,
            M31 = 0, M32 = 0, M33 = (far + near) / (near - far), M34 = -1,
            M41 = 0, M42 = 0, M43 = 2 * far * near / (near - far), M44 = 0,
        };
    }

    /// <summary>视图矩阵（右手系，摄像机看向 -Z）。</summary>
    public static Matrix4x4 CreateLookAt(Vector3 eye, Vector3 target, Vector3 up)
    {
        Vector3 z = (eye - target).Normalized();
        Vector3 x = up.Cross(z).Normalized();
        Vector3 y = z.Cross(x);
        return new Matrix4x4
        {
            M11 = x.X, M12 = y.X, M13 = z.X, M14 = 0,
            M21 = x.Y, M22 = y.Y, M23 = z.Y, M24 = 0,
            M31 = x.Z, M32 = y.Z, M33 = z.Z, M34 = 0,
            M41 = -x.Dot(eye), M42 = -y.Dot(eye), M43 = -z.Dot(eye), M44 = 1,
        };
    }

    // ---------------- 运算 ----------------

    public static Matrix4x4 operator *(Matrix4x4 a, Matrix4x4 b) => Multiply(a, b);

    public static Matrix4x4 Multiply(Matrix4x4 a, Matrix4x4 b) => new()
    {
        M11 = a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31 + a.M14 * b.M41,
        M12 = a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32 + a.M14 * b.M42,
        M13 = a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33 + a.M14 * b.M43,
        M14 = a.M11 * b.M14 + a.M12 * b.M24 + a.M13 * b.M34 + a.M14 * b.M44,

        M21 = a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31 + a.M24 * b.M41,
        M22 = a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32 + a.M24 * b.M42,
        M23 = a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33 + a.M24 * b.M43,
        M24 = a.M21 * b.M14 + a.M22 * b.M24 + a.M23 * b.M34 + a.M24 * b.M44,

        M31 = a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31 + a.M34 * b.M41,
        M32 = a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32 + a.M34 * b.M42,
        M33 = a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33 + a.M34 * b.M43,
        M34 = a.M31 * b.M14 + a.M32 * b.M24 + a.M33 * b.M34 + a.M34 * b.M44,

        M41 = a.M41 * b.M11 + a.M42 * b.M21 + a.M43 * b.M31 + a.M44 * b.M41,
        M42 = a.M41 * b.M12 + a.M42 * b.M22 + a.M43 * b.M32 + a.M44 * b.M42,
        M43 = a.M41 * b.M13 + a.M42 * b.M23 + a.M43 * b.M33 + a.M44 * b.M43,
        M44 = a.M41 * b.M14 + a.M42 * b.M24 + a.M43 * b.M34 + a.M44 * b.M44,
    };

    /// <summary>变换点（齐次除法，w 非 1 时归一化）。</summary>
    public Vector3 TransformPoint(Vector3 v)
    {
        float x = M11 * v.X + M21 * v.Y + M31 * v.Z + M41;
        float y = M12 * v.X + M22 * v.Y + M32 * v.Z + M42;
        float z = M13 * v.X + M23 * v.Y + M33 * v.Z + M43;
        float w = M14 * v.X + M24 * v.Y + M34 * v.Z + M44;
        if (MathF.Abs(w) > 1e-6f && MathF.Abs(w - 1) > 1e-6f)
        {
            x /= w; y /= w; z /= w;
        }
        return new Vector3(x, y, z);
    }

    /// <summary>变换方向（忽略平移）。</summary>
    public Vector3 TransformDirection(Vector3 v) => new(
        M11 * v.X + M21 * v.Y + M31 * v.Z,
        M12 * v.X + M22 * v.Y + M32 * v.Z,
        M13 * v.X + M23 * v.Y + M33 * v.Z);

    public float Determinant()
    {
        float a00 = M11, a01 = M12, a02 = M13, a03 = M14;
        float a10 = M21, a11 = M22, a12 = M23, a13 = M24;
        float a20 = M31, a21 = M32, a22 = M33, a23 = M34;
        float a30 = M41, a31 = M42, a32 = M43, a33 = M44;

        float b00 = a00 * a11 - a01 * a10;
        float b01 = a00 * a12 - a02 * a10;
        float b02 = a00 * a13 - a03 * a10;
        float b03 = a01 * a12 - a02 * a11;
        float b04 = a01 * a13 - a03 * a11;
        float b05 = a02 * a13 - a03 * a12;
        float b06 = a20 * a31 - a21 * a30;
        float b07 = a20 * a32 - a22 * a30;
        float b08 = a20 * a33 - a23 * a30;
        float b09 = a21 * a32 - a22 * a31;
        float b10 = a21 * a33 - a23 * a31;
        float b11 = a22 * a33 - a23 * a32;

        return b00 * b11 - b01 * b10 + b02 * b09 + b03 * b08 - b04 * b07 + b05 * b06;
    }

    public Matrix4x4 Inverted()
    {
        if (!TryInvert(out var inv)) return Identity;
        return inv;
    }

    public bool TryInvert(out Matrix4x4 result)
    {
        float a00 = M11, a01 = M12, a02 = M13, a03 = M14;
        float a10 = M21, a11 = M22, a12 = M23, a13 = M24;
        float a20 = M31, a21 = M32, a22 = M33, a23 = M34;
        float a30 = M41, a31 = M42, a32 = M43, a33 = M44;

        float b00 = a00 * a11 - a01 * a10;
        float b01 = a00 * a12 - a02 * a10;
        float b02 = a00 * a13 - a03 * a10;
        float b03 = a01 * a12 - a02 * a11;
        float b04 = a01 * a13 - a03 * a11;
        float b05 = a02 * a13 - a03 * a12;
        float b06 = a20 * a31 - a21 * a30;
        float b07 = a20 * a32 - a22 * a30;
        float b08 = a20 * a33 - a23 * a30;
        float b09 = a21 * a32 - a22 * a31;
        float b10 = a21 * a33 - a23 * a31;
        float b11 = a22 * a33 - a23 * a32;

        float det = b00 * b11 - b01 * b10 + b02 * b09 + b03 * b08 - b04 * b07 + b05 * b06;
        if (MathF.Abs(det) < 1e-6f)
        {
            result = Identity;
            return false;
        }
        float invDet = 1f / det;

        result = new Matrix4x4
        {
            M11 = (a11 * b11 - a12 * b10 + a13 * b09) * invDet,
            M12 = (a02 * b10 - a01 * b11 - a03 * b09) * invDet,
            M13 = (a31 * b05 - a32 * b04 + a33 * b03) * invDet,
            M14 = (a22 * b04 - a21 * b05 - a23 * b03) * invDet,
            M21 = (a12 * b08 - a10 * b11 - a13 * b07) * invDet,
            M22 = (a00 * b11 - a02 * b08 + a03 * b07) * invDet,
            M23 = (a32 * b02 - a30 * b05 - a33 * b01) * invDet,
            M24 = (a20 * b05 - a22 * b02 + a23 * b01) * invDet,
            M31 = (a10 * b10 - a11 * b08 + a13 * b06) * invDet,
            M32 = (a01 * b08 - a00 * b10 - a03 * b06) * invDet,
            M33 = (a30 * b04 - a31 * b02 + a33 * b00) * invDet,
            M34 = (a21 * b02 - a20 * b04 - a23 * b00) * invDet,
            M41 = (a11 * b07 - a10 * b09 - a12 * b06) * invDet,
            M42 = (a00 * b09 - a01 * b07 + a02 * b06) * invDet,
            M43 = (a31 * b01 - a30 * b03 - a32 * b00) * invDet,
            M44 = (a20 * b03 - a21 * b01 + a22 * b00) * invDet,
        };
        return true;
    }

    public Matrix4x4 Transposed() => new()
    {
        M11 = M11, M12 = M21, M13 = M31, M14 = M41,
        M21 = M12, M22 = M22, M23 = M32, M24 = M42,
        M31 = M13, M32 = M23, M33 = M33, M34 = M43,
        M41 = M14, M42 = M24, M43 = M34, M44 = M44,
    };

    // ---------------- 导出 ----------------

    /// <summary>导出为 OpenGL/WebGL/WebGPU 列主序 float[16]（上传 uniform 直接可用）。</summary>
    public float[] ToArrayColumnMajor() => new float[16]
    {
        M11, M12, M13, M14,
        M21, M22, M23, M24,
        M31, M32, M33, M34,
        M41, M42, M43, M44,
    };

    public float[] ToArrayRowMajor() => new float[16]
    {
        M11, M12, M13, M14,
        M21, M22, M23, M24,
        M31, M32, M33, M34,
        M41, M42, M43, M44,
    };

    // ---------------- 相等 ----------------

    public bool Equals(Matrix4x4 other) =>
        M11 == other.M11 && M12 == other.M12 && M13 == other.M13 && M14 == other.M14 &&
        M21 == other.M21 && M22 == other.M22 && M23 == other.M23 && M24 == other.M24 &&
        M31 == other.M31 && M32 == other.M32 && M33 == other.M33 && M34 == other.M34 &&
        M41 == other.M41 && M42 == other.M42 && M43 == other.M43 && M44 == other.M44;

    public override bool Equals(object? obj) => obj is Matrix4x4 m && Equals(m);
    public override int GetHashCode() => HashCode.Combine(
        HashCode.Combine(M11, M12, M13, M14, M21, M22, M23, M24),
        HashCode.Combine(M31, M32, M33, M34, M41, M42, M43, M44));

    public static bool operator ==(Matrix4x4 a, Matrix4x4 b) => a.Equals(b);
    public static bool operator !=(Matrix4x4 a, Matrix4x4 b) => !a.Equals(b);
}
