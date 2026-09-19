using System;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 图形呈现参数（照 MonoGame 的 Microsoft.Xna.Framework.Graphics.PresentationParameters）。
    /// <para>
    /// 与 MonoGame 的差异只有两处：① 去掉了 iOS / Android 的条件编译分支；
    /// ② 默认宽高取自 MonoGame 的 <c>GraphicsDeviceManager.DefaultBackBufferWidth/Height</c>（800×480），
    /// 本仓库暂无 <c>GraphicsDeviceManager</c>，先在类内定义同名常量，等移植该类时再改为引用它。
    /// </para>
    /// </summary>
    public class PresentationParameters
    {
        #region Constants

        /// <summary>默认呈现速率（照 MonoGame 的 DefaultPresentRate）。</summary>
        public const int DefaultPresentRate = 60;

        /// <summary>默认后备缓冲宽度（取自 MonoGame 的 GraphicsDeviceManager.DefaultBackBufferWidth）。</summary>
        private const int DefaultBackBufferWidth = 800;

        /// <summary>默认后备缓冲高度（取自 MonoGame 的 GraphicsDeviceManager.DefaultBackBufferHeight）。</summary>
        private const int DefaultBackBufferHeight = 480;

        #endregion Constants

        #region Private Fields

        private DepthFormat depthStencilFormat;
        private SurfaceFormat backBufferFormat;
        private int backBufferHeight = DefaultBackBufferHeight;
        private int backBufferWidth = DefaultBackBufferWidth;
        private IntPtr deviceWindowHandle;
        private int multiSampleCount;
        private bool isFullScreen;
        private bool hardwareModeSwitch = true;

        #endregion Private Fields

        #region Constructors

        /// <summary>创建一份全部为默认值的呈现参数（照 MonoGame 的构造函数会先 Clear）。</summary>
        public PresentationParameters()
        {
            Clear();
        }

        #endregion Constructors

        #region Properties

        /// <summary>后备缓冲的像素格式。</summary>
        public SurfaceFormat BackBufferFormat
        {
            get { return backBufferFormat; }
            set { backBufferFormat = value; }
        }

        /// <summary>后备缓冲高度（像素）。</summary>
        public int BackBufferHeight
        {
            get { return backBufferHeight; }
            set { backBufferHeight = value; }
        }

        /// <summary>后备缓冲宽度（像素）。</summary>
        public int BackBufferWidth
        {
            get { return backBufferWidth; }
            set { backBufferWidth = value; }
        }

        /// <summary>后备缓冲的矩形范围（左上角恒为 0,0）。</summary>
        public Rectangle Bounds
        {
            get { return new Rectangle(0, 0, backBufferWidth, backBufferHeight); }
        }

        /// <summary>呈现后备缓冲的窗口句柄（Web 平台无意义，照 MonoGame 保留）。</summary>
        public IntPtr DeviceWindowHandle
        {
            get { return deviceWindowHandle; }
            set { deviceWindowHandle = value; }
        }

        /// <summary>后备缓冲的深度 / 模板格式。</summary>
        public DepthFormat DepthStencilFormat
        {
            get { return depthStencilFormat; }
            set { depthStencilFormat = value; }
        }

        /// <summary>是否全屏。</summary>
        public bool IsFullScreen
        {
            get { return isFullScreen; }
            set { isFullScreen = value; }
        }

        /// <summary>
        /// 为 true 时切全屏会做真正的显示模式切换；为 false 时改为「无边框最大化窗口」的软全屏。
        /// </summary>
        public bool HardwareModeSwitch
        {
            get { return hardwareModeSwitch; }
            set { hardwareModeSwitch = value; }
        }

        /// <summary>后备缓冲的多重采样数量（本后端一律按 0 处理，未移植 MSAA resolve）。</summary>
        public int MultiSampleCount
        {
            get { return multiSampleCount; }
            set { multiSampleCount = value; }
        }

        /// <summary>呈现间隔。</summary>
        public PresentInterval PresentationInterval { get; set; }

        /// <summary>屏幕方向。</summary>
        public DisplayOrientation DisplayOrientation { get; set; }

        /// <summary>
        /// 后备缓冲的内容保留策略：决定 <see cref="GraphicsDevice"/> 把画布设为渲染目标时是否清屏。
        /// 默认 <see cref="RenderTargetUsage.DiscardContents"/>（= 枚举 0），即切回画布会按
        /// <see cref="GraphicsDevice.DiscardColor"/> 清一次；设为
        /// <see cref="RenderTargetUsage.PreserveContents"/> 则保留已合成内容。
        /// </summary>
        public RenderTargetUsage RenderTargetUsage { get; set; }

        #endregion Properties

        #region Methods

        /// <summary>把所有属性重置为默认值（照 MonoGame 的 Clear）。</summary>
        public void Clear()
        {
            backBufferFormat = SurfaceFormat.Color;
            backBufferWidth = DefaultBackBufferWidth;
            backBufferHeight = DefaultBackBufferHeight;
            deviceWindowHandle = IntPtr.Zero;
            depthStencilFormat = DepthFormat.None;
            multiSampleCount = 0;
            PresentationInterval = PresentInterval.Default;
            DisplayOrientation = DisplayOrientation.Default;
        }

        /// <summary>创建一份副本（照 MonoGame 的 Clone）。</summary>
        public PresentationParameters Clone()
        {
            PresentationParameters clone = new PresentationParameters();
            clone.backBufferFormat = this.backBufferFormat;
            clone.backBufferHeight = this.backBufferHeight;
            clone.backBufferWidth = this.backBufferWidth;
            clone.deviceWindowHandle = this.deviceWindowHandle;
            clone.depthStencilFormat = this.depthStencilFormat;
            clone.IsFullScreen = this.IsFullScreen;
            clone.HardwareModeSwitch = this.HardwareModeSwitch;
            clone.multiSampleCount = this.multiSampleCount;
            clone.PresentationInterval = this.PresentationInterval;
            clone.DisplayOrientation = this.DisplayOrientation;
            clone.RenderTargetUsage = this.RenderTargetUsage;
            return clone;
        }

        #endregion Methods
    }
}
