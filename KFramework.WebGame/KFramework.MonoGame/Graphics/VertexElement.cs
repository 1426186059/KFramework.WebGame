namespace KFramework.MonoGame
{
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
}
