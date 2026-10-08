using System.Numerics;
using KFramework.MonoGame;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 单个待绘制精灵。照 MonoGame 的 SpriteBatchItem：
    ///  - 顶点在【提交期】就算好（item.Set），Flush 时只做内存拷贝，降低 flush 开销；
    ///  - 用一个 float SortKey 参与排序，并实现 IComparable 让 Array.Sort 直接排；
    ///  - 由 <see cref="SpriteBatch"/> 以对象池方式复用，每帧零分配。
    /// 顶点位置是 float4（x, y, 0, 1，w 固定 1），与 Unity appdata_t 的 <c>float4 vertex : POSITION</c> 一致。
    /// </summary>
    internal sealed class SpriteBatchItem : IComparable<SpriteBatchItem>
    {
        public Texture2D Texture;
        public float SortKey;

        /// <summary>这一次绘制要覆盖的着色器属性块（可空；见 <see cref="SpriteBatch.Draw"/> 的 block 重载）。</summary>
        public ShaderPropertyBlock? Properties;

        /// <summary>取块值那一刻的块版本号：块被改过（版本变了）就必须另起一批，否则会用到过期的 uniform 值。</summary>
        public int BlockVersion = -1;

        public VertexPositionColorTexture vertexTL;
        public VertexPositionColorTexture vertexTR;
        public VertexPositionColorTexture vertexBL;
        public VertexPositionColorTexture vertexBR;

        public SpriteBatchItem()
        {
            vertexTL = new VertexPositionColorTexture();
            vertexTR = new VertexPositionColorTexture();
            vertexBL = new VertexPositionColorTexture();
            vertexBR = new VertexPositionColorTexture();
        }

        /// <summary>轴对齐（无旋转）：位置即 (x, y)，尺寸 (w, h)。</summary>
        public void Set(float x, float y, float w, float h, Color color, Vector2 texCoordTL, Vector2 texCoordBR, float depth)
        {
            vertexTL.Position = new Vector4(x, y, 0f, 1f);
            vertexTL.Color = color;
            vertexTL.TexCoord = texCoordTL;

            vertexTR.Position = new Vector4(x + w, y, 0f, 1f);
            vertexTR.Color = color;
            vertexTR.TexCoord = new Vector2(texCoordBR.X, texCoordTL.Y);

            vertexBL.Position = new Vector4(x, y + h, 0f, 1f);
            vertexBL.Color = color;
            vertexBL.TexCoord = new Vector2(texCoordTL.X, texCoordBR.Y);

            vertexBR.Position = new Vector4(x + w, y + h, 0f, 1f);
            vertexBR.Color = color;
            vertexBR.TexCoord = texCoordBR;
        }

        /// <summary>带旋转：以 (x, y) 为锚点，dx/dy 为相对锚点的偏移（通常 dx=-origin.X, dy=-origin.Y）。</summary>
        public void Set(float x, float y, float dx, float dy, float w, float h, float sin, float cos, Color color, Vector2 texCoordTL, Vector2 texCoordBR, float depth)
        {
            vertexTL.Position = new Vector4(x + dx * cos - dy * sin, y + dx * sin + dy * cos, 0f, 1f);
            vertexTL.Color = color;
            vertexTL.TexCoord = texCoordTL;

            vertexTR.Position = new Vector4(x + (dx + w) * cos - dy * sin, y + (dx + w) * sin + dy * cos, 0f, 1f);
            vertexTR.Color = color;
            vertexTR.TexCoord = new Vector2(texCoordBR.X, texCoordTL.Y);

            vertexBL.Position = new Vector4(x + dx * cos - (dy + h) * sin, y + dx * sin + (dy + h) * cos, 0f, 1f);
            vertexBL.Color = color;
            vertexBL.TexCoord = new Vector2(texCoordTL.X, texCoordBR.Y);

            vertexBR.Position = new Vector4(x + (dx + w) * cos - (dy + h) * sin, y + (dx + w) * sin + (dy + h) * cos, 0f, 1f);
            vertexBR.Color = color;
            vertexBR.TexCoord = texCoordBR;
        }

        public int CompareTo(SpriteBatchItem other) => SortKey.CompareTo(other.SortKey);
    }
}
