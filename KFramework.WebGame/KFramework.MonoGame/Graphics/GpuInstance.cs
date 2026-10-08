using System.Numerics;
using System.Runtime.InteropServices;

namespace KFramework.MonoGame
{
    /// <summary>
    /// GPU 实例化（Instancing）的逐实例数据，116 字节。字段与 Unity 的实例化数据对齐：
    /// <see cref="ObjectToWorld"/> 就是 Unity 的 <c>unity_ObjectToWorld</c>（对象→世界变换矩阵），
    /// 其余是 2D 精灵特有的附加数据（颜色 / UV 矩形 / 两个逐实例属性槽）。
    /// <para>
    /// 布局与 <see cref="WebGL_ShaderProgram_2D_Instanced"/> 的顶点属性一一对应（<c>vertexAttribDivisor = 1</c>）：
    /// ObjectToWorld float32x4 ×4（偏移 0 / 16 / 32 / 48，占 4 个属性槽）
    /// → Tint unorm8x4（偏移 64）
    /// → UvRect float32x4（偏移 68：xy = UV 起点、zw = UV 尺寸，均归一化）
    /// → Inst0 / Inst1 两个 float32x4（偏移 84 / 100：逐实例属性槽，着色器里 <c>aInst0/aInst1</c> → <c>vInst0/vInst1</c>）。
    /// </para>
    /// <para>
    /// <b>矩阵约定</b>：<see cref="ObjectToWorld"/> 是<b>行主序 + 行向量</b>（与本引擎 / MonoGame 一致，p' = p × M），
    /// 它把<b>单位四边形 (0,0)-(1,1)</b> 变换到屏幕；顶点着色器里写
    /// <c>gl_Position = uProjection * (aObjectToWorld * vec4(顶点, 0, 1))</c>。
    /// 内存按行主序直发、GLSL 按列读，两者恰好等价（见 <see cref="Matrix4x4"/> 的说明）。
    /// 矩阵带来的好处与 Unity 一样：位置 / 尺寸 / 旋转 / 斜切 / 任意仿射都能表达，不必再为每种变换加字段。
    /// </para>
    /// <para>
    /// <see cref="Inst0"/> / <see cref="Inst1"/> 是"每个实例各自持有自己的属性值"的落点：由
    /// <see cref="Material.SetGpuInstanceChannels"/> 声明哪些着色器属性进这 8 个槽
    /// （照 Unity 的实例化属性 <c>UNITY_INSTANCING_BUFFER</c>），值由
    /// <see cref="GpuInstanceBatch.Add(in Matrix4x4, Color, ShaderPropertyBlock?)"/> 从属性块编码进来 ——
    /// 逐精灵不同的属性值随实例缓冲走、不占 uniform，整批仍然只有一次 DrawCall。
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

        /// <summary>逐实例属性槽 0（着色器里 <c>aInst0</c> → <c>vInst0</c>）：内容由 <see cref="Material.SetGpuInstanceChannels"/> 声明。</summary>
        public Vector4 Inst0;

        /// <summary>逐实例属性槽 1（着色器里 <c>aInst1</c> → <c>vInst1</c>）：内容由 <see cref="Material.SetGpuInstanceChannels"/> 声明。</summary>
        public Vector4 Inst1;

        /// <summary>逐实例属性槽个数（两个 vec4）。</summary>
        public const int InstanceChannelCount = 8;

        public const int SizeInBytes = 116;

        /// <summary>整幅纹理的 UV 矩形（0,0,1,1）。</summary>
        public static readonly Vector4 WholeTextureUv = new(0f, 0f, 1f, 1f);

        public GpuInstance(in Matrix4x4 objectToWorld, Color tint)
            : this(objectToWorld, tint, WholeTextureUv) { }

        public GpuInstance(in Matrix4x4 objectToWorld, Color tint, Vector4 uvRect)
        {
            ObjectToWorld = objectToWorld;
            Tint = tint;
            UvRect = uvRect;
            Inst0 = Vector4.Zero;
            Inst1 = Vector4.Zero;
        }

        /// <summary>
        /// 便捷构造「对象→世界」矩阵：等价于 Unity 的 <c>Matrix4x4.TRS(position, rotation, scale)</c>
        /// 再加上一步"单位四边形居中"（Unity 的四边形网格是居中的，我们的是 (0,0)-(1,1)）。
        /// <para>
        /// 顺序（行向量约定：先发生的写在左边）：
        /// 平移 -0.5 居中 → 按 <paramref name="size"/> 缩放 → 绕中心旋转 <paramref name="rotation"/> 弧度 → 平移到 <paramref name="center"/>。
        /// </para>
        /// <para>
        /// 实现是上面那条链的<b>展开式</b>：2D 仿射只有 6 个非平凡分量（M11/M12/M21/M22/M41/M42），
        /// 一次三角函数 + 约 10 次浮点运算即可，不必做三次通用 4×4 乘法
        /// （逐实例路径每帧要算 N 次，这是热路径；通用乘法每次 112 次运算外加 64 字节中间结果拷贝）。
        /// </para>
        /// </summary>
        public static Matrix4x4 CreateObjectToWorld(Vector2 center, Vector2 size, float rotation)
        {
            float c = MathF.Cos(rotation), s = MathF.Sin(rotation);
            float w = size.X, h = size.Y;

            // p' = p × [居中平移 × 缩放 × 旋转 × 平移到中心] 展开后：
            //   [ w·c   w·s  0  0 ]        x' = x·w·c − y·h·s + M41
            //   [ −h·s  h·c  0  0 ]   →
            //   [  0     0   1  0 ]        M41 = cx − ½(w·c − h·s)，M42 = cy − ½(w·s + h·c)
            //   [ M41   M42  0  1 ]        即"居中平移"也被旋转带偏后的结果
            return new Matrix4x4
            {
                M11 = w * c, M12 = w * s,
                M21 = -h * s, M22 = h * c,
                M33 = 1f,
                M41 = center.X - 0.5f * (w * c - h * s),
                M42 = center.Y - 0.5f * (w * s + h * c),
                M44 = 1f,
            };
        }
    }
}
