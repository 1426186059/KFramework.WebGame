namespace KFramework.MonoGame
{
    /// <summary>
    /// 描述一种显示模式（照 MonoGame 的 Microsoft.Xna.Framework.Graphics.DisplayMode）。
    /// <para>
    /// 与 MonoGame 的差异：① 构造函数是 public（Web 上只有「画布当前尺寸」一种模式，
    /// 无需限制为 internal）；② 没有 TitleSafeArea（本后端无电视安全区概念）。
    /// </para>
    /// </summary>
    public class DisplayMode
    {
        private readonly SurfaceFormat format;
        private readonly int height;
        private readonly int width;

        /// <summary>宽高比（宽 / 高）。</summary>
        public float AspectRatio => height == 0 ? 0f : (float)width / (float)height;

        /// <summary>该模式的像素格式。</summary>
        public SurfaceFormat Format => format;

        /// <summary>屏幕高度（像素）。</summary>
        public int Height => height;

        /// <summary>屏幕宽度（像素）。</summary>
        public int Width => width;

        /// <summary>
        /// 用指定的宽高与格式创建显示模式。
        /// </summary>
        /// <param name="width">屏幕宽度（像素）。</param>
        /// <param name="height">屏幕高度（像素）。</param>
        /// <param name="format">像素格式。</param>
        public DisplayMode(int width, int height, SurfaceFormat format)
        {
            this.width = width;
            this.height = height;
            this.format = format;
        }

        /// <summary>判断两个显示模式是否不同。</summary>
        public static bool operator !=(DisplayMode? left, DisplayMode? right) => !(left == right);

        /// <summary>判断两个显示模式是否相同（宽高与格式都相同）。</summary>
        public static bool operator ==(DisplayMode? left, DisplayMode? right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left is null || right is null) return false;
            return left.format == right.format && left.height == right.height && left.width == right.width;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is DisplayMode other && this == other;

        /// <inheritdoc />
        public override int GetHashCode() => width.GetHashCode() ^ height.GetHashCode() ^ format.GetHashCode();

        /// <inheritdoc />
        public override string ToString()
            => "{Width:" + width + " Height:" + height + " Format:" + Format + " AspectRatio:" + AspectRatio + "}";
    }
}
