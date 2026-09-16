namespace KFramework.MonoGame
{
    /// <summary>
    /// 顶点布局声明（照 MonoGame 的 VertexDeclaration）。
    /// 描述一个顶点跨多少字节（VertexStride）以及各通道的偏移/格式/用途。
    /// 本 WebGL 2D 后端在 GraphicsDevice 内已硬编码属性布局，这里仅作为与官方一致的 API 类型。
    /// </summary>
    public class VertexDeclaration : GraphicsResource
    {
        private readonly VertexElement[] _elements;

        public int VertexStride { get; }

        public VertexDeclaration(int vertexStride, params VertexElement[] elements)
        {
            VertexStride = vertexStride;
            _elements = elements;
        }

        public VertexElement[] GetVertexElements() => _elements;
    }
}
