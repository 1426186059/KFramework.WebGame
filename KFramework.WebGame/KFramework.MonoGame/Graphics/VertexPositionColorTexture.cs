using System.Runtime.InteropServices;

namespace KFramework.MonoGame
{
    /// <summary>
    /// SpriteBatch 的顶点：位置(vec2) + 纹理坐标(vec2) + 颜色(rgba8) + 逐精灵参数(8×u8)，共 28 字节。
    /// <para>
    /// 布局：offset 0/8 是浮点（位置、UV），16 是颜色（unorm8x4），20/24 是逐精灵参数（两组 unorm8x4）。
    /// 参数通道的存在，使"一批内每个精灵外观不同"不必依赖 MaterialPropertyBlock —— 后者挂在每一次
    /// 绘制上、按块切分 DrawCall，而顶点数据不参与分批键。
    /// </para>
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VertexPositionColorTexture
    {
        public Vector2 Position;
        public Vector2 TexCoord;
        public Color Color;

        /// <summary>逐精灵参数（8 通道 × 1 字节），语义完全由着色器决定，引擎不做解释。</summary>
        public SpriteParams Params;

        public const int SizeInBytes = 28;

        public VertexPositionColorTexture(Vector2 position, Vector2 texCoord, Color color)
            : this(position, texCoord, color, default) { }

        public VertexPositionColorTexture(Vector2 position, Vector2 texCoord, Color color, SpriteParams parameters)
        {
            Position = position;
            TexCoord = texCoord;
            Color = color;
            Params = parameters;
        }
    }
}
