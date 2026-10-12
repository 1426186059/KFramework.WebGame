using System.Numerics;
using System.Runtime.InteropServices;

namespace KFramework.MonoGame
{
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
