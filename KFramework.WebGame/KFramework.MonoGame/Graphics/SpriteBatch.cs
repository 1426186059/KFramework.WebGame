using System.Numerics;

namespace KFramework.MonoGame
{
    public sealed class SpriteBatch
    {
        private readonly GraphicsDevice _device;
        private readonly SpriteBatcher _batcher;

        private SpriteSortMode _sortMode;
        private Matrix4x4 _transform = Matrix4x4.Identity;
        private Matrix4x4 _projection;
        private Material _material = null!;
        private readonly Material _cache_Mat = new Material();

        private bool _beginCalled;

        // ---- 实例化（GPU Instancing）路径的排队状态 ----

        /// <summary>本批是否走实例化提交（由 Begin 的 instanced 参数决定）。</summary>
        private bool _instanced;

        /// <summary>本批已排队的实例（按绘制顺序；排序模式非 Deferred 时在提交前排序）。</summary>
        private readonly List<InstanceEntry> _instances = new();

        /// <summary>提交时把 <see cref="_instances"/> 拷成连续数组（实例缓冲要的是连续内存）。</summary>
        private SpriteInstance[] _instanceScratch = Array.Empty<SpriteInstance>();

        /// <summary>缓存比较委托，避免每次排序分配（List.Sort 会持有一个 Comparison）。</summary>
        private static readonly Comparison<InstanceEntry> EntryComparison = CompareEntries;

        /// <summary>实例化路径里排队的一条绘制：纹理 + 逐实例数据 + 排序键。</summary>
        private struct InstanceEntry
        {
            public Texture2D Texture;
            public SpriteInstance Data;
            public float SortKey;
            public int Order;
        }

        public SpriteBatch(GraphicsDevice device)
        {
            ArgumentNullException.ThrowIfNull(device);
            _device = device;
            _batcher = new SpriteBatcher(device);
        }

        public GraphicsDevice GraphicsDevice => _device;

        /// <summary>
        /// 用材质配置开启一批绘制。材质打包了着色器 + 混合/采样/深度/剔除状态，
        /// GraphicsDevice 按「材质内容 + 变换」做去重，相同配置不再重复下发跨 JS 状态。
        /// <para>
        /// 想在某一次绘制上临时覆盖材质属性，用 <see cref="Draw(Texture2D, Rectangle, Color, MaterialPropertyBlock?)"/>
        /// 的 block 参数 —— 属性块的归属是「这一次绘制」（照 Unity 的 <c>renderer.SetPropertyBlock</c>），不是这一批。
        /// </para>
        /// <para>
        /// <paramref name="instanced"/> = true 时走 GPU 实例化提交：每个精灵只写一条 48 字节实例数据，
        /// 一批（同一纹理）一次 <c>drawElementsInstanced</c>。逐物体数据来自 Draw 的参数与
        /// <see cref="MaterialPropertyBlock"/>。目前仅 WebGL2 支持；WebGPU 上会抛 <see cref="NotSupportedException"/>。
        /// </para>
        /// <para>
        /// <b>实例化模式的两条约定</b>（都是"照 Unity"）：
        /// <list type="number">
        ///   <item><description>想让 MaterialPropertyBlock 的值进实例数据，必须先在本批材质上声明通道：
        ///   <c>material.SetSpriteChannels("uTint.rgb", "uPulse")</c> —— 与逐顶点模式同一套映射；
        ///   没被映射的属性会让这一笔退回逐顶点路径单独画（等同 Unity 的"非实例化属性把物体踢出实例化"）。</description></item>
        ///   <item><description>本模式用<b>内置实例化着色器</b>（纹理 × 逐实例颜色，8 个逐实例参数已送到
        ///   <c>vParams0</c> / <c>vParams1</c>），<see cref="Material.Effect"/> 里的自定义片元着色器<b>不参与</b>；
        ///   需要自定义时请用逐顶点模式，或等我们给实例化加自定义片元入口。</description></item>
        /// </list>
        /// </para>
        /// </summary>
        public void Begin(Material material, SpriteSortMode sortMode = SpriteSortMode.Deferred,
                          Matrix4x4? transformMatrix = null, bool instanced = false)
        {
            BeginInternal(material, sortMode, transformMatrix, instanced);
        }

        /// <summary>
        /// 兼容旧签名的重载：把散装的 Blend / Sampler / DepthStencil / Rasterizer / Effect 状态包成一个材质，
        /// 交给 BeginInternal。effect 为 null 时使用设备默认精灵着色器（<see cref="GraphicsDevice.Effect"/>）。
        /// 参数顺序对齐原版 MonoGame SpriteBatch.Begin 的散装签名（effect 位于 rasterizerState 与 transformMatrix 之间）。
        /// 新增代码建议直接用 <see cref="Begin(Material, SpriteSortMode, Matrix4x4?)"/>。
        /// </summary>
        public void Begin(SpriteSortMode sortMode = SpriteSortMode.Deferred,
                          BlendState? blendState = null,
                          SamplerState? samplerState = null,
                          DepthStencilState? depthStencilState = null,
                          RasterizerState? rasterizerState = null,
                          Effect? effect = null,
                          Matrix4x4? transformMatrix = null,
                          bool instanced = false)
        {
            // 复用本批材质快照 _cache_Mat（避免每次 Begin 都 new），填好参数后交给 BeginInternal。
            _cache_Mat.Reset();
            _cache_Mat.Effect = effect;
            _cache_Mat.Blend = blendState ?? BlendState.NonPremultiplied;
            _cache_Mat.Sampler = samplerState ?? SamplerState.Point;
            _cache_Mat.DepthStencil = depthStencilState ?? DepthStencilState.None;
            _cache_Mat.Rasterizer = rasterizerState ?? RasterizerState.CullNone;
            BeginInternal(_cache_Mat, sortMode, transformMatrix, instanced);
        }

        private void BeginInternal(Material material, SpriteSortMode sortMode, Matrix4x4? transformMatrix, bool instanced)
        {
            if (_beginCalled) throw new InvalidOperationException("上一次 Begin 还没有对应的 End。");
            if (instanced && !_device.Backend.SupportsInstancing)
                throw new NotSupportedException(
                    $"后端「{_device.Backend.Name}」尚未接入 GPU 实例化：Begin(..., instanced: true) 目前只在 WebGL2 上可用。");

            _sortMode = sortMode;
            _instanced = instanced;
            _instances.Clear();
            _material = material;
            _transform = transformMatrix ?? Matrix4x4.Identity;
            _batcher.SetSamplerState(material.Sampler);

            var viewport = _device.Viewport;
            // 离屏时是否改用 Y 向上投影，取决于后端的坐标系原点（详见 IGraphicsBackend.NeedsOffscreenYFlip）：
            //   WebGL  —— FBO 原点在左下，"屏幕翻转"与"FBO 翻转"抵消后才对，故需要；
            //   WebGPU —— 附件原点与屏幕一致（都在左上），再翻一次就会上下颠倒，故不需要。
            // WebGL 这条等价于 MonoGame GL 后端在顶点着色器里对离屏渲染做的 posFixup.y *= -1
            // （GraphicsDevice.OpenGL.cs: "If we have a render target bound (rendering offscreen) flip vertically"）。
            // 注意：这里曾把 WebGL 的事实当成通用真理硬编码，导致 WebGPU 的离屏画面上下颠倒
            //（症状：渲染目标里的文字是倒的）。
            _projection = _device.RenderTargetCount > 0 && _device.Backend.NeedsOffscreenYFlip
                ? Matrix4x4.CreateOrthographicOffCenter(0f, viewport.Width, 0f, viewport.Height, 0f, 1f)
                : Matrix4x4.CreateOrthographicScreen(viewport.Width, viewport.Height);

            // Immediate 模式与「带属性块」的绘制，都由 batcher 在提交时逐段下发状态（见 Draw / End），这里不提前设。
            _beginCalled = true;
        }

        public void End()
        {
            if (!_beginCalled) throw new InvalidOperationException("End 必须在 Begin 之后调用。");
            _beginCalled = false;

            // 实例化模式：把排队的实例按纹理分组、一次 drawElementsInstanced 提交；
            // 逐顶点模式：交给 SpriteBatcher 排序 + 分批。
            if (_instanced) FlushInstances();
            else FlushBatch();
        }

        /// <summary>
        /// 提交当前累积的绘制：分批与「每段下发材质 + 属性块」都在 <see cref="SpriteBatcher.DrawBatch"/> 里做。
        /// Immediate 模式与带属性块的绘制会中途调用它（属性块是可变 uniform，值必须当场生效）。
        /// </summary>
        private void FlushBatch()
            => _batcher.DrawBatch(_sortMode, _material, _transform * _projection);


        public void Draw(Texture2D texture, Vector2 position, Color color)
            => Draw(texture, position, null, color, 0f, Vector2.Zero, Vector2.One, SpriteEffects.None, 0f);

        public void Draw(Texture2D texture, Vector2 position, Color color, float rotation, Vector2 origin, float scale, float layerDepth = 0f)
            => Draw(texture, position, null, color, rotation, origin, new Vector2(scale, scale), SpriteEffects.None, layerDepth);

        public void Draw(Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color)
            => Draw(texture, position, sourceRectangle, color, 0f, Vector2.Zero, Vector2.One, SpriteEffects.None, 0f);

        /// <summary>
        /// 完整参数的绘制（照 MonoGame 的 Draw，UV 计算兼容本引擎的图集 Bounds 偏移）。
        /// <para>
        /// <paramref name="spriteParams"/>：逐精灵参数（8 个通道）。逐顶点模式下随顶点上传、不参与分批键；
        /// 实例化模式下进实例数据。两种模式下都<b>不会</b>因为"每个精灵不同"而打断合批。
        /// </para>
        /// <para>
        /// <paramref name="properties"/>：这一次绘制的材质属性覆盖块（照 Unity 的 <c>renderer.SetPropertyBlock</c>）。
        /// <list type="bullet">
        ///   <item><description><b>逐顶点模式</b>：能被 <see cref="Material.SetSpriteChannels"/> 映射的属性编码进顶点（照样合批）；
        ///   剩下映射不了的（矩阵、纹理等）只能挂 uniform —— 那部分会让这一次绘制切批。</description></item>
        ///   <item><description><b>实例化模式</b>：映射得到的值随实例数据进 GPU（一批一次 draw）；块里含映射不了的属性时，
        ///   这一笔退回逐顶点路径单独画（照 Unity：非实例化属性会把该物体踢出实例化）。</description></item>
        /// </list>
        /// </para>
        /// </summary>
        public void Draw(Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color,
                         float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth = 0f,
                         SpriteParams spriteParams = default, MaterialPropertyBlock? properties = null)
        {
            CheckValid(texture);

            // 材质声明过「属性块 → 顶点通道」映射时：能映射的属性编码成 8 个通道值，
            // 剩下映射不了的（矩阵、纹理等）仍然得挂 uniform —— 那部分会让这一次绘制切批。
            // 这是 MaterialPropertyBlock "同材质 + 每物体不同 + DrawCall 合并" 的关键一环。
            MaterialPropertyBlock? uniformBlock = properties;
            if (_material.HasSpriteChannels)
            {
                spriteParams = _material.SpriteChannels!.Encode(properties, _material, spriteParams);
                uniformBlock = properties is not null && HasUnmappedProperty(properties) ? properties : null;
            }

            if (_instanced)
            {
                if (uniformBlock is null)
                {
                    // 实例化路径：只排队一条 48 字节的实例数据，不碰顶点缓冲。
                    AddInstance(texture, position, sourceRectangle, color, rotation, origin, scale, effects, layerDepth, spriteParams);
                    return;
                }

                // 块里有"通道装不下、只能挂 uniform"的属性：实例数据里没有它们的容身之处，
                // 这一笔退回逐顶点路径单独画。先把已排队的实例提交掉，保证绘制顺序不乱。
                FlushInstances();
            }

            DrawVertexPath(texture, position, sourceRectangle, color, rotation, origin, scale, effects, layerDepth, spriteParams, uniformBlock);
        }

        /// <summary>
        /// 逐顶点路径的一次绘制：把精灵展开成 4 个顶点交给 <see cref="SpriteBatcher"/> 排队/提交。
        /// </summary>
        /// <param name="uniformBlock">需要在本段下发 uniform 的属性块（可空；非空时这一笔当场提交、切批）。</param>
        private void DrawVertexPath(Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color,
                                    float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth,
                                    SpriteParams spriteParams, MaterialPropertyBlock? uniformBlock)
        {
            SpriteBatchItem item = _batcher.CreateBatchItem();
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
                item.Set(position.X - origin.X, position.Y - origin.Y, w, h, color, uvTL, uvBR, layerDepth, spriteParams);
            }
            else
            {
                float c = MathF.Cos(rotation), s = MathF.Sin(rotation);
                item.Set(position.X, position.Y, -origin.X, -origin.Y, w, h, s, c, color, uvTL, uvBR, layerDepth, spriteParams);
            }

            if (_sortMode == SpriteSortMode.Immediate || uniformBlock is not null)
            {
                // Immediate：不排队，当场画。
                // 带 uniform 块：块是【可变的】uniform 数据，值必须当场生效（若等 End 再提交，块早被后面的绘制改掉了）
                // —— 一次 draw 只有一份 uniform 值，这是必然的代价。
                FlushBatch();
            }
        }

        /// <summary>
        /// 实例化路径的一次绘制：把这次绘制的「中心点 / 尺寸 / 旋转 / 颜色 / 8 个参数 / UV 矩形」记成一条实例数据，
        /// 到 <see cref="End"/>（或中途需要切批时）再按纹理分组、一次 <c>drawElementsInstanced</c> 提交。
        /// <para>
        /// 几何语义与逐顶点路径一致：origin 先乘 scale、旋转绕 <paramref name="position"/> 锚点；
        /// 区别只是这里存"中心点 + 尺寸 + 弧度"，四边形由顶点着色器从单位四边形展开。
        /// </para>
        /// </summary>
        private void AddInstance(Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color,
                                 float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth,
                                 SpriteParams spriteParams)
        {
            ComputeUv(texture, sourceRectangle, effects, out Vector2 uvTL, out Vector2 uvBR);
            Rectangle source = sourceRectangle ?? new Rectangle(0, 0, texture.Width, texture.Height);
            float w = source.Width * scale.X;
            float h = source.Height * scale.Y;

            // 中心点：origin 先按 scale 缩放；旋转时中心绕 position 转（等价于逐顶点路径绕锚点转四个角）。
            origin = origin * scale;
            float dx = w * 0.5f - origin.X;
            float dy = h * 0.5f - origin.Y;
            Vector2 center = rotation == 0f
                ? new Vector2(position.X + dx, position.Y + dy)
                : new Vector2(position.X + dx * MathF.Cos(rotation) - dy * MathF.Sin(rotation),
                              position.Y + dx * MathF.Sin(rotation) + dy * MathF.Cos(rotation));

            // 翻转（FlipX / FlipY）已经在 uvTL / uvBR 的交换里体现：UV 尺寸写成负值即可让着色器插值到正确方向。
            Vector4 uvRect = new Vector4(uvTL.X, uvTL.Y, uvBR.X - uvTL.X, uvBR.Y - uvTL.Y);

            _instances.Add(new InstanceEntry
            {
                Texture = texture,
                Data = new SpriteInstance(center, new Vector2(w, h), rotation, color, spriteParams, uvRect),
                SortKey = SortKeyFor(texture, layerDepth),
                Order = _instances.Count,
            });

            if (_sortMode == SpriteSortMode.Immediate) FlushInstances();
        }

        /// <summary>
        /// 提交实例化批次：排序（非 Deferred / Immediate 时）→ 按纹理引用相等切段 → 每段按实例上限分块 → 一次实例化 draw。
        /// <para>
        /// 一次 <c>drawElementsInstanced</c> 只能绑一张纹理，所以"按纹理切段"是有意义的；
        /// 段内实例数与缓冲容量的关系由后端决定（超出即分块，照 <see cref="ISpriteInstancer.Capacity"/>）。
        /// </para>
        /// </summary>
        private void FlushInstances()
        {
            if (_instances.Count == 0) return;

            if (_sortMode != SpriteSortMode.Deferred && _sortMode != SpriteSortMode.Immediate)
                _instances.Sort(EntryComparison);

            ISpriteInstancer? instancer = _device.Instancer;
            if (instancer is null)
            {
                // Begin 已经拦过不支持的后端（直接抛异常），这里只是兜底，避免静默丢绘制。
                _instances.Clear();
                return;
            }

            if (_instanceScratch.Length < _instances.Count)
                _instanceScratch = new SpriteInstance[Math.Max(_instances.Count, GraphicsDevice.MaxBatchSize)];

            Matrix4x4 transform = _transform * _projection;
            int start = 0;

            while (start < _instances.Count)
            {
                Texture2D texture = _instances[start].Texture;
                int end = start + 1;
                while (end < _instances.Count && ReferenceEquals(_instances[end].Texture, texture)) end++;

                for (int offset = start; offset < end; offset += instancer.Capacity)
                {
                    int count = Math.Min(instancer.Capacity, end - offset);
                    for (int i = 0; i < count; i++) _instanceScratch[i] = _instances[offset + i].Data;

                    _device.DrawInstanced(instancer, _material, transform,
                                          _instanceScratch.AsSpan(0, count), count, texture);
                }

                start = end;
            }

            _instances.Clear();
        }

        /// <summary>排序键：照 MonoGame。Texture 模式用 Texture.SortingKey（纹理内在序号），前后排序用 layerDepth。</summary>
        private float SortKeyFor(Texture2D texture, float layerDepth) => _sortMode switch
        {
            SpriteSortMode.Texture => texture.SortingKey,
            SpriteSortMode.FrontToBack => layerDepth,
            SpriteSortMode.BackToFront => -layerDepth,
            _ => 0f,
        };

        /// <summary>UV 计算（含图集 Bounds 偏移与翻转）：两条提交路径共用同一份，避免实现走偏。</summary>
        private static void ComputeUv(Texture2D texture, Rectangle? sourceRectangle, SpriteEffects effects,
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

        /// <summary>实例排序：先按排序键，键相同再按原始次序（等价于稳定排序，保持同深度精灵的相对顺序）。</summary>
        private static int CompareEntries(InstanceEntry a, InstanceEntry b)
        {
            int byKey = a.SortKey.CompareTo(b.SortKey);
            return byKey != 0 ? byKey : a.Order.CompareTo(b.Order);
        }

        /// <summary>
        /// 块里是否有「没被通道映射覆盖」的属性（矩阵、纹理，或没写进 <see cref="Material.SetSpriteChannels"/> 的属性）。
        /// 有的话这一次绘制必须挂 uniform，因而会切批。
        /// </summary>
        private bool HasUnmappedProperty(MaterialPropertyBlock block)
        {
            SpriteChannelMap map = _material.SpriteChannels!;
            foreach (var pair in block.Properties)
            {
                if (!map.IsMapped(pair.Key)) return true;
            }
            return false;
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
        /// 逐精灵参数的重载：同一批（同一个材质 + 同一个 Begin）里，每个精灵可以带自己的参数，
        /// 因为参数随顶点走、不参与分批键，所以不会像 MaterialPropertyBlock 那样按批切分 DrawCall。
        /// </summary>
        public void Draw(Texture2D texture, Vector2 position, Color color, SpriteParams spriteParams)
            => Draw(texture, position, null, color, 0f, Vector2.Zero, Vector2.One, SpriteEffects.None, 0f, spriteParams);

        public void Draw(Texture2D texture, Rectangle destination, Color color, SpriteParams spriteParams)
            => Draw(texture, destination, null, color, spriteParams);

        public void Draw(Texture2D texture, Rectangle destination, Rectangle? sourceRectangle, Color color, SpriteParams spriteParams)
        {
            ArgumentNullException.ThrowIfNull(texture);
            Draw(texture, new Vector2(destination.X, destination.Y), sourceRectangle, color,
                 0f, Vector2.Zero,
                 new Vector2(destination.Width / (float)(sourceRectangle?.Width ?? texture.Width),
                             destination.Height / (float)(sourceRectangle?.Height ?? texture.Height)),
                 SpriteEffects.None, 0f, spriteParams);
        }

        /// <summary>
        /// 属性块重载（照 Unity 的 <c>renderer.SetPropertyBlock</c>）：块挂在<b>这一次绘制</b>上，
        /// 能覆盖任意条数、任意类型的属性（float / int / 向量 / 矩阵 / 纹理），代价是块变即切批。
        /// 想让逐精灵差异参与合批，请改用 SpriteParams 重载（第 8 页）。
        /// </summary>
        public void Draw(Texture2D texture, Vector2 position, Color color, MaterialPropertyBlock? properties)
            => Draw(texture, position, null, color, 0f, Vector2.Zero, Vector2.One, SpriteEffects.None, 0f, default, properties);

        public void Draw(Texture2D texture, Rectangle destination, Color color, MaterialPropertyBlock? properties)
            => Draw(texture, destination, null, color, properties);

        public void Draw(Texture2D texture, Rectangle destination, Rectangle? sourceRectangle, Color color, MaterialPropertyBlock? properties)
        {
            ArgumentNullException.ThrowIfNull(texture);
            Draw(texture, new Vector2(destination.X, destination.Y), sourceRectangle, color,
                 0f, Vector2.Zero,
                 new Vector2(destination.Width / (float)(sourceRectangle?.Width ?? texture.Width),
                             destination.Height / (float)(sourceRectangle?.Height ?? texture.Height)),
                 SpriteEffects.None, 0f, default, properties);
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
