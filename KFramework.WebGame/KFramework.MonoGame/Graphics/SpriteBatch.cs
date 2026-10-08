namespace KFramework.MonoGame
{
    /// <summary>
    /// CPU 合批的精灵绘制器（本引擎的主力 API）：把待绘制的精灵排队 → 排序 → 按纹理/属性块分批，
    /// 每批一次 draw call（几何在 CPU 侧展开成四边形顶点）。
    /// <para>
    /// 原来的 <c>SpriteBatcher</c> 已并入本类（它的职责只有"给 SpriteBatch 排队与提交"这一件事）：
    /// <list type="bullet">
    ///   <item><description><see cref="SpriteBatchItem"/> 以对象池复用（1.5 倍增长、按 64 对齐），只重置计数，零每帧分配；</description></item>
    ///   <item><description>排序用 float SortKey + <c>Array.Sort</c>；</description></item>
    ///   <item><description>单次 draw 最多 <see cref="GraphicsDevice.MaxBatchSize"/> 个四边形，超出自动分块（绕开 short 索引上限）。</description></item>
    /// </list>
    /// 分组与排序完全照官方 MonoGame：按纹理引用相等（<c>ReferenceEquals</c>）换批，Texture 排序模式用
    /// <see cref="Texture2D.SortingKey"/>。要让同一张图集页合批，必须用单个 <see cref="Texture2D"/> + source rect 绘制
    /// （KTexturePacker、SpriteFont 都是这种官方用法）；直接 LoadTexture 拿到的子图区域视图各自是独立
    /// <see cref="Texture2D"/>，按官方语义各自成批。
    /// </para>
    /// <para>
    /// 本类只做 CPU 合批（逐顶点路径）。另外两条路各有自己的类：
    /// <see cref="GpuInstanceBatch"/>（GPU 实例化，几何只有一份单位四边形）与
    /// <see cref="UrpBatch"/>（SRP-Batcher 式 UBO，DrawCall 不降但换物体极便宜）。
    /// </para>
    /// </summary>
    public sealed class SpriteBatch
    {
        /// <summary>对象池初始容量。</summary>
        private const int InitialBatchSize = 256;

        private readonly GraphicsDevice _device;

        private SpriteSortMode _sortMode;
        private Matrix4x4 _transform = Matrix4x4.Identity;
        private Matrix4x4 _projection;
        private Material _material = null!;
        private readonly Material _cache_Mat = new Material();

        private bool _beginCalled;

        // ---- 对象池 + 顶点数组（原 SpriteBatcher 的成员）----

        /// <summary>可复用的绘制项池（1.5 倍增长、按 64 对齐）；下标 &lt; <see cref="_batchItemCount"/> 的项属于本批。</summary>
        private SpriteBatchItem[] _batchItemList;

        /// <summary>本批已排队的绘制项数（提交后清零）。</summary>
        private int _batchItemCount;

        /// <summary>本批的「世界 → 屏幕」矩阵（= 相机矩阵 × 投影）：批内不变，开批时算一次。</summary>
        private Matrix4x4 _transformProjection;

        /// <summary>设备上此刻是否挂着某个属性块的 uniform 值（为 true 时，遇不带块的段要先把材质默认值发回去）。</summary>
        private bool _blockApplied;

        /// <summary>逐顶点数组（提交前把绘制项展开成四边形写在这里，再整段上传）。</summary>
        private VertexPositionColorTexture[] _vertexArray;

        public SpriteBatch(GraphicsDevice device)
        {
            ArgumentNullException.ThrowIfNull(device);
            _device = device;

            _batchItemList = new SpriteBatchItem[InitialBatchSize];
            for (int i = 0; i < InitialBatchSize; i++)
            {
                _batchItemList[i] = new SpriteBatchItem();
            }
            EnsureVertexArrayCapacity(InitialBatchSize);
        }

        public GraphicsDevice GraphicsDevice => _device;

        //这是本引擎的主力API，允许直接传入材质（Material）对象。
        //本 API 只做 CPU 合批（逐顶点路径）；GPU 实例化请用 GpuInstanceBatch（另一条独立的路）。
        public void Begin(Material material, SpriteSortMode sortMode = SpriteSortMode.Deferred,
                          Matrix4x4? transformMatrix = null)
        {
            BeginInternal(material, sortMode, transformMatrix);
        }

        //这是老API，后续不再推荐使用，建议改用上面的 Begin(Material, ...) 版本。
        //这个API 是对齐 MonoGame,不再增加新字段和删除字段。不要修改任何参数签名，避免破坏兼容性。 
        public void Begin(SpriteSortMode sortMode = SpriteSortMode.Deferred,
                          BlendState? blendState = null,
                          SamplerState? samplerState = null,
                          DepthStencilState? depthStencilState = null,
                          RasterizerState? rasterizerState = null,
                          ShaderEffect? effect = null,
                          Matrix4x4? transformMatrix = null)
        {
            // 复用本批材质快照 _cache_Mat（避免每次 Begin 都 new），填好参数后交给 BeginInternal。
            _cache_Mat.Reset();
            _cache_Mat.Effect = effect;
            _cache_Mat.Blend = blendState ?? BlendState.NonPremultiplied;
            _cache_Mat.Sampler = samplerState ?? SamplerState.Point;
            _cache_Mat.DepthStencil = depthStencilState ?? DepthStencilState.None;
            _cache_Mat.Rasterizer = rasterizerState ?? RasterizerState.CullNone;
            BeginInternal(_cache_Mat, sortMode, transformMatrix);
        }

        private void BeginInternal(Material material, SpriteSortMode sortMode, Matrix4x4? transformMatrix)
        {
            if (_beginCalled) throw new InvalidOperationException("上一次 Begin 还没有对应的 End。");

            // 材质没指定效果就在【开批】时落到设备默认效果（写回字段）：此后 ApplyMaterial 与着色器程序
            // 都只认 material.Effect，不必在更晚的地方再判一次空。
            material.Effect ??= ShaderEffect.Default;

            _sortMode = sortMode;
            _material = material;
            _transform = transformMatrix ?? Matrix4x4.Identity;

            // 投影矩阵：与 GpuInstanceBatch / UrpBatch 共用同一份（见 GraphicsDevice.CreateSpriteProjection）。
            _projection = _device.CreateSpriteProjection();
            _transformProjection = _transform * _projection;

            // ---- 批内不变的渲染状态，在这里一次性下发 ----
            // 材质（混合 / 深度 / 剔除 / 采样）与效果（着色器程序 + uProjection + 材质属性）在整个 Begin/End
            // 期间都不会变（一个批只有一个材质），所以只在开批时下发一次；提交时每段只需换纹理。
            // 「带属性块」的段例外：块的 uniform 值必须逐段下发/还原（见 FlushBatch），那是可变 uniform 的代价。
            _device.ApplyMaterial(_material, _transformProjection, null);
            _blockApplied = false;

            _beginCalled = true;
        }

        public void End()
        {
            if (!_beginCalled) throw new InvalidOperationException("End 必须在 Begin 之后调用。");
            _beginCalled = false;

            FlushBatch();
        }

        /// <summary>
        /// 提交当前累积的绘制：排序 → 按「纹理 + 属性块」切段 → 把段内绘制项展开成四边形顶点 →
        /// 整段一次 <c>drawElements</c>；精灵总数累加到渲染统计。
        /// <para>
        /// <b>每段只换纹理</b>：材质状态 / 效果程序 / uProjection / 材质属性这些批内不变的东西，已在
        /// <see cref="BeginInternal"/> 里下发过一次，这里不再重复 —— 这正是"一个批一个材质"的红利
        /// （不打图集时"一张图一段"，若每段都重设一遍状态，开销就按精灵数走了）。
        /// </para>
        /// <para>
        /// 例外是<b>属性块</b>：块是可变 uniform，值必须逐段生效 —— 带块的段下发块值（盖住材质默认值），
        /// 之后遇到不带块的段要把材质默认值发回去（<see cref="_blockApplied"/>）。
        /// 分批键 = 纹理引用 + 属性块（引用 + 版本号），所以块一变（含"同一个块被改过"）就另起一段；
        /// 带块的绘制会在 <see cref="Draw"/> 里当场调用它，因此这里通常是「一段不带块 + 末尾一个带块项」。
        /// </para>
        /// </summary>
        private void FlushBatch()
        {
            if (_batchItemCount == 0) return;

            switch (_sortMode)
            {
                case SpriteSortMode.Texture:
                case SpriteSortMode.FrontToBack:
                case SpriteSortMode.BackToFront:
                    Array.Sort(_batchItemList, 0, _batchItemCount);
                    break;
            }

            // 整批精灵总数（照 MonoGame：spriteCount 只在提交时累加一次）。
            _device._metrics._spriteCount += _batchItemCount;

            // 多批交错使用时补发：Begin 之后若有别的批处理（另一个 SpriteBatch / GpuInstanceBatch / UrpBatch）
            // 改过设备状态，本批的材质 + 变换就得补回来（批是类，可以 new 多个、彼此交错）。
            // 顺序使用时这里只是一次引用比较 + 一次矩阵比较。
            if (!_device.IsMaterialCurrent(_material, _transformProjection))
            {
                _device.ApplyMaterial(_material, _transformProjection, null);
                _blockApplied = false;
            }

            Matrix4x4 transform = _transformProjection;
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

                        // 材质状态 / 效果 / 投影已在 Begin 里下发过（批内不变），这里只处理"变的"：
                        //   1) 属性块：块值必须逐段下发；块结束后要还原材质默认值（否则后续精灵会沿用块值）；
                        //   2) 纹理：每段必然不同（分段键之一）。
                        if (block is not null)
                        {
                            _device.ApplyMaterial(_material, transform, block);
                            _blockApplied = true;
                        }
                        else if (_blockApplied)
                        {
                            _device.ApplyMaterial(_material, transform, null);
                            _blockApplied = false;
                        }

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

        /// <summary>从对象池取一个可复用的绘制项；池满了就按 1.5 倍扩容（并按 64 对齐）。</summary>
        private SpriteBatchItem CreateBatchItem()
        {
            if (_batchItemCount >= _batchItemList.Length)
            {
                int oldSize = _batchItemList.Length;
                int newSize = (oldSize + oldSize / 2 + 63) & (~63);
                Array.Resize(ref _batchItemList, newSize);
                for (int i = oldSize; i < newSize; i++) _batchItemList[i] = new SpriteBatchItem();

                EnsureVertexArrayCapacity(Math.Min(newSize, GraphicsDevice.MaxBatchSize));
            }
            return _batchItemList[_batchItemCount++];
        }

        /// <summary>保证顶点数组能装下 <paramref name="numBatchItems"/> 个四边形（每个 4 顶点）。</summary>
        private void EnsureVertexArrayCapacity(int numBatchItems)
        {
            int needed = 4 * numBatchItems;
            if (_vertexArray != null && needed <= _vertexArray.Length)
            {
                return;
            }
            _vertexArray = new VertexPositionColorTexture[Math.Max(needed, 4 * GraphicsDevice.MaxBatchSize)];
        }

        /// <summary>把顶点数组的一段交给设备绘制（一次 draw call）。</summary>
        private void FlushVertexArray(int start, int end)
        {
            if (start == end) return;
            _device.DrawUserIndexedPrimitives(_vertexArray, start, end);
        }


        public void Draw(Texture2D texture, Vector2 position, Color color)
            => Draw(texture, position, null, color, 0f, Vector2.Zero, Vector2.One, SpriteEffects.None, 0f);

        public void Draw(Texture2D texture, Vector2 position, Color color, float rotation, Vector2 origin, float scale, float layerDepth = 0f)
            => Draw(texture, position, null, color, rotation, origin, new Vector2(scale, scale), SpriteEffects.None, layerDepth);

        public void Draw(Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color)
            => Draw(texture, position, sourceRectangle, color, 0f, Vector2.Zero, Vector2.One, SpriteEffects.None, 0f);

        /// <summary>
        /// 完整参数的绘制（照 MonoGame 的 Draw，UV 计算兼容本引擎的图集 Bounds 偏移）。
        /// <para>
        /// <paramref name="properties"/>：这一次绘制的着色器属性覆盖块（照 Unity 的 <c>renderer.SetPropertyBlock</c>）。
        /// 块是可变 uniform，只有"这一批只画这一个物体"时才等价，故带块的绘制会当场提交（切批）。
        /// 想让"逐精灵不同"仍然只一次 DrawCall，请改用 GPU 实例化（<see cref="GpuInstanceBatch"/>，另一条路）。
        /// </para>
        /// </summary>
        public void Draw(Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color,
                         float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth = 0f,
                         ShaderPropertyBlock? properties = null)
        {
            CheckValid(texture);
            DrawVertexPath(texture, position, sourceRectangle, color, rotation, origin, scale, effects, layerDepth, properties);
        }

        /// <summary>
        /// 逐顶点路径的一次绘制：把精灵展开成 4 个顶点交给本类的排队/提交。
        /// </summary>
        /// <param name="uniformBlock">需要在本段下发 uniform 的属性块（可空；非空时这一笔当场提交、切批）。</param>
        private void DrawVertexPath(Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color,
                                    float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth,
                                    ShaderPropertyBlock? uniformBlock)
        {
            SpriteBatchItem item = CreateBatchItem();
            item.Texture = texture;
            item.Properties = uniformBlock;
            item.BlockVersion = uniformBlock?.PropertiesVersion ?? -1;
            item.SortKey = SortKeyFor(texture, layerDepth);

            ComputeUv(texture, sourceRectangle, effects, out Vector2 uvTL, out Vector2 uvBR);
            Rectangle source = sourceRectangle ?? new Rectangle(0, 0, texture.Width, texture.Height);
            float w = source.Width * scale.X;
            float h = source.Height * scale.Y;

            // 照 MonoGame：origin 先乘以 scale，再参与定位。
            origin = origin * scale;
            if (rotation == 0f)
            {
                item.Set(position.X - origin.X, position.Y - origin.Y, w, h, color, uvTL, uvBR, layerDepth);
            }
            else
            {
                float c = MathF.Cos(rotation), s = MathF.Sin(rotation);
                item.Set(position.X, position.Y, -origin.X, -origin.Y, w, h, s, c, color, uvTL, uvBR, layerDepth);
            }

            if (_sortMode == SpriteSortMode.Immediate || uniformBlock is not null)
            {
                // Immediate：不排队，当场画。
                // 带 uniform 块：块是【可变的】uniform 数据，值必须当场生效（若等 End 再提交，块早被后面的绘制改掉了）
                // —— 一次 draw 只有一份 uniform 值，这是必然的代价。
                FlushBatch();
            }
        }

        /// <summary>排序键：照 MonoGame。Texture 模式用 Texture.SortingKey（纹理内在序号），前后排序用 layerDepth。</summary>
        private float SortKeyFor(Texture2D texture, float layerDepth) => _sortMode switch
        {
            SpriteSortMode.Texture => texture.SortingKey,
            SpriteSortMode.FrontToBack => layerDepth,
            SpriteSortMode.BackToFront => -layerDepth,
            _ => 0f,
        };

        /// <summary>
        /// UV 计算（含图集 Bounds 偏移与翻转）。
        /// <para>
        /// <c>internal</c>：<see cref="GpuInstanceBatch"/> / <see cref="UrpBatch"/> 与这里共用同一份，
        /// 保证三个批处理的 sourceRectangle / SpriteEffects 语义完全一致（换批处理不用改调用代码）。
        /// </para>
        /// </summary>
        internal static void ComputeUv(Texture2D texture, Rectangle? sourceRectangle, SpriteEffects effects,
                                       out Vector2 uvTL, out Vector2 uvBR)
        {
            Rectangle source = sourceRectangle ?? new Rectangle(0, 0, texture.Width, texture.Height);

            uvTL = new Vector2((texture.Bounds.X + source.X) / (float)texture.TextureWidth,
                               (texture.Bounds.Y + source.Y) / (float)texture.TextureHeight);
            uvBR = new Vector2((texture.Bounds.X + source.X + source.Width) / (float)texture.TextureWidth,
                               (texture.Bounds.Y + source.Y + source.Height) / (float)texture.TextureHeight);

            if ((effects & SpriteEffects.FlipVertically) != 0) (uvTL.Y, uvBR.Y) = (uvBR.Y, uvTL.Y);
            if ((effects & SpriteEffects.FlipHorizontally) != 0) (uvTL.X, uvBR.X) = (uvBR.X, uvTL.X);
        }

        /// <summary>目标矩形既决定位置也决定缩放（照 MonoGame 的带目标矩形重载）。</summary>
        public void Draw(Texture2D texture, Rectangle destinationRectangle, Rectangle? sourceRectangle, Color color,
                         float rotation, Vector2 origin, SpriteEffects effects, float layerDepth)
        {
            int sw = sourceRectangle?.Width ?? texture.Width;
            int sh = sourceRectangle?.Height ?? texture.Height;
            var scale = new Vector2(sw == 0 ? 0f : destinationRectangle.Width / (float)sw,
                                    sh == 0 ? 0f : destinationRectangle.Height / (float)sh);
            Draw(texture, new Vector2(destinationRectangle.X, destinationRectangle.Y), sourceRectangle, color,
                 rotation, origin, scale, effects, layerDepth);
        }

        public void Draw(Texture2D texture, Rectangle destination, Color color)
            => Draw(texture, destination, null, color);

        public void Draw(Texture2D texture, Rectangle destination, Rectangle? sourceRectangle, Color color)
        {
            ArgumentNullException.ThrowIfNull(texture);
            Draw(texture, new Vector2(destination.X, destination.Y), sourceRectangle, color,
                 0f, Vector2.Zero,
                 new Vector2(destination.Width / (float)(sourceRectangle?.Width ?? texture.Width),
                             destination.Height / (float)(sourceRectangle?.Height ?? texture.Height)),
                 SpriteEffects.None, 0f);
        }

        /// <summary>
        /// 属性块重载（照 Unity 的 <c>renderer.SetPropertyBlock</c>）：块挂在<b>这一次绘制</b>上，
        /// 能覆盖任意条数、任意类型的属性（float / int / 向量 / 矩阵 / 纹理），代价是块变即切批
        /// （一次 draw 只带一份 uniform，给每个物体不同值 = 每个物体一次 DrawCall）。
        /// </summary>
        public void Draw(Texture2D texture, Vector2 position, Color color, ShaderPropertyBlock? properties)
            => Draw(texture, position, null, color, 0f, Vector2.Zero, Vector2.One, SpriteEffects.None, 0f, properties);

        public void Draw(Texture2D texture, Rectangle destination, Color color, ShaderPropertyBlock? properties)
            => Draw(texture, destination, null, color, properties);

        public void Draw(Texture2D texture, Rectangle destination, Rectangle? sourceRectangle, Color color, ShaderPropertyBlock? properties)
        {
            ArgumentNullException.ThrowIfNull(texture);
            Draw(texture, new Vector2(destination.X, destination.Y), sourceRectangle, color,
                 0f, Vector2.Zero,
                 new Vector2(destination.Width / (float)(sourceRectangle?.Width ?? texture.Width),
                             destination.Height / (float)(sourceRectangle?.Height ?? texture.Height)),
                 SpriteEffects.None, 0f, properties);
        }

        /// <summary>以中心点对齐绘制并缩放。</summary>
        public void DrawCentered(Texture2D texture, Vector2 center, Color color,
                                 float rotation = 0f, float scale = 1f, float layerDepth = 0f)
            => Draw(texture, center, null, color, rotation,
                    new Vector2(texture.Width / 2f, texture.Height / 2f),
                    new Vector2(scale, scale), SpriteEffects.None, layerDepth);



        public void DrawString(IFont font, string text, Vector2 position, Color color)
            => font.Draw(this, text, position, color, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

        public void DrawString(IFont font, string text, Vector2 position, Color color,
                               float rotation, Vector2 origin, float scale, float layerDepth = 0f)
            => font.Draw(this, text, position, color, rotation, origin, scale, SpriteEffects.None, layerDepth);


        private void CheckValid(Texture2D texture)
        {
            if (texture == null) throw new ArgumentNullException(nameof(texture));
            if (!_beginCalled) throw new InvalidOperationException("Draw 必须在 Begin / End 之间调用。");
        }
    }
}
