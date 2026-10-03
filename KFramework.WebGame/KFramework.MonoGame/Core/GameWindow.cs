using KFramework.MonoGame;
using System.Diagnostics;


namespace KFramework.MonoGame
{

    /// <summary>浏览器画布窗口。</summary>
    public sealed class GameWindow
    {
        private readonly GraphicsDevice _device;
        public HTML_Canvas Canvas { get; }
        public string CanvasId => Canvas.Id;
        public event Action? SizeChanged;
        public event Action<bool>? FocusChanged;

        internal GameWindow(GraphicsDevice device)
        {
            _device = device;
            Canvas = GraphicsDevice.Canvas;
        }

        public void SetCanvasRect(int x, int y, int width, int height)
        { 
            Canvas.SetRect(x, y, width, height);
        }
        
        public void SetCanvasFullscreen()
        {
            Canvas.SetLayout(HTML_CanvasLayoutMode.Fullscreen, 0, 0, 0, 0);
        }
        
        public void SetCanvasCentered(int width, int height)
        { 
            Canvas.SetCentered(width, height);
        }

        public void RestoreCanvasLayout()
        {
            Canvas.RestoreLayout();
        }
        
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

        public float DevicePixelRatio => HTML_Window.DevicePixelRatio;

        public string Title
        {
            set => JSBind_Platform.SetTitle(value);
        }

        //窗口 尺寸改变
        internal void OnWindowSizeChanged()
        {
            PrintTool.Log("OnWindow Size Changed");
            SizeChanged?.Invoke();
        }
            
        //窗口 焦点改变
        internal void OnWindowFocusChanged(bool bFocus)
        {
            PrintTool.Log("OnWindow Focus Changed");
            FocusChanged?.Invoke(bFocus);
        }
    }
}
