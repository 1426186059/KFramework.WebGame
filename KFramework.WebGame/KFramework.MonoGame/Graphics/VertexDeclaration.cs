namespace KFramework.MonoGame
{
    /// <summary>顶点元素的格式（照 MonoGame 的 VertexElementFormat，保留 2D 引擎相关成员）。</summary>
    public enum VertexElementFormat
    {
        Single,
        Vector2,
        Vector3,
        Vector4,
        Color,
        Byte4,
        Short2,
        Short4,
        NormalizedShort2,
        NormalizedShort4,
        HalfVector2,
        HalfVector4,
    }

    /// <summary>顶点元素的用途（照 MonoGame 的 VertexElementUsage）。</summary>
    public enum VertexElementUsage
    {
        Position,
        Color,
        TextureCoordinate,
        Normal,
        Binormal,
        Tangent,
        BlendIndices,
        BlendWeight,
        Depth,
        Sample,
        PointSize,
        TessellateFactor,
    }

    /// <summary>单个顶点元素的描述（照 MonoGame 的 VertexElement）。</summary>
    public struct VertexElement
    {
        public int Offset;
        public VertexElementFormat VertexElementFormat;
        public VertexElementUsage VertexElementUsage;
        public int UsageIndex;

        public VertexElement(int offset, VertexElementFormat format, VertexElementUsage usage, int usageIndex = 0)
        {
            Offset = offset;
            VertexElementFormat = format;
            VertexElementUsage = usage;
            UsageIndex = usageIndex;
        }
    }

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
