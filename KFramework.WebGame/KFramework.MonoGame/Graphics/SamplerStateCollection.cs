namespace KFramework.MonoGame
{
    /// <summary>
    /// 设备上的采样器状态槽集合（照 MonoGame 的 SamplerStateCollection）。
    /// 索引对应纹理单元（本 2D 后端只用单元 0）。与官方一致地用索引器读写、Clear 清空。
    /// </summary>
    public sealed class SamplerStateCollection
    {
        private readonly SamplerState?[] _states;

        public int Count { get; }

        public SamplerStateCollection(int count)
        {
            Count = count;
            _states = new SamplerState?[count];
        }

        public SamplerState? this[int index]
        {
            get => _states[index];
            set => _states[index] = value;
        }

        public void Clear() => Array.Clear(_states, 0, _states.Length);
    }
}
