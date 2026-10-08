using System.Numerics;
using System.Runtime.InteropServices;

namespace KFramework.MonoGame
{
    /// <summary>
    /// GPU 实例化（Instancing）的逐实例数据，84 字节。字段与 Unity 的实例化数据对齐：
    /// <see cref="ObjectToWorld"/> 就是 Unity 的 <c>unity_ObjectToWorld</c>（对象→世界变换矩阵），
    /// 其余是 2D 精灵特有的附加数据（颜色 / UV 矩形）。
    /// <para>
    /// 布局与 <see cref="WebGL_ShaderProgram_2D_Instanced"/> 的顶点属性一一对应（<c>vertexAttribDivisor = 1</c>）：
    /// ObjectToWorld float32x4 ×4（偏移 0 / 16 / 32 / 48，占 4 个属性槽）
    /// → Tint unorm8x4（偏移 64）
    /// → UvRect float32x4（偏移 68：xy = UV 起点、zw = UV 尺寸，均归一化）。
    /// </para>
    /// <para>
    /// <b>矩阵约定</b>：<see cref="ObjectToWorld"/> 是<b>行主序 + 行向量</b>（与本引擎 / MonoGame 一致，p' = p × M），
    /// 它把<b>单位四边形 (0,0)-(1,1)</b> 变换到屏幕；顶点着色器里写
    /// <c>gl_Position = uProjection * (aObjectToWorld * vec4(顶点, 0, 1))</c>。
    /// 内存按行主序直发、GLSL 按列读，两者恰好等价（见 <see cref="Matrix4x4"/> 的说明）。
    /// 矩阵带来的好处与 Unity 一样：位置 / 尺寸 / 旋转 / 斜切 / 任意仿射都能表达，不必再为每种变换加字段。
    /// </para>
    /// <para>
    /// 与 Unity 的差异（有意为之）：Unity 的内置实例化缓冲还带 <c>unity_WorldToObject</c>（法线 / 光照用）。
    /// 2D 精灵没有光照与法线，故这里不带它 —— 带上等于每个实例白多 64 字节。
    /// </para>
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct GpuInstance
    {
        /// <summary>对象→世界变换矩阵（照 Unity 的 <c>unity_ObjectToWorld</c>）：把单位四边形 (0,0)-(1,1) 变换到屏幕。</summary>
        public Matrix4x4 ObjectToWorld;

        /// <summary>与纹理相乘的逐实例颜色。</summary>
        public Color Tint;

        /// <summary>
        /// 逐实例 UV 矩形：xy = 起点、zw = 尺寸（都归一化到 0~1）。
        /// <para>
        /// 有了它，一次实例化 draw 里每个实例都能取纹理的不同子区域（图集切图、字体图集），
        /// 而不是被迫"一个 batch 只能整幅纹理"。做翻转（FlipX/FlipY）时让尺寸为负即可，
        /// 与逐顶点路径的 uvTL/uvBR 语义一致。
        /// </para>
        /// </summary>
        public Vector4 UvRect;

        public const int SizeInBytes = 84;

        /// <summary>整幅纹理的 UV 矩形（0,0,1,1）。</summary>
        public static readonly Vector4 WholeTextureUv = new(0f, 0f, 1f, 1f);

        public GpuInstance(in Matrix4x4 objectToWorld, Color tint)
            : this(objectToWorld, tint, WholeTextureUv) { }

        public GpuInstance(in Matrix4x4 objectToWorld, Color tint, Vector4 uvRect)
        {
            ObjectToWorld = objectToWorld;
            Tint = tint;
            UvRect = uvRect;
        }

        /// <summary>
        /// 便捷构造「对象→世界」矩阵（按中心点）：等价于 <see cref="CreateObjectToWorld(Vector2, Vector2, Vector2, float)"/>
        /// 取 <paramref name="origin"/> = 半个尺寸 —— 即"单位四边形居中到 <paramref name="center"/>、按 size 缩放、绕中心旋转"。
        /// </summary>
        public static Matrix4x4 CreateObjectToWorld(Vector2 center, Vector2 size, float rotation)
            => CreateObjectToWorld(center, new Vector2(size.X * 0.5f, size.Y * 0.5f), size, rotation);

        /// <summary>
        /// 通用「对象→世界」矩阵：<b>语义与 <see cref="SpriteBatch.Draw(Texture2D, Vector2, Rectangle?, Color, float, Vector2, Vector2, SpriteEffects, float, ShaderPropertyBlock?)"/> 完全一致</b>
        /// （照 MonoGame 的 origin / scale 约定），所以三个批处理之间换用不需要改调用代码。
        /// <para>
        /// 行向量约定下的等价变换链：<c>按 size 缩放 → 平移 -origin → 绕 (0,0) 旋转 → 平移到 position</c>，
        /// 即 <c>p' = position + R(rotation) · (p ⊙ size − origin)</c>，其中 p 是单位四边形 (0,0)-(1,1) 上的点。
        /// </para>
        /// <para>
        /// 注意 <paramref name="origin"/> 与 <see cref="SpriteBatch"/> 一样是<b>已乘过 scale</b> 的像素值（调用方乘好）。
        /// </para>
        /// <para>
        /// 实现是展开式：2D 仿射只有 6 个非平凡分量（M11/M12/M21/M22/M41/M42），一次三角函数 + 约 10 次浮点运算，
        /// 不必做三次通用 4×4 乘法（逐物体路径每帧要算 N 次，通用乘法每次 112 次运算外加 64 字节中间结果拷贝）。
        /// </para>
        /// </summary>
        public static Matrix4x4 CreateObjectToWorld(Vector2 position, Vector2 origin, Vector2 size, float rotation)
        {
            float c = MathF.Cos(rotation), s = MathF.Sin(rotation);
            float w = size.X, h = size.Y;

            // [  w·c   w·s  0  0 ]     x' = x·w·c − y·h·s + M41
            // [ −h·s   h·c  0  0 ]  →  y' = x·w·s + y·h·c + M42
            // [   0     0   1  0 ]     M41 = px − ox·c + oy·s
            // [  M41   M42  0  1 ]     M42 = py − ox·s − oy·c
            return new Matrix4x4
            {
                M11 = w * c, M12 = w * s,
                M21 = -h * s, M22 = h * c,
                M33 = 1f,
                M41 = position.X - origin.X * c + origin.Y * s,
                M42 = position.Y - origin.X * s - origin.Y * c,
                M44 = 1f,
            };
        }
    }
}
