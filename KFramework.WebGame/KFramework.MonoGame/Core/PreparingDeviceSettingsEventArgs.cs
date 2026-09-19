namespace KFramework.MonoGame
{
    /// <summary>
    /// <see cref="GraphicsDeviceManager.PreparingDeviceSettings"/> 事件的参数（照 MonoGame 的 Microsoft.Xna.Framework.PreparingDeviceSettingsEventArgs）。
    /// </summary>
    public class PreparingDeviceSettingsEventArgs : EventArgs
    {
        /// <summary>
        /// 创建事件实例。
        /// </summary>
        /// <param name="graphicsDeviceInformation">创建设备所用的默认设置。</param>
        public PreparingDeviceSettingsEventArgs(GraphicsDeviceInformation graphicsDeviceInformation)
        {
            GraphicsDeviceInformation = graphicsDeviceInformation;
        }

        /// <summary>
        /// 将用于创建设备的默认设置（可在事件里改写）。
        /// </summary>
        public GraphicsDeviceInformation GraphicsDeviceInformation { get; }
    }
}
