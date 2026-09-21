using KFramework.MonoGame;


namespace KFramework.MonoGame
{

    /// <summary>浏览器画布窗口。</summary>
    public sealed class GameWindow
    {
        private readonly GraphicsDevice _device;

        internal GameWindow(GraphicsDevice device)
        {
            _device = device;
            Canvas = new HTML_Canvas(GraphicsDevice.CanvasId);
        }

        /// <summary>本窗口（画布）的 DOM id，画布管理相关调用都用它定位。</summary>
        public string CanvasId => Canvas.Id;

        /// <summary>
        /// 本窗口那块画布本身（与 TS 层 <c>html_canvas.ts</c> 一一对应）。
        /// 想做 <see cref="HTML_Canvas_Func"/> 里没有的组合操作，直接拿它即可。
        /// </summary>
        public HTML_Canvas Canvas { get; }

        /// <summary>
        /// 设置画布在页面中的位置与 CSS 尺寸（单位：CSS 像素，坐标相对视口左上角）。
        /// </summary>
        /// <remarks>
        /// 只改 CSS：后备缓冲尺寸、渲染视口与输入坐标会在下一帧自动跟上（见 <see cref="GraphicsDevice.SyncCanvasSize"/>）。
        /// 频繁改动会导致画布反复重建 backing buffer，别放在每帧的 Update / Draw 里调。
        /// </remarks>
        /// <returns>画布存在且设置成功时返回 true。</returns>
        public bool SetCanvasRect(int x, int y, int width, int height)
            => SyncAfterCanvasChange(Canvas.SetRect(x, y, width, height));

        /// <summary>把画布恢复为填满整个 HTML 页面（启动时的默认状态），不改显示模式。</summary>
        /// <returns>画布存在且设置成功时返回 true。</returns>
        public bool SetCanvasFullscreen() => SyncAfterCanvasChange(Canvas.SetLayout(HTML_CanvasLayoutMode.Fullscreen, 0, 0, 0, 0));

        /// <summary>把画布摆到浏览器视口正中（给定 CSS 尺寸），并在浏览器缩放时自动保持居中。</summary>
        /// <remarks>等价于 <see cref="HTML_Canvas.SetCentered(int, int)"/>，作用在窗口自己的画布上。</remarks>
        public bool SetCanvasCentered(int width, int height)
            => SyncAfterCanvasChange(Canvas.SetCentered(width, height));

        /// <summary>撤销引擎写在画布上的行内样式，恢复页面自身给画布的布局（如 <c>width:100%</c>），
        /// 之后画布重新随浏览器缩放。
        /// </summary>
        public bool RestoreCanvasLayout() => SyncAfterCanvasChange(Canvas.RestoreLayout());

        /// <summary>
        /// 浏览器视口尺寸（window.innerWidth / innerHeight，CSS 像素）。布局计算请用这个，而不是画布尺寸。
        /// </summary>
        /// <remarks><see cref="Point.X"/> = 视口宽，<see cref="Point.Y"/> = 视口高。</remarks>
        public Point HTMLPageSize
        {
            get
            {
                Span<int> view = stackalloc int[2];
                JSBind_HTML_Canvas.GetHTMLPageSize(view);
                return new Point(view[0], view[1]);
            }
        }

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

        /// <summary>
        /// 画布改动后把后备缓冲与视口同步到新尺寸（不用等下一帧），并把 JS 侧的结果透传回去。
        /// 只有尺寸真的变了才触发 <see cref="SizeChanged"/>，与 Game.TickFrame 的处理保持一致。
        /// </summary>
        private bool SyncAfterCanvasChange(bool ok)
        {
            if (ok && _device.SyncCanvasSize())
                RaiseSizeChanged();
            return ok;
        }
    }
}
