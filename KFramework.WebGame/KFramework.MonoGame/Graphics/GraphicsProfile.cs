namespace KFramework.MonoGame
{
    /// <summary>
    /// 定义一组图形能力等级（照 MonoGame 的 Microsoft.Xna.Framework.Graphics.GraphicsProfile）。
    /// </summary>
    public enum GraphicsProfile
    {
        /// <summary>
        /// 只使用有限的图形特性，以适配尽可能多的设备。
        /// </summary>
        Reach,

        /// <summary>
        /// 使用完整的图形特性，面向能力更强的设备。
        /// </summary>
        HiDef,
    }
}
