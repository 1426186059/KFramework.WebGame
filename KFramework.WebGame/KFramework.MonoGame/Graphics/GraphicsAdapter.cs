using System.Collections.ObjectModel;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 图形适配器（照 MonoGame 的 Microsoft.Xna.Framework.Graphics.GraphicsAdapter）。
    /// <para>
    /// Web 上的差异：浏览器不暴露显卡 / 显示器枚举能力，故本实现只有一个「默认适配器」，
    /// 其当前显示模式直接取自画布的绘制缓冲尺寸（CSS 尺寸 × DPR）：
    /// <c>Adapters</c> 恒为 1 项，<c>MonitorHandle / VendorId / DeviceId</c> 等平台专有属性未移植，
    /// <c>IsProfileSupported</c> 恒为 true（WebGL2 能力集固定）。
    /// </para>
    /// </summary>
    public sealed class GraphicsAdapter
    {
        private static readonly GraphicsAdapter[] AdaptersArray = { new GraphicsAdapter() };

        private static readonly ReadOnlyCollection<GraphicsAdapter> AdaptersCollection = new(AdaptersArray);

        private GraphicsAdapter()
        {
            Description = "WebGL 2.0 (Browser Canvas)";
            DeviceName = "Browser Canvas";
            IsDefaultAdapter = true;
        }

        /// <summary>默认适配器（Web 上即唯一的画布适配器）。</summary>
        public static GraphicsAdapter DefaultAdapter => AdaptersArray[0];

        /// <summary>系统可用适配器集合（Web 上恒为 1 项）。</summary>
        public static ReadOnlyCollection<GraphicsAdapter> Adapters => AdaptersCollection;

        /// <summary>给用户看的适配器描述。</summary>
        public string Description { get; }

        /// <summary>设备名。</summary>
        public string DeviceName { get; }

        /// <summary>是否为默认适配器（Web 上恒为 true）。</summary>
        public bool IsDefaultAdapter { get; }

        /// <summary>
        /// 当前显示模式，取自画布的绘制缓冲尺寸（照 MonoGame 的 CurrentDisplayMode）。
        /// </summary>
        public DisplayMode CurrentDisplayMode
        {
            get
            {
                Span<int> size = stackalloc int[5];
                JSBind_Platform.GetCanvasSize(size);
                int width = size[2];
                int height = size[3];
                if (width <= 0 || height <= 0)
                {
                    width = GraphicsDeviceManager.DefaultBackBufferWidth;
                    height = GraphicsDeviceManager.DefaultBackBufferHeight;
                }
                return new DisplayMode(width, height, SurfaceFormat.Color);
            }
        }

        /// <summary>当前显示模式是否为宽屏（照 MonoGame：宽高比 ≥ 16:10 视为宽屏）。</summary>
        public bool IsWideScreen => CurrentDisplayMode.AspectRatio >= 16.0f / 10.0f;

        /// <summary>
        /// 查询适配器是否支持指定能力等级（照 MonoGame 的 IsProfileSupported）。
        /// Web 上能力集由 WebGL2 固定，恒为 true。
        /// </summary>
        /// <param name="graphicsProfile">要查询的图形能力等级。</param>
        public bool IsProfileSupported(GraphicsProfile graphicsProfile) => true;

        /// <summary>
        /// 仅为 XNA 兼容性保留（照 MonoGame：本类不持有非托管资源，什么都不做）。
        /// </summary>
        public void Dispose()
        {
        }
    }
}
