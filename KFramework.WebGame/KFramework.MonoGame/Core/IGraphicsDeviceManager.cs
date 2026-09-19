namespace KFramework.MonoGame
{
    /// <summary>
    /// 供宿主（<see cref="Game"/>）控制图形设备的内部接口（照 MonoGame 的 Microsoft.Xna.Framework.IGraphicsDeviceManager）。
    /// </summary>
    public interface IGraphicsDeviceManager
    {
        /// <summary>
        /// 在一帧渲染开始时调用。
        /// </summary>
        /// <returns>返回 true 表示本帧应当渲染。</returns>
        bool BeginDraw();

        /// <summary>
        /// 创建图形设备。
        /// </summary>
        /// <remarks>设备已创建时什么都不做。</remarks>
        void CreateDevice();

        /// <summary>
        /// 渲染完成后调用，把本帧呈现到屏幕。
        /// </summary>
        void EndDraw();
    }
}
