using System;

using KFramework.MonoGame;

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
        private BlendState _blendState = BlendState.AlphaBlend;
        private SamplerState _samplerState = SamplerState.Point;
        private Matrix4x4 _transform = Matrix4x4.Identity;
        private Matrix4x4 _projection;

        private bool _beginCalled;

        // 2D 精灵默认不剔除：本后端的正交投影翻转 Y，使四边形绕序与 MonoGame 默认的 CCW 正面相反，
        // 开启背面剔除会把精灵整批剔掉。需要剔除的 3D 渲染可显式设置 GraphicsDevice.RasterizerState。
        private RasterizerState _rasterizerState = RasterizerState.CullNone;
        private DepthStencilState _depthStencilState = DepthStencilState.None;

        public SpriteBatch(GraphicsDevice device)
        {
            ArgumentNullException.ThrowIfNull(device);
            _device = device;
            _batcher = new SpriteBatcher(device);
        }

        public GraphicsDevice GraphicsDevice => _device;

        public void Begin(SpriteSortMode sortMode = SpriteSortMode.Deferred,
                          BlendState? blendState = null,
                          SamplerState? samplerState = null,
                          Matrix4x4? transformMatrix = null)
        {
            if (_beginCalled) throw new InvalidOperationException("上一次 Begin 还没有对应的 End。");

            _sortMode = sortMode;
            // 纹理数据是非预乘的，默认用 NonPremultiplied（SRC_ALPHA, ONE_MINUS_SRC_ALPHA）
            _blendState = blendState ?? BlendState.NonPremultiplied;
            _samplerState = samplerState ?? SamplerState.Point;
            _transform = transformMatrix ?? Matrix4x4.Identity;
            _rasterizerState = RasterizerState.CullNone;
            _depthStencilState = DepthStencilState.None;
            _batcher.SetSamplerState(_samplerState);

            var viewport = _device.Viewport;
            _projection = Matrix4x4.CreateOrthographicScreen(viewport.Width, viewport.Height);

            // Immediate 模式：每次 Draw 立即下发，故提前把渲染状态设好。
            if (sortMode == SpriteSortMode.Immediate) Setup();

            _beginCalled = true;
        }

        public void End()
        {
            if (!_beginCalled) throw new InvalidOperationException("End 必须在 Begin 之后调用。");
            _beginCalled = false;

            if (_sortMode != SpriteSortMode.Immediate) Setup();
            _batcher.DrawBatch(_sortMode, _device.Effect);
        }

        /// <summary>下发混合状态 + 把 (变换 × 正交投影) 写入着色器。照 MonoGame 的 Setup()。</summary>
        private void Setup()
        {
            _device.SetBlendState(_blendState);
            // 照 MonoGame 的 Setup()：把光栅化/深度状态交给 GraphicsDevice 统一管理（绘制前强制下发，
            // 不受外部 GL 状态影响），确保 2D 绘制用一致的状态：剔除逆时针背面 + 关闭深度测试。
            _device.DepthStencilState = _depthStencilState;
            _device.RasterizerState = _rasterizerState;
            // 行向量约定（p' = p × M）：变换在左、投影在右，故 transform 先发生。
            _device.Effect.Apply(_transform * _projection);
        }

        #region Draw 重载

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

            if (_sortMode == SpriteSortMode.Immediate) _batcher.DrawBatch(_sortMode, _device.Effect);
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

        #endregion

        #region 文字

        public void DrawString(SpriteFont font, string text, Vector2 position, Color color)
            => font.Draw(this, text, position, color, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

        public void DrawString(SpriteFont font, string text, Vector2 position, Color color,
                               float rotation, Vector2 origin, float scale, float layerDepth = 0f)
            => font.Draw(this, text, position, color, rotation, origin, scale, SpriteEffects.None, layerDepth);

        #endregion

        private void CheckValid(Texture2D texture)
        {
            if (texture == null) throw new ArgumentNullException(nameof(texture));
            if (!_beginCalled) throw new InvalidOperationException("Draw 必须在 Begin / End 之间调用。");
        }
    }
}
