// KFramework.MonoGame - 照 MonoGame 的 Microsoft.Xna.Framework.Graphics.GraphicsMetrics
namespace KFramework.MonoGame
{
    /// <summary>
    /// 渲染统计快照，照 MonoGame 的 Microsoft.Xna.Framework.Graphics.GraphicsMetrics。
    /// 通过 <see cref="GraphicsDevice.Metrics"/> 读取，用于运行时调试与性能分析。
    /// 每帧由 GraphicsDevice.EndFrame 重置一次（照 MonoGame 在 Present 里重置），跨所有 SpriteBatch 批次累计。
    /// </summary>
    public struct GraphicsMetrics
    {
        internal long _clearCount;
        internal long _drawCount;
        internal long _pixelShaderCount;
        internal long _primitiveCount;
        internal long _spriteCount;
        internal long _targetCount;
        internal long _textureCount;
        internal long _vertexShaderCount;

        /// <summary>Clear 被调用的次数。</summary>
        public long ClearCount => _clearCount;

        /// <summary>Draw 被调用的次数（真正的 draw call 数）。</summary>
        public long DrawCount => _drawCount;

        /// <summary>像素着色器在 GPU 上被切换的次数。</summary>
        public long PixelShaderCount => _pixelShaderCount;

        /// <summary>顶点着色器在 GPU 上被切换的次数。</summary>
        public long VertexShaderCount => _vertexShaderCount;

        /// <summary>渲染的图元数。</summary>
        public long PrimitiveCount => _primitiveCount;

        /// <summary>经 SpriteBatch 渲染的精灵/文字字符数。</summary>
        public long SpriteCount => _spriteCount;

        /// <summary>渲染目标在 GPU 上被切换的次数。</summary>
        public long TargetCount => _targetCount;

        /// <summary>纹理在 GPU 上被切换的次数。</summary>
        public long TextureCount => _textureCount;




        /// <summary>两组 metrics 的差值。</summary>
        public static GraphicsMetrics operator -(GraphicsMetrics value1, GraphicsMetrics value2)
        {
            return new GraphicsMetrics()
            {
                _clearCount = value1._clearCount - value2._clearCount,
                _drawCount = value1._drawCount - value2._drawCount,
                _pixelShaderCount = value1._pixelShaderCount - value2._pixelShaderCount,
                _primitiveCount = value1._primitiveCount - value2._primitiveCount,
                _spriteCount = value1._spriteCount - value2._spriteCount,
                _targetCount = value1._targetCount - value2._targetCount,
                _textureCount = value1._textureCount - value2._textureCount,
                _vertexShaderCount = value1._vertexShaderCount - value2._vertexShaderCount,
            };
        }

        /// <summary>两组 metrics 的合并。</summary>
        public static GraphicsMetrics operator +(GraphicsMetrics value1, GraphicsMetrics value2)
        {
            return new GraphicsMetrics()
            {
                _clearCount = value1._clearCount + value2._clearCount,
                _drawCount = value1._drawCount + value2._drawCount,
                _pixelShaderCount = value1._pixelShaderCount + value2._pixelShaderCount,
                _primitiveCount = value1._primitiveCount + value2._primitiveCount,
                _spriteCount = value1._spriteCount + value2._spriteCount,
                _targetCount = value1._targetCount + value2._targetCount,
                _textureCount = value1._textureCount + value2._textureCount,
                _vertexShaderCount = value1._vertexShaderCount + value2._vertexShaderCount,
            };
        }

        /// <summary>
        /// 全部计数的多行汇总，供调试浮层直接显示（MonoGame 原版没有此方法，这里为便于展示而加）。
        /// 行之间用 <c>'\n'</c>：SpriteFont / KFont / BitmapFont 的 DrawString 都原生支持换行。
        /// </summary>
        public override string ToString()
            => $"Clear: {_clearCount}   Draw: {_drawCount}   Prim: {_primitiveCount}   Sprite: {_spriteCount}\n"
             + $"Tex: {_textureCount}   Target: {_targetCount}   VS: {_vertexShaderCount}   PS: {_pixelShaderCount}";
    }
}
