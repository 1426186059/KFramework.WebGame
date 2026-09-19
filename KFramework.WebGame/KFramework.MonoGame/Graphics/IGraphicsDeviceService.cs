namespace KFramework.MonoGame
{
    /// <summary>
    /// <see cref="GraphicsDevice"/> 的提供者（照 MonoGame 的 Microsoft.Xna.Framework.Graphics.IGraphicsDeviceService）。
    /// </summary>
    public interface IGraphicsDeviceService
    {
        /// <summary>
        /// 所提供的 <see cref="GraphicsDevice"/>。
        /// </summary>
        GraphicsDevice GraphicsDevice { get; }

        /// <summary>
        /// 创建了新的 <see cref="GraphicsDevice"/> 时触发。
        /// </summary>
        event EventHandler<EventArgs>? DeviceCreated;

        /// <summary>
        /// <see cref="GraphicsDevice"/> 被释放时触发。
        /// </summary>
        event EventHandler<EventArgs>? DeviceDisposing;

        /// <summary>
        /// <see cref="GraphicsDevice"/> 已重置后触发。
        /// </summary>
        event EventHandler<EventArgs>? DeviceReset;

        /// <summary>
        /// <see cref="GraphicsDevice"/> 即将重置前触发。
        /// </summary>
        event EventHandler<EventArgs>? DeviceResetting;
    }
}
