namespace KFramework.MonoGame
{
    public sealed class SpriteBatch
    {
        private const int InitialBatchSize = 256;
        private readonly GraphicsDevice _device;
        private SpriteSortMode _sortMode;
        private Matrix4x4 _cameraMatrix = Matrix4x4.Identity;
        private Matrix4x4 _projection;
        private Material _material = null!;
        private readonly Material _cache_Mat = new Material();
        private bool _beginCalled;

        private SpriteBatchItem[] _batchItemList;

        /// <summary>本批已排队的绘制项数（提交后清零）。</summary>
        private int _batchItemCount;

        /// <summary>本批的「世界 → 屏幕」矩阵（= 相机矩阵 × 投影）：批内不变，开批时算一次。</summary>
        private Matrix4x4 _cameraProjectionMatrix;

        /// <summary>设备上此刻是否挂着某个属性块的 uniform 值（为 true 时，遇不带块的段要先把材质默认值发回去）。</summary>
        private bool _blockApplied;

        /// <summary>逐顶点数组（提交前把绘制项展开成四边形写在这里，再整段上传）。</summary>
        private VertexPositionColorTexture[] _vertexArray;
        
        internal SpriteBatch(GraphicsDevice device)
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
        //transformMatrix 就是 Camera（相机）矩阵：世界 → 屏幕的变换，null = 单位矩阵；
        //与投影矩阵相乘后作为整批顶点的变换下发（批内不变，见 GraphicsDevice.CreateSpriteProjection）。
        //projection：自定义投影矩阵（如透视矩阵），null = 使用默认正交屏幕投影（CreateSpriteProjection）；
        //传透视矩阵即可让本批 2D 精灵走透视渲染（近大远小），但需自行保证深度/相机关系正确。
        public void Begin(Material material, SpriteSortMode sortMode = SpriteSortMode.Deferred,
                          Matrix4x4? transformMatrix = null, Matrix4x4? projection = null)
        {
            BeginInternal(material, sortMode, transformMatrix, projection);
        }

        //这是老API，后续不再推荐使用，建议改用上面的 Begin(Material, ...) 版本。
        //这个API 是对齐 MonoGame,不再增加新字段和删除字段。不要修改任何参数签名，避免破坏兼容性。 
        //transformMatrix：同上面的主力 API —— 就是 Camera（相机）矩阵（世界 → 屏幕），null = 单位矩阵。
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
            BeginInternal(_cache_Mat, sortMode, transformMatrix, null);
        }

        private void BeginInternal(Material material, SpriteSortMode sortMode, Matrix4x4? transformMatrix, Matrix4x4? projection = null)
        {
            if (_beginCalled)
            {
                throw new InvalidOperationException("上一次 Begin 还没有对应的 End。");
            }

            material.Effect ??= ShaderEffect.Default;
            _material = material;

            _sortMode = sortMode;
   
            _cameraMatrix = transformMatrix ?? Matrix4x4.Identity;
            _projection = projection ?? _device.CreateSpriteProjection();
            _cameraProjectionMatrix = _cameraMatrix * _projection;

            _beginCalled = true;
        }

        public void End()
        {
            if (!_beginCalled) throw new InvalidOperationException("End 必须在 Begin 之后调用。");
            _beginCalled = false;

            FlushBatch();
        }

        /// <summary>
        /// 安全结束当前批：若已 <see cref="Begin"/> 则提交当前段（<see cref="FlushBatch"/>）并复位 Begin 状态；
        /// 未 Begin 时为 no-op（不会像 <see cref="End"/> 那样抛异常）。
        /// 用于「自动切批」——调用本方法提交旧段后，紧接 <c>Begin(新材质)</c> 即可切换到新的 SpriteBatch 段。
        /// </summary>
        public void AutoEndBatch()
        {
            if (_beginCalled)
            {
                _beginCalled = false;
                FlushBatch();
            }
        }

        /// <summary>
        /// 提交当前累积的绘制：下发材质 → 排序 → 按「纹理 + 属性块」切段 → 把段内绘制项展开成四边形顶点 →
        /// 整段一次 <c>drawElements</c>；精灵总数累加到渲染统计。
        /// <para>
        /// <b>材质在提交这一刻下发</b>（而不是 <see cref="BeginInternal"/> 里）：空批不白设状态；
        /// 且批进行中途若发生过 <c>Clear</c>（切渲染目标清屏 → <see cref="GraphicsDevice.InvalidateMaterialCache"/>，
        /// 后端会重置 uniform 槽位）或块段残留，提交前的这一刻也能把状态拉回确定值。
        /// <b>不设 dirty 标志</b>是有意的：<see cref="GraphicsDevice.ApplyMaterial"/> 自身按
        /// 「材质内容 + 变换」去重，重复调用只做几次引用比较；一旦用标志跳过，上面那种中途失效就会漏掉。
        /// </para>
        /// <para>
        /// <b>每段只换纹理</b>：材质状态 / 效果程序 / uProjection / 材质属性都是批内不变的，上面统一下发
        /// 一次即可，不必每段重复 —— 这正是"一个批一个材质"的红利（不打图集时"一张图一段"，
        /// 若每段都重设一遍状态，开销就按精灵数走了）。
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

            // ---- 材质：批内不变，提交前下发一次 ----
            // 重复调用由 GraphicsDevice 按「材质内容 + 变换」去重（命中则只做几次引用比较）。
            // _blockApplied 归零：ApplyMaterial(..., null) 之后状态已回到材质默认值，
            // 块"待还原"的标记必须同步清掉，否则后面的不带块段会多走一次还原路径。
            _device.ApplyMaterial(_material, _cameraProjectionMatrix, null);
            _blockApplied = false;

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

                    if (!batchStarted
                        || !ReferenceEquals(item.Texture, tex)
                        || !ReferenceEquals(item.Properties, block)
                        || item.BlockVersion != blockVersion)
                    {
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
                            _device.ApplyMaterial(_material, _cameraProjectionMatrix, block);
                            _blockApplied = true;
                        }
                        else if (_blockApplied)
                        {
                            _device.ApplyMaterial(_material, _cameraProjectionMatrix, null);
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


        /// <summary>
        /// 绘制（位置形态，浮点）：按浮点位置画整幅纹理，带旋转 / 锚点 / 缩放 [+ 着色 + 属性块]。
        /// <paramref name="position"/> 是锚点落点（即 <paramref name="origin"/> 落在哪）；
        /// <paramref name="origin"/> 是源纹理上的像素锚点（按 源尺寸 → 实际尺寸 的比例放大后再定位）；
        /// <paramref name="scale"/> 传 null = 不缩放；<paramref name="color"/> 传 null = <see cref="Color.White"/>。
        /// <para>
        /// 本形态不做翻转、不参与深度排序（翻转与 <c>layerDepth</c> 只在下面那个「目标矩形形态」里给）；
        /// 顶点按 <see cref="SpriteEffects.None"/> / 深度 0 展开，要与其它精灵比深度请用目标矩形形态。
        /// </para>
        /// </summary>
        public void Draw(
            Texture2D texture,
            Vector2 position, 
            float rotation = 0f,
            Vector2? scale = null,
            Color? color = null,
            Vector2 origin = default, 
            ShaderPropertyBlock? properties = null)
        {
            CheckValid(texture);
            DrawVertexPath(texture, position, null, color ?? Color.White, rotation, origin, scale ?? Vector2.One,
                           SpriteEffects.None, 0f, properties);
        }

        /// <summary>
        /// 绘制（完整形态）：与 <see cref="GpuInstanceBatch"/> / <see cref="UrpBatch"/> 的那个 Draw 逐参数对应。
        /// <paramref name="targetRectangle"/> 的 X/Y 是锚点落点、Width/Height 是基准尺寸（null = 落点在原点、尺寸取源尺寸）；
        /// <paramref name="scale"/> 在矩形尺寸之上再缩放（null = 不额外缩放）；<paramref name="origin"/> 是源纹理上的像素锚点。
        /// </summary>
        public void Draw(
            Texture2D texture, 
            Rectangle? targetRectangle, 
            Rectangle? sourceRectangle, 
            Color color,
            float rotation = 0f, 
            Vector2 origin = default, 
            Vector2? scale = null,
            SpriteEffects effects = SpriteEffects.None, 
            float layerDepth = 0f,
            ShaderPropertyBlock? properties = null)
        {
            CheckValid(texture);

            Rectangle source = sourceRectangle ?? new Rectangle(0, 0, texture.Width, texture.Height);
            Vector2 position = targetRectangle.HasValue
                ? new Vector2(targetRectangle.Value.X, targetRectangle.Value.Y)
                : Vector2.Zero;

            // 实际尺寸 =（目标矩形 / 源尺寸）× 额外缩放 → 换算成逐顶点路径要的缩放系数。
            float w = (targetRectangle?.Width ?? source.Width) * (scale?.X ?? 1f);
            float h = (targetRectangle?.Height ?? source.Height) * (scale?.Y ?? 1f);
            var effectiveScale = new Vector2(source.Width == 0 ? 0f : w / source.Width,
                                             source.Height == 0 ? 0f : h / source.Height);

            DrawVertexPath(texture, position, sourceRectangle, color, rotation, origin, effectiveScale,
                           effects, layerDepth, properties);
        }

        /// <summary>引擎内部用（字体排字）：按浮点位置画字形 —— 公开面只有两个 Draw（位置形态 / 目标矩形形态），字体用这条保留浮点精度。</summary>
        internal void DrawGlyph(Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color,
                                float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth,
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

            if (uniformBlock is not null)
            {
                // 带 uniform 块：块是【可变的】uniform 数据，值必须当场生效（若等 End 再提交，块早被后面的绘制改掉了）
                // —— 一次 draw 只有一份 uniform 值，这是必然的代价。
                // （没有 Immediate 模式：需要"结算当前已排队的内容"直接 End 即可。）
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

        /// <summary>以中心点对齐绘制并缩放。</summary>
        public void DrawCentered(Texture2D texture, Vector2 center, Color color,
                                 float rotation = 0f, float scale = 1f, float layerDepth = 0f)
        {
            // 中心点对齐：目标矩形的锚点落在 center，锚点取纹理中心 —— 于是精灵（含旋转）绕 center 摆。
            var target = new Rectangle((int)MathF.Round(center.X), (int)MathF.Round(center.Y),
                                       (int)MathF.Round(texture.Width * scale),
                                       (int)MathF.Round(texture.Height * scale));
            Draw(texture, target, null, color, rotation,
                 new Vector2(texture.Width / 2f, texture.Height / 2f), null, SpriteEffects.None, layerDepth);
        }



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
