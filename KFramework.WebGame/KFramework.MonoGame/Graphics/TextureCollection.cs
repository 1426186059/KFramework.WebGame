namespace KFramework.MonoGame
{
    /// <summary>
    /// 设备上的纹理槽集合（照 MonoGame 的 TextureCollection）。
    /// 索引对应纹理单元（本 2D 后端只用单元 0）。与官方一致地用索引器读写、Clear 清空。
    /// </summary>
    public sealed class TextureCollection
    {
        private readonly Texture?[] _textures;

        public int Count { get; }

        public TextureCollection(int count)
        {
            Count = count;
            _textures = new Texture?[count];
        }

        public Texture? this[int index]
        {
            get => _textures[index];
            set => _textures[index] = value;
        }

        public void Clear() => Array.Clear(_textures, 0, _textures.Length);
    }
}
