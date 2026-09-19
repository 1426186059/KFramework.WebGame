using KFramework.MonoGame;


namespace KFramework.MonoGame
{

    /// <summary>浏览器画布窗口。</summary>
    public sealed class GameWindow
    {
        private readonly GraphicsDevice _device;
        private readonly string _canvasId;

        internal GameWindow(GraphicsDevice device)
        {
            _device = device;
            _canvasId = device.CanvasId;
        }

        /// <summary>本窗口（画布）的 DOM id，画布管理相关调用都用它定位。</summary>
        public string CanvasId => _canvasId;

        /// <summary>
        /// 设置画布在页面中的位置与 CSS 尺寸（单位：CSS 像素，坐标相对视口左上角）。
        /// </summary>
        /// <remarks>
        /// 只改 CSS：后备缓冲尺寸、渲染视口与输入坐标会在下一帧自动跟上（见 <see cref="GraphicsDevice.SyncCanvasSize"/>）。
        /// 频繁改动会导致画布反复重建 backing buffer，别放在每帧的 Update / Draw 里调。
        /// </remarks>
        /// <returns>画布存在且设置成功时返回 true。</returns>
        public bool SetCanvasRect(int x, int y, int width, int height) => Html5Canvas.SetRect(_canvasId, x, y, width, height);

        /// <summary>把画布恢复为铺满视口（启动时的默认状态）。</summary>
        /// <returns>画布存在且设置成功时返回 true。</returns>
        public bool SetCanvasFullscreen() => Html5Canvas.SetFullscreen(_canvasId);

        /// <summary>绘制缓冲宽度（物理像素，已含设备像素比）。</summary>
        public int Width => _device.Viewport.Width;

        /// <summary>绘制缓冲高度（物理像素，已含设备像素比）。</summary>
        public int Height => _device.Viewport.Height;

        public Vector2 Size => new(Width, Height);

        public float DevicePixelRatio => _device.DevicePixelRatio;

        /// <summary>CSS 像素尺寸。输入上报的坐标已经是后备缓冲像素，本属性仅供需要 CSS 语义的场合。</summary>
        public Vector2 CssSize => _device.CssSize;

        public string Title
        {
            set => JSBind_Platform.SetTitle(value);
        }

        /// <summary>画布尺寸变化时触发（含浏览器缩放、手机旋转）。</summary>
        public event Action? SizeChanged;

        internal void RaiseSizeChanged() => SizeChanged?.Invoke();
    }
}
