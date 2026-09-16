namespace KFramework.MonoGame
{
    /// <summary>背面剔除模式（照 MonoGame 的 CullMode）。</summary>
    public enum CullMode
    {
        /// <summary>不剔除。</summary>
        None = 0,
        /// <summary>剔除顺时针背面（即保留逆时针正面）。</summary>
        CullClockwiseFace = 1,
        /// <summary>剔除逆时针背面（即保留顺时针正面，MonoGame / SpriteBatch 默认）。</summary>
        CullCounterClockwiseFace = 2,
    }
}
