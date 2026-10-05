namespace KFramework.MonoGame
{
    /// <summary>
    /// 2D 精灵批处理器。用法与 MonoGame 一致：Begin() → 若干 Draw() → End()。
    /// 合批逻辑全部委托给 <see cref="SpriteBatcher"/>（照 MonoGame 的 SpriteBatch / SpriteBatcher 拆分）。
    /// 同一张纹理（用 source rect 绘制的图集页）会被合并为一次 draw call；分组按引用相等判断。
    /// </summary>
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

        /// <summary>
        /// 用材质配置开启一批绘制。材质打包了着色器 + 混合/采样/深度/剔除状态，
        /// GraphicsDevice 按「材质内容 + 变换」做去重，相同配置不再重复下发跨 JS 状态。
        /// </summary>
        public void Begin(Material material, SpriteSortMode sortMode = SpriteSortMode.Deferred,
                          Matrix4x4? transformMatrix = null)
        {
            BeginInternal(material, sortMode, transformMatrix);
        }

        /// <summary>
        /// 兼容旧签名的重载：把散装的 Blend / Sampler / DepthStencil / Rasterizer 状态包成一个默认材质
        /// （Effect 用设备默认精灵着色器）。参数对齐原版 MonoGame SpriteBatch.Begin 的散装签名。
        /// 新增代码建议直接用 <see cref="Begin(Material, SpriteSortMode, Matrix4x4?)"/>。
        /// </summary>
        public void Begin(SpriteSortMode sortMode = SpriteSortMode.Deferred,
                          BlendState? blendState = null,
                          SamplerState? samplerState = null,
                          DepthStencilState? depthStencilState = null,
                          RasterizerState? rasterizerState = null,
                          Matrix4x4? transformMatrix = null)
        {
            // 复用本批材质快照 _cache_Mat（避免每次 Begin 都 new），填好参数后交给 BeginInternal。
            _cache_Mat.Reset();
            _cache_Mat.Effect = null;
            _cache_Mat.Blend = blendState ?? BlendState.NonPremultiplied;
            _cache_Mat.Sampler = samplerState ?? SamplerState.Point;
            _cache_Mat.DepthStencil = depthStencilState ?? DepthStencilState.None;
            _cache_Mat.Rasterizer = rasterizerState ?? RasterizerState.CullNone;
            BeginInternal(_cache_Mat, sortMode, transformMatrix);
        }

        private void BeginInternal(Material material, SpriteSortMode sortMode, Matrix4x4? transformMatrix)
        {
            if (_beginCalled) throw new InvalidOperationException("上一次 Begin 还没有对应的 End。");

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

            // Immediate 模式：每次 Draw 立即下发，故提前把渲染状态设好。
            if (sortMode == SpriteSortMode.Immediate) Setup();

            _beginCalled = true;
        }

        public void End()
        {
            if (!_beginCalled) throw new InvalidOperationException("End 必须在 Begin 之后调用。");
            _beginCalled = false;

            if (_sortMode != SpriteSortMode.Immediate) Setup();
            _batcher.DrawBatch(_sortMode, _material.Effect ?? _device.Effect);
        }

        /// <summary>下发混合/深度/剔除/采样状态 + 把 (变换 × 正交投影) 写入着色器。照 MonoGame 的 Setup()。</summary>
        private void Setup()
        {
            // 材质级去重：相同材质 + 相同变换时，GraphicsDevice 内部整体跳过状态下发与矩阵上传。
            _device.ApplyMaterial(_material, _transform * _projection);
        }


        public void Draw(Texture2D texture, Vector2 position, Color color)
            => Draw(texture, position, null, color, 0f, Vector2.Zero, Vector2.One, SpriteEffects.None, 0f);

        public void Draw(Texture2D texture, Vector2 position, Color color, float rotation, Vector2 origin, float scale, float layerDepth = 0f)
            => Draw(texture, position, null, color, rotation, origin, new Vector2(scale, scale), SpriteEffects.None, layerDepth);

        public void Draw(Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color)
            => Draw(texture, position, sourceRectangle, color, 0f, Vector2.Zero, Vector2.One, SpriteEffects.None, 0f);

        /// <summary>完整参数的绘制（照 MonoGame 的 Draw，UV 计算兼容本引擎的图集 Bounds 偏移）。</summary>
        public void Draw(Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color,
                         float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth = 0f)
        {
            CheckValid(texture);

            SpriteBatchItem item = _batcher.CreateBatchItem();
            item.Texture = texture;

            // 排序键：照 MonoGame。Texture 排序模式用 Texture.SortingKey（纹理内在序号）。
            switch (_sortMode)
            {
                case SpriteSortMode.Texture:
                    item.SortKey = texture.SortingKey;
                    break;
                case SpriteSortMode.FrontToBack:
                    item.SortKey = layerDepth;
                    break;
                case SpriteSortMode.BackToFront:
                    item.SortKey = -layerDepth;
                    break;
                default:
                    item.SortKey = 0;
                    break;
            }

            Rectangle source = sourceRectangle ?? new Rectangle(0, 0, texture.Width, texture.Height);
            float w = source.Width * scale.X;
            float h = source.Height * scale.Y;

            Vector2 uvTL = new Vector2((texture.Bounds.X + source.X) / (float)texture.TextureWidth,
                                       (texture.Bounds.Y + source.Y) / (float)texture.TextureHeight);
            Vector2 uvBR = new Vector2((texture.Bounds.X + source.X + source.Width) / (float)texture.TextureWidth,
                                       (texture.Bounds.Y + source.Y + source.Height) / (float)texture.TextureHeight);

            if ((effects & SpriteEffects.FlipVertically) != 0) (uvTL.Y, uvBR.Y) = (uvBR.Y, uvTL.Y);
            if ((effects & SpriteEffects.FlipHorizontally) != 0) (uvTL.X, uvBR.X) = (uvBR.X, uvTL.X);

            // 照 MonoGame：origin 先乘以 scale，再参与定位。
            origin = origin * scale;
            float c = MathF.Cos(rotation), s = MathF.Sin(rotation);

            if (rotation == 0f)
                item.Set(position.X - origin.X, position.Y - origin.Y, w, h, color, uvTL, uvBR, layerDepth);
            else
                item.Set(position.X, position.Y, -origin.X, -origin.Y, w, h, s, c, color, uvTL, uvBR, layerDepth);

            if (_sortMode == SpriteSortMode.Immediate) _batcher.DrawBatch(_sortMode, _material.Effect ?? _device.Effect);
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
