// KFramework.MonoGame - 照 MonoGame 的 Microsoft.Xna.Framework.Graphics.GraphicsMetrics
namespace KFramework.MonoGame
{
    /// <summary>
    /// 渲染统计快照，照 MonoGame 的 Microsoft.Xna.Framework.Graphics.GraphicsMetrics。
    /// 通过 <see cref="GraphicsDevice.Metrics"/> 读取，用于运行时调试与性能分析。
    /// 每帧由 GraphicsDevice.Clear 重置一次（照 MonoGame 在 Present 里重置），跨所有 SpriteBatch 批次累计。
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

        // —— 帧时间分解（时间戳字段，单位 Stopwatch ticks；照 MonoGame Metrics 之外额外补充）——
        /// <summary>所有 DrawUserIndexedPrimitives 内部「顶点上传 + 绘制（含 C#→JS 跨界 + JS 侧 WebGL 执行）」耗时的累计 ticks。</summary>
        internal long _drawSubmitTicks;
        /// <summary>整段 Draw（C# 计算 + 所有跨界调用）耗时的累计 ticks，由 Game 测量后写入。</summary>
        internal long _frameDrawTicks;
        /// <summary>整帧（Update + Draw + EndFrame + 所有跨界）CPU 侧耗时的累计 ticks，由 Game 测量后写入。</summary>
        internal long _frameTotalTicks;

        /// <summary>Clear 被调用的次数。</summary>
        public long ClearCount => _clearCount;

        /// <summary>Draw 被调用的次数（真正的 draw call 数）。</summary>
        public long DrawCount => _drawCount;

        /// <summary>像素着色器在 GPU 上被切换的次数。</summary>
        public long PixelShaderCount => _pixelShaderCount;

        /// <summary>渲染的图元数。</summary>
        public long PrimitiveCount => _primitiveCount;

        /// <summary>经 SpriteBatch 渲染的精灵/文字字符数。</summary>
        public long SpriteCount => _spriteCount;

        /// <summary>渲染目标在 GPU 上被切换的次数。</summary>
        public long TargetCount => _targetCount;

        /// <summary>纹理在 GPU 上被切换的次数。</summary>
        public long TextureCount => _textureCount;

        /// <summary>顶点着色器在 GPU 上被切换的次数。</summary>
        public long VertexShaderCount => _vertexShaderCount;

        /// <summary>顶点上传 + 绘制（含跨界 + JS WebGL 执行）耗时（毫秒）。</summary>
        public double DrawSubmitMilliseconds => ToMilliseconds(_drawSubmitTicks);
        /// <summary>整段 Draw（C# 计算 + 所有跨界调用）耗时（毫秒）。</summary>
        public double FrameDrawMilliseconds => ToMilliseconds(_frameDrawTicks);
        /// <summary>整帧 CPU 耗时（毫秒）。与 16.7ms（60fps 预算）对比即可判断是否 CPU 瓶颈。</summary>
        public double FrameTotalMilliseconds => ToMilliseconds(_frameTotalTicks);

        private static double ToMilliseconds(long ticks)
            => ticks / (double)System.Diagnostics.Stopwatch.Frequency * 1000.0;

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
                _drawSubmitTicks = value1._drawSubmitTicks - value2._drawSubmitTicks,
                _frameDrawTicks = value1._frameDrawTicks - value2._frameDrawTicks,
                _frameTotalTicks = value1._frameTotalTicks - value2._frameTotalTicks
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
                _drawSubmitTicks = value1._drawSubmitTicks + value2._drawSubmitTicks,
                _frameDrawTicks = value1._frameDrawTicks + value2._frameDrawTicks,
                _frameTotalTicks = value1._frameTotalTicks + value2._frameTotalTicks
            };
        }
    }
}
