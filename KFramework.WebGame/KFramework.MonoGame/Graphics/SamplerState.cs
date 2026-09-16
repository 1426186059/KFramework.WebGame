namespace KFramework.MonoGame
{
    /// <summary>纹理采样方式。</summary>
    public sealed class SamplerState
    {
        public readonly int MinFilter;
        public readonly int MagFilter;
        public readonly int WrapMode;

        private SamplerState(int minFilter, int magFilter, int wrapMode)
        {
            MinFilter = minFilter; MagFilter = magFilter; WrapMode = wrapMode;
        }

        /// <summary>最近邻采样，像素风游戏的首选，放大后不模糊。</summary>
        public static readonly SamplerState Point =
            new(JSBind_GL.NEAREST, JSBind_GL.NEAREST, JSBind_GL.CLAMP_TO_EDGE);

        /// <summary>双线性采样，适合需要平滑缩放的美术风格。</summary>
        public static readonly SamplerState Linear =
            new(JSBind_GL.LINEAR, JSBind_GL.LINEAR, JSBind_GL.CLAMP_TO_EDGE);

        /// <summary>线性采样 + 平铺，用于背景滚动。</summary>
        public static readonly SamplerState LinearWrap =
            new(JSBind_GL.LINEAR, JSBind_GL.LINEAR, JSBind_GL.REPEAT);

        /// <summary>点采样 + 边缘钳制（MonoGame 命名，等价于 <see cref="Point"/>）。</summary>
        public static readonly SamplerState PointClamp = Point;
    }
}
