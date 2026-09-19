namespace KFramework.MonoGame
{
    /// <summary>
    /// 创建图形设备所用的设置（照 MonoGame 的 Microsoft.Xna.Framework.GraphicsDeviceInformation）。
    /// 通过 <see cref="GraphicsDeviceManager.PreparingDeviceSettings"/> 事件暴露给用户改写。
    /// </summary>
    public class GraphicsDeviceInformation
    {
        /// <summary>
        /// 创建设备所用的图形适配器。
        /// </summary>
        /// <remarks>Web 上只有唯一适配器，默认取 <see cref="GraphicsAdapter.DefaultAdapter"/>。</remarks>
        public GraphicsAdapter Adapter { get; set; } = GraphicsAdapter.DefaultAdapter;

        /// <summary>
        /// 请求的图形能力等级。
        /// </summary>
        public GraphicsProfile GraphicsProfile { get; set; }

        /// <summary>
        /// 定义如何把画面呈现到显示设备的参数。
        /// </summary>
        public PresentationParameters PresentationParameters { get; set; } = new PresentationParameters();
    }
}
