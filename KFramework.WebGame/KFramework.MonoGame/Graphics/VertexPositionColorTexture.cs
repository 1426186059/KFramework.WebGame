using System.Runtime.InteropServices;

namespace KFramework.MonoGame
{
    /// <summary>SpriteBatch 的顶点：位置(vec2) + 纹理坐标(vec2) + 颜色(rgba8)，共 20 字节。</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VertexPositionColorTexture
    {
        public Vector2 Position;
        public Vector2 TexCoord;
        public Color Color;

        public const int SizeInBytes = 20;

        public VertexPositionColorTexture(Vector2 position, Vector2 texCoord, Color color)
        {
            Position = position;
            TexCoord = texCoord;
            Color = color;
        }
    }
}
