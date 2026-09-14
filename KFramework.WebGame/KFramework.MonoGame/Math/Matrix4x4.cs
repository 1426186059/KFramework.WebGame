using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace KFramework.MonoGame
{

    /// <summary>
    /// 列主序 4x4 矩阵，内存布局与 WebGL uniformMatrix4fv 期望的完全一致（可按原样上传）。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Matrix4x4
    {
        public float M11, M21, M31, M41;
        public float M12, M22, M32, M42;
        public float M13, M23, M33, M43;
        public float M14, M24, M34, M44;

        public static Matrix4x4 Identity => new()
        {
            M11 = 1f, M22 = 1f, M33 = 1f, M44 = 1f
        };

        /// <summary>
        /// 2D 正交投影：屏幕坐标 (0,0)-(width,height)，Y 轴向下，(0,0) 在左上角。
        /// </summary>
        public static Matrix4x4 CreateOrthographicOffCenter(float left, float right, float bottom, float top, float zNear, float zFar)
        {
            Matrix4x4 m = Identity;
            m.M11 = 2f / (right - left);
            m.M22 = 2f / (top - bottom);
            m.M33 = -2f / (zFar - zNear);
            // 列主序：平移位于第 4 列（M14/M24/M34），不是第 4 行
            m.M14 = -(right + left) / (right - left);
            m.M24 = -(top + bottom) / (top - bottom);
            m.M34 = -(zFar + zNear) / (zFar - zNear);
            return m;
        }

        /// <summary>2D 游戏最常用的投影：原点左上，Y 向下，1 单位 = 1 像素。</summary>
        public static Matrix4x4 CreateOrthographicScreen(float width, float height)
            => CreateOrthographicOffCenter(0f, width, height, 0f, 0f, 1f);

        public static Matrix4x4 CreateScale(float scaleX, float scaleY)
            => new() { M11 = scaleX, M22 = scaleY, M33 = 1f, M44 = 1f };

        public static Matrix4x4 CreateTranslation(float x, float y, float z = 0f)
            => new() { M11 = 1f, M22 = 1f, M33 = 1f, M44 = 1f, M14 = x, M24 = y, M34 = z };

        /// <summary>先缩放后平移，2D 屏幕适配（letterbox / 居中）最常用的组合。</summary>
        public static Matrix4x4 CreateScaleTranslation(float scaleX, float scaleY, float translateX, float translateY)
            => new()
            {
                M11 = scaleX, M22 = scaleY, M33 = 1f, M44 = 1f,
                M14 = translateX, M24 = translateY
            };

        /// <summary>三轴缩放（2D 场景下 z 传 1 即可）。</summary>
        public static Matrix4x4 CreateScale(float scaleX, float scaleY, float scaleZ)
            => new() { M11 = scaleX, M22 = scaleY, M33 = scaleZ, M44 = 1f };

        /// <summary>
        /// 绕 Z 轴旋转（2D 旋转）。sin 落在 M12、M21 = -sin，
        /// 因此旋转角可由 Atan2(M12, M11) 还原。
        /// </summary>
        public static Matrix4x4 CreateRotationZ(float radians)
        {
            float c = MathF.Cos(radians), s = MathF.Sin(radians);
            return new()
            {
                M11 = c, M12 = s,
                M21 = -s, M22 = c,
                M33 = 1f, M44 = 1f,
            };
        }

        /// <summary>
        /// 求逆。复用 System.Numerics 的实现以保证数值正确性；
        /// 奇异矩阵（不可逆）返回单位矩阵而不是 NaN，避免变换结果污染。
        /// </summary>
        public static Matrix4x4 Invert(Matrix4x4 m)
        {
            var source = new System.Numerics.Matrix4x4(
                m.M11, m.M12, m.M13, m.M14,
                m.M21, m.M22, m.M23, m.M24,
                m.M31, m.M32, m.M33, m.M34,
                m.M41, m.M42, m.M43, m.M44);

            if (!System.Numerics.Matrix4x4.Invert(source, out var inverse))
                return Identity;

            return new()
            {
                M11 = inverse.M11, M12 = inverse.M12, M13 = inverse.M13, M14 = inverse.M14,
                M21 = inverse.M21, M22 = inverse.M22, M23 = inverse.M23, M24 = inverse.M24,
                M31 = inverse.M31, M32 = inverse.M32, M33 = inverse.M33, M34 = inverse.M34,
                M41 = inverse.M41, M42 = inverse.M42, M43 = inverse.M43, M44 = inverse.M44,
            };
        }

        public static Matrix4x4 operator *(Matrix4x4 a, Matrix4x4 b) => new()
        {
            M11 = a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31 + a.M14 * b.M41,
            M21 = a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31 + a.M24 * b.M41,
            M31 = a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31 + a.M34 * b.M41,
            M41 = a.M41 * b.M11 + a.M42 * b.M21 + a.M43 * b.M31 + a.M44 * b.M41,

            M12 = a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32 + a.M14 * b.M42,
            M22 = a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32 + a.M24 * b.M42,
            M32 = a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32 + a.M34 * b.M42,
            M42 = a.M41 * b.M12 + a.M42 * b.M22 + a.M43 * b.M32 + a.M44 * b.M42,

            M13 = a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33 + a.M14 * b.M43,
            M23 = a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33 + a.M24 * b.M43,
            M33 = a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33 + a.M34 * b.M43,
            M43 = a.M41 * b.M13 + a.M42 * b.M23 + a.M43 * b.M33 + a.M44 * b.M43,

            M14 = a.M11 * b.M14 + a.M12 * b.M24 + a.M13 * b.M34 + a.M14 * b.M44,
            M24 = a.M21 * b.M14 + a.M22 * b.M24 + a.M23 * b.M34 + a.M24 * b.M44,
            M34 = a.M31 * b.M14 + a.M32 * b.M24 + a.M33 * b.M34 + a.M34 * b.M44,
            M44 = a.M41 * b.M14 + a.M42 * b.M24 + a.M43 * b.M34 + a.M44 * b.M44,
        };

        /// <summary>变换一个点：取第 1、2 行的前两列做线性变换，加上第 4 列作为平移。</summary>
        public Vector2 Transform(Vector2 p)
            => new(M11 * p.X + M12 * p.Y + M14, M21 * p.X + M22 * p.Y + M24);
    }
}
