using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 行主序（row-major）4x4 矩阵，向量约定与 MonoGame / XNA 完全一致。
    ///
    /// <para>==================== 一、什么是行主序 / 列主序 ====================</para>
    /// 一个 4x4 矩阵在数学上就是 4 行 4 列的数表，但落到内存必须"摊平"成 16 个连续数字。
    /// 摊平的顺序只有两种，这就是"主序（major order）"：
    ///
    /// <para>【行主序 row-major】按【行】依次存放：先存第 1 行的 4 个数，再存第 2 行……</para>
    /// <code>
    /// 数学矩阵:             内存顺序:
    /// [ a11 a12 a13 a14 ]   a11 a12 a13 a14 | a21 a22 a23 a24 | a31 ... | a41 ...
    /// [ a21 a22 a23 a24 ]
    /// [ a31 a32 a33 a34 ]
    /// [ a41 a42 a43 a44 ]
    /// </code>
    /// 代表：C/C++ 二维数组、DirectX、MonoGame / XNA、System.Numerics.Matrix4x4
    ///
    /// <para>【列主序 column-major】按【列】依次存放：先存第 1 列的 4 个数，再存第 2 列……</para>
    /// <code>
    /// 数学矩阵:             内存顺序:
    /// [ a11 a12 a13 a14 ]   a11 a21 a31 a41 | a12 a22 a32 a42 | a13 ... | a14 ...
    /// [ a21 a22 a23 a24 ]
    /// [ a31 a32 a33 a34 ]
    /// [ a41 a42 a43 a44 ]
    /// </code>
    /// 代表：Fortran、OpenGL / GLSL、WebGL 的 uniformMatrix4fv
    ///
    /// <para>==================== 二、比主序更要紧的：向量约定 ====================</para>
    /// 主序只决定【内存怎么摆】，不改变矩阵的数学含义。
    /// 真正决定"平移放哪、乘法怎么写"的是配套的【向量约定】：
    ///
    /// <para>【行向量约定 row-vector】点写成行向量、左乘矩阵：p' = p × M</para>
    /// 平移位于矩阵的【第 4 行】：M41 / M42 / M43
    /// 组合变换时【先发生的写在左边】：M = M_first × M_second
    /// 代表：MonoGame / XNA、DirectX、System.Numerics
    ///
    /// <para>【列向量约定 column-vector】点写成列向量、右乘矩阵：v' = M × v</para>
    /// 平移位于矩阵的【第 4 列】：M14 / M24 / M34
    /// 组合变换时【先发生的写在右边】：M = M_second × M_first
    /// 代表：OpenGL / GLSL、WebGL shader、glMatrix
    ///
    /// 习惯上"行主序 + 行向量"配对（MonoGame 系）、"列主序 + 列向量"配对（OpenGL 系），
    /// 因为这样平移恰好落在内存里连续的 3 个数上。混用不会报错，但会静默算错——
    /// 比如按行向量习惯去取 M41 当平移，而矩阵实际是列向量的，取到的就是 0。
    ///
    /// <para>==================== 三、我们采取哪一种 ====================</para>
    /// 本引擎采取【行主序 + 行向量约定】，与 MonoGame / XNA 完全一致：
    /// 字段按行声明：<c>M11 M12 M13 M14</c> 是第 1 行，<c>M41 M42 M43 M44</c> 是第 4 行；
    /// 平移放在【第 4 行】的 <c>M41 / M42 / M43</c>（所以取世界坐标就是取 M41 / M42）；
    /// 变换点用行向量：<see cref="Transform"/> 计算 <c>p' = p × M</c>；
    /// 组合时【先发生的变换在左】：世界矩阵 = 子节点矩阵 × 父节点矩阵。
    ///
    /// <para>==================== 四、为什么选它 ====================</para>
    /// 1. 与 MonoGame / XNA 保持一致：本项目的 SpriteBatch、SpriteFont、各类 Draw 重载都是照
    ///    MonoGame 的用法写的，业务层的心智模型也是 MonoGame 的。矩阵沿用同一套约定，
    ///    可以避免"按 MonoGame 习惯写出来的代码实际是错的"这类极隐蔽的 bug
    ///    （典型如：习惯性用 M41/M42 取位置、习惯性写"子 × 父"）。
    /// 2. 与 System.Numerics 一致：<see cref="Invert"/> 直接复用 System.Numerics.Matrix4x4，
    ///    两边约定相同，字段原样对应即可，不需要任何转置往返，不易出错。
    /// 3. 上传 WebGL 的代价极小：WebGL 的 uniformMatrix4fv 要求【列主序】且 transpose 必须为
    ///    false。行主序矩阵需要转置一次——但转置只是 16 个 float 的排布，而且行主序内存
    ///    【按原样发出去】恰好就是"转置矩阵的列主序"，GLSL 里 v' = Mᵀ × v 与行向量 p' = p × M
    ///    完全等价，所以实际是零成本直发，每帧也只有一次矩阵上传。
    ///    见 <c>SpriteEffect.WriteMatrix</c>。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Matrix4x4
    {
        /// <summary>第 1 行。</summary>
        public float M11, M12, M13, M14;
        /// <summary>第 2 行。</summary>
        public float M21, M22, M23, M24;
        /// <summary>第 3 行。</summary>
        public float M31, M32, M33, M34;
        /// <summary>第 4 行。行向量约定下，平移就存放在这一行的 M41 / M42 / M43。</summary>
        public float M41, M42, M43, M44;

        public static Matrix4x4 Identity => new()
        {
            M11 = 1f, M22 = 1f, M33 = 1f, M44 = 1f
        };

        /// <summary>
        /// 2D 正交投影：屏幕坐标 (0,0)-(width,height)，Y 轴向下，(0,0) 在左上角。
        /// 行向量约定：平移位于第 4 行（M41 / M42 / M43）。
        /// </summary>
        public static Matrix4x4 CreateOrthographicOffCenter(float left, float right, float bottom, float top, float zNear, float zFar)
        {
            Matrix4x4 m = Identity;
            m.M11 = 2f / (right - left);
            m.M22 = 2f / (top - bottom);
            m.M33 = -2f / (zFar - zNear);
            // 行主序 + 行向量：平移位于第 4 行（不是第 4 列）
            m.M41 = -(right + left) / (right - left);
            m.M42 = -(top + bottom) / (top - bottom);
            m.M43 = -(zFar + zNear) / (zFar - zNear);
            return m;
        }

        /// <summary>2D 游戏最常用的投影：原点左上，Y 向下，1 单位 = 1 像素。</summary>
        public static Matrix4x4 CreateOrthographicScreen(float width, float height)
            => CreateOrthographicOffCenter(0f, width, height, 0f, 0f, 1f);

        public static Matrix4x4 CreateScale(float scaleX, float scaleY)
            => new() { M11 = scaleX, M22 = scaleY, M33 = 1f, M44 = 1f };

        /// <summary>
        /// 平移矩阵。行向量约定：平移放在第 4 行 M41 / M42 / M43（与 MonoGame 一致）。
        /// </summary>
        public static Matrix4x4 CreateTranslation(float x, float y, float z = 0f)
            => new() { M11 = 1f, M22 = 1f, M33 = 1f, M44 = 1f, M41 = x, M42 = y, M43 = z };

        /// <summary>
        /// 先缩放后平移（等价于 CreateScale × CreateTranslation），2D 屏幕适配（letterbox / 居中）最常用。
        /// 行向量约定下平移同样落在第 4 行。
        /// </summary>
        public static Matrix4x4 CreateScaleTranslation(float scaleX, float scaleY, float translateX, float translateY)
            => new()
            {
                M11 = scaleX, M22 = scaleY, M33 = 1f, M44 = 1f,
                M41 = translateX, M42 = translateY
            };

        /// <summary>三轴缩放（2D 场景下 z 传 1 即可）。</summary>
        public static Matrix4x4 CreateScale(float scaleX, float scaleY, float scaleZ)
            => new() { M11 = scaleX, M22 = scaleY, M33 = scaleZ, M44 = 1f };

        /// <summary>
        /// 绕 Z 轴旋转（2D 旋转）。行向量约定下第 1 行是 (c, s)、第 2 行是 (-s, c)，
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
        /// System.Numerics.Matrix4x4 同为行主序 + 行向量，字段可原样对应，无需转置。
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

        /// <summary>
        /// 标准矩阵乘法 C = A × B（C[r][c] = Σ A[r][k] · B[k][c]）。
        /// 行向量约定下意为"先应用 A、再应用 B"，因此组合父子变换时应写作"子 × 父"。
        /// </summary>
        public static Matrix4x4 operator *(Matrix4x4 a, Matrix4x4 b) => new()
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

        /// <summary>
        /// 变换一个点。行向量约定：p' = p × M（与 MonoGame 的 Vector2.Transform 一致）。
        /// 即取第 1 列 / 第 2 列与点做线性组合，再加第 4 行的平移 M41 / M42。
        /// </summary>
        public Vector2 Transform(Vector2 p)
            => new(p.X * M11 + p.Y * M21 + M41, p.X * M12 + p.Y * M22 + M42);
    }
}
