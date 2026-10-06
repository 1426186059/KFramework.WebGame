using System.Numerics;
using System.Runtime.InteropServices;

namespace KFramework.MonoGame
{
    /// <summary>
    /// GPU 实例化（Instancing）的逐实例数据，48 字节。
    /// <para>
    /// 布局与 <see cref="WebGlInstancedSpriteProgram"/> 的顶点属性一一对应：
    /// Rect float32x4（xy = 中心点，zw = 尺寸）→ Rotation float32（弧度）
    /// → Tint unorm8x4 → Params 两组 unorm8x4（与 <see cref="SpriteParams"/> 的 8 个通道同义）
    /// → UvRect float32x4（xy = UV 起点，zw = UV 尺寸，均已归一化）。
    /// </para>
    /// <para>
    /// 与逐顶点的区别：这里一份数据对应【一个实例】，绘制时由顶点着色器把单位四边形
    /// 按 Rect/Rotation 变换到世界位置——所以 CPU 侧每实例只写 48 字节，且 N 个实例一次 draw。
    /// </para>
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SpriteInstance
    {
        /// <summary>xy = 中心点（屏幕坐标），zw = 尺寸（像素）。</summary>
        public Vector4 Rect;

        /// <summary>绕中心的旋转弧度。</summary>
        public float Rotation;

        /// <summary>与纹理相乘的逐实例颜色。</summary>
        public Color Tint;

        /// <summary>逐实例参数（8 通道，语义由着色器决定）。</summary>
        public SpriteParams Params;

        /// <summary>
        /// 逐实例 UV 矩形：xy = 起点、zw = 尺寸（都归一化到 0~1）。
        /// <para>
        /// 有了它，一次实例化 draw 里每个实例都能取纹理的不同子区域（图集切图、字体图集），
        /// 而不是被迫"一个 batch 只能整幅纹理"。做翻转（FlipX/FlipY）时让尺寸为负即可，
        /// 与逐顶点路径的 uvTL/uvBR 语义一致。
        /// </para>
        /// </summary>
        public Vector4 UvRect;

        public const int SizeInBytes = 48;

        /// <summary>整幅纹理的 UV 矩形（0,0,1,1）。</summary>
        public static readonly Vector4 WholeTextureUv = new(0f, 0f, 1f, 1f);

        public SpriteInstance(Vector2 center, Vector2 size, float rotation, Color tint, SpriteParams parameters)
            : this(center, size, rotation, tint, parameters, WholeTextureUv) { }

        public SpriteInstance(Vector2 center, Vector2 size, float rotation, Color tint, SpriteParams parameters, Vector4 uvRect)
        {
            Rect = new Vector4(center.X, center.Y, size.X, size.Y);
            Rotation = rotation;
            Tint = tint;
            Params = parameters;
            UvRect = uvRect;
        }
    }
}
