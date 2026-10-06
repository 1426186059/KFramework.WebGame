using KFramework.MonoGame;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 单个待绘制精灵。照 MonoGame 的 SpriteBatchItem：
    ///  - 顶点在【提交期】就算好（item.Set），Flush 时只做内存拷贝，降低 flush 开销；
    ///  - 用一个 float SortKey 参与排序，并实现 IComparable 让 Array.Sort 直接排；
    ///  - 由 SpriteBatcher 以对象池方式复用，每帧零分配。
    /// 顶点只存 vec2 位置（2D 无深度缓冲），与 KFramework 的 VertexPositionColorTexture 一致。
    /// </summary>
    internal sealed class SpriteBatchItem : IComparable<SpriteBatchItem>
    {
        public Texture2D Texture;
        public float SortKey;

        /// <summary>这一次绘制要覆盖的材质属性块（可空；见 <see cref="SpriteBatch.Draw"/> 的 block 重载）。</summary>
        public MaterialPropertyBlock? Properties;

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

        /// <summary>轴对齐（无旋转）：位置即 (x, y)，尺寸 (w, h)。<paramref name="parameters"/> 逐精灵参数写进 4 个顶点（同值，插值后整片相同）。</summary>
        public void Set(float x, float y, float w, float h, Color color, Vector2 texCoordTL, Vector2 texCoordBR, float depth, SpriteParams parameters = default)
        {
            vertexTL.Position = new Vector2(x, y);
            vertexTL.Color = color;
            vertexTL.TexCoord = texCoordTL;
            vertexTL.Params = parameters;

            vertexTR.Position = new Vector2(x + w, y);
            vertexTR.Color = color;
            vertexTR.TexCoord = new Vector2(texCoordBR.X, texCoordTL.Y);
            vertexTR.Params = parameters;

            vertexBL.Position = new Vector2(x, y + h);
            vertexBL.Color = color;
            vertexBL.TexCoord = new Vector2(texCoordTL.X, texCoordBR.Y);
            vertexBL.Params = parameters;

            vertexBR.Position = new Vector2(x + w, y + h);
            vertexBR.Color = color;
            vertexBR.TexCoord = texCoordBR;
            vertexBR.Params = parameters;
        }

        /// <summary>带旋转：以 (x, y) 为锚点，dx/dy 为相对锚点的偏移（通常 dx=-origin.X, dy=-origin.Y）。</summary>
        public void Set(float x, float y, float dx, float dy, float w, float h, float sin, float cos, Color color, Vector2 texCoordTL, Vector2 texCoordBR, float depth, SpriteParams parameters = default)
        {
            vertexTL.Position = new Vector2(x + dx * cos - dy * sin, y + dx * sin + dy * cos);
            vertexTL.Color = color;
            vertexTL.TexCoord = texCoordTL;
            vertexTL.Params = parameters;

            vertexTR.Position = new Vector2(x + (dx + w) * cos - dy * sin, y + (dx + w) * sin + dy * cos);
            vertexTR.Color = color;
            vertexTR.TexCoord = new Vector2(texCoordBR.X, texCoordTL.Y);
            vertexTR.Params = parameters;

            vertexBL.Position = new Vector2(x + dx * cos - (dy + h) * sin, y + dx * sin + (dy + h) * cos);
            vertexBL.Color = color;
            vertexBL.TexCoord = new Vector2(texCoordTL.X, texCoordBR.Y);
            vertexBL.Params = parameters;

            vertexBR.Position = new Vector2(x + (dx + w) * cos - (dy + h) * sin, y + (dx + w) * sin + (dy + h) * cos);
            vertexBR.Color = color;
            vertexBR.TexCoord = texCoordBR;
            vertexBR.Params = parameters;
        }

        public int CompareTo(SpriteBatchItem other) => SortKey.CompareTo(other.SortKey);
    }
}
