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

        public SpriteBatch(GraphicsDevice device)
        {
            ArgumentNullException.ThrowIfNull(device);
            _device = device;
            _batcher = new SpriteBatcher(device);
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

            // 交给 SpriteBatcher 排序 + 分批提交（按纹理与属性块切段）。
            FlushBatch();
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
        /// 逐顶点路径的一次绘制：把精灵展开成 4 个顶点交给 <see cref="SpriteBatcher"/> 排队/提交。
        /// </summary>
        /// <param name="uniformBlock">需要在本段下发 uniform 的属性块（可空；非空时这一笔当场提交、切批）。</param>
        private void DrawVertexPath(Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color,
                                    float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth,
                                    ShaderPropertyBlock? uniformBlock)
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

        /// <summary>UV 计算（含图集 Bounds 偏移与翻转）。</summary>
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
