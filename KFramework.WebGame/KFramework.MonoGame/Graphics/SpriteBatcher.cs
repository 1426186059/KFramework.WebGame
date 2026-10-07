namespace KFramework.MonoGame
{
    /// <summary>
    /// 把待绘制精灵排队后送入 GPU：生成四边形顶点、按纹理分组，每组一次 draw call。
    /// 照 MonoGame 的 SpriteBatcher：
    ///  - SpriteBatchItem 以对象池复用（x1.5 增长、按 64 对齐），只重置计数，零每帧分配；
    ///  - 排序用一个 float SortKey + Array.Sort；
    ///  - 单次 draw 最多 MaxBatchSize 个四边形，超出自动分块（绕开 short 索引上限）。
    ///
    /// 分组与排序完全照官方 MonoGame：按纹理引用相等（ReferenceEquals）换批，Texture 排序模式
    /// 用 Texture.SortingKey。要让同一张图集页合批，必须用单个 Texture2D + source rect 绘制
    /// （KTexturePacker、SpriteFont 都是这种官方用法）；直接 LoadTexture 拿到的子图区域视图
    /// 各自是独立 Texture2D，按官方语义各自成批。
    /// </summary>
    internal sealed class SpriteBatcher
    {
        private const int InitialBatchSize = 256;

        private readonly GraphicsDevice _device;
        private SpriteBatchItem[] _batchItemList;
        private int _batchItemCount;
        private SamplerState _samplerState;

        private VertexPositionColorTexture[] _vertexArray;

        public SpriteBatcher(GraphicsDevice device, int capacity = 0)
        {
            _device = device;

            if (capacity <= 0) capacity = InitialBatchSize;
            else capacity = (capacity + 63) & (~63); // 对齐到 64

            _batchItemList = new SpriteBatchItem[capacity];
            for (int i = 0; i < capacity; i++) _batchItemList[i] = new SpriteBatchItem();

            EnsureArrayCapacity(capacity);
        }

        public void SetSamplerState(SamplerState samplerState) => _samplerState = samplerState;

        /// <summary>从对象池取一个可复用的 SpriteBatchItem；池满了就按 1.5 倍扩容。</summary>
        public SpriteBatchItem CreateBatchItem()
        {
            if (_batchItemCount >= _batchItemList.Length)
            {
                int oldSize = _batchItemList.Length;
                int newSize = (oldSize + oldSize / 2 + 63) & (~63);
                Array.Resize(ref _batchItemList, newSize);
                for (int i = oldSize; i < newSize; i++) _batchItemList[i] = new SpriteBatchItem();

                EnsureArrayCapacity(Math.Min(newSize, GraphicsDevice.MaxBatchSize));
            }
            return _batchItemList[_batchItemCount++];
        }

        private void EnsureArrayCapacity(int numBatchItems)
        {
            int needed = 4 * numBatchItems;
            if (_vertexArray != null && needed <= _vertexArray.Length) return;
            // 顶点缓冲按 MaxBatchSize 一块的容量准备，单次 draw 绝不会超出。
            _vertexArray = new VertexPositionColorTexture[Math.Max(needed, 4 * GraphicsDevice.MaxBatchSize)];
        }

        /// <summary>
        /// 排序 + 分批（按纹理 + 属性块），每批一次 draw call；总精灵数累加到 _metrics。
        /// <para>
        /// 分批键 = 纹理引用 + 属性块（引用 + 版本号）：一次 draw 只能有一份 uniform 值，
        /// 所以块一变（含"同一个块被改过"这种情况，版本号会变）就必须另起一批。
        /// 带块的绘制由 <see cref="SpriteBatch"/> 在 Draw 时当场提交（块是可变的，值必须当场生效），
        /// 因此这里看到的通常是「一段不带块 + 末尾一个带块项」。
        /// </para>
        /// </summary>
        public void DrawBatch(SpriteSortMode sortMode, Material material, Matrix4x4 transform)
        {
            if (_batchItemCount == 0) return;

            switch (sortMode)
            {
                case SpriteSortMode.Texture:
                case SpriteSortMode.FrontToBack:
                case SpriteSortMode.BackToFront:
                    Array.Sort(_batchItemList, 0, _batchItemCount);
                    break;
            }

            // 整批精灵总数（照 MonoGame：spriteCount 只在 DrawBatch 累加一次）。
            _device._metrics._spriteCount += _batchItemCount;

            int batchIndex = 0;
            int batchCount = _batchItemCount;
            int maxBatchSize = GraphicsDevice.MaxBatchSize;

            while (batchCount > 0)
            {
                int startIndex = 0;
                int index = 0;
                Texture2D? tex = null;
                ShaderPropertyBlock? block = null;
                int blockVersion = -1;
                bool batchStarted = false;

                int numBatchesToProcess = Math.Min(batchCount, maxBatchSize);

                for (int i = 0; i < numBatchesToProcess; i++, batchIndex++, index += 4)
                {
                    SpriteBatchItem item = _batchItemList[batchIndex];

                    // 换批：纹理变了（照 MonoGame 用引用相等判断），或属性块变了。
                    if (!batchStarted
                        || !ReferenceEquals(item.Texture, tex)
                        || !ReferenceEquals(item.Properties, block)
                        || item.BlockVersion != blockVersion)
                    {
                        // 先把上一段画掉（用的是上一段自己的状态），再为本段下发状态。
                        FlushVertexArray(startIndex, index);

                        tex = item.Texture;
                        block = item.Properties;
                        blockVersion = item.BlockVersion;
                        startIndex = index;
                        batchStarted = true;

                        // 每段都要下发：材质 + 属性块（块盖在材质之上）→ 采样状态 → 纹理。
                        _device.ApplyMaterial(material, transform, block);
                        _device.SetSamplerState(_samplerState);
                        _device.BindTexture(tex!);
                    }

                    _vertexArray[index] = item.vertexTL;
                    _vertexArray[index + 1] = item.vertexTR;
                    _vertexArray[index + 2] = item.vertexBL;
                    _vertexArray[index + 3] = item.vertexBR;

                    item.Texture = null;      // 释放引用，便于 GC
                    item.Properties = null;
                }

                FlushVertexArray(startIndex, index);
                batchCount -= numBatchesToProcess;
            }

            _batchItemCount = 0;
        }

        private void FlushVertexArray(int start, int end)
        {
            if (start == end) return;
            _device.DrawUserIndexedPrimitives(_vertexArray, start, end);
        }
    }
}
