namespace KFramework.MonoGame
{
    /// <summary>像素表面格式（照 MonoGame 的 SurfaceFormat，这里仅保留 2D 引擎实际用到的成员）。</summary>
    public enum SurfaceFormat
    {
        /// <summary>RGBA8，8 位每通道。</summary>
        Color,
    }

    /// <summary>
    /// 纹理资源的抽象基类（照 MonoGame 的 Texture）。
    /// 提供 Format/LevelCount 与排序用的 SortingKey（合批时按纹理分组的内在顺序键）。
    /// </summary>
    public abstract class Texture : GraphicsResource
    {
        /// <summary>纹理像素格式（照 MonoGame）。</summary>
        public SurfaceFormat Format { get; protected set; } = SurfaceFormat.Color;

        /// <summary>纹理 mip 层级数（照 MonoGame，WebGL 2D 后端暂只用 1 级）。</summary>
        public int LevelCount { get; protected set; } = 1;

        /// <summary>
        /// 合批排序用的唯一序号（照 MonoGame 的 Texture.SortingKey）：
        /// 每个纹理对象一个，自增分配，用作 Texture 排序模式下的稳定排序键。
        /// </summary>
        internal ulong _sortingKey;

        public ulong SortingKey => _sortingKey;
    }
}
