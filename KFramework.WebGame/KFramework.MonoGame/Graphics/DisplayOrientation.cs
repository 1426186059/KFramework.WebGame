namespace KFramework.MonoGame
{
    /// <summary>
    /// 定义屏幕方向（照 MonoGame 的 Microsoft.Xna.Framework.DisplayOrientation）。
    /// </summary>
    [System.Flags]
    public enum DisplayOrientation
    {
        /// <summary>默认方向。</summary>
        Default = 0,
        /// <summary>逆时针旋转到横屏，宽大于高。</summary>
        LandscapeLeft = 1,
        /// <summary>顺时针旋转到横屏，宽大于高。</summary>
        LandscapeRight = 2,
        /// <summary>竖屏，高大于宽。</summary>
        Portrait = 4,
        /// <summary>倒置竖屏。</summary>
        PortraitDown = 8,
        /// <summary>未知方向。</summary>
        Unknown = 16,
    }
}
