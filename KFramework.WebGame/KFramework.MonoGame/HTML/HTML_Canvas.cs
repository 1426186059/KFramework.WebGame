using System;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 一块 HTML 画布（<c>&lt;canvas&gt;</c>）—— 与 TS 层 <c>html_canvas.ts</c> 的 <c>canvas</c> 模块一一对应的对象封装。
    /// 一个实例 = 页面上的一块画布（按 DOM id 定位），自带当前矩形 <see cref="Rect"/>，调用方不必每次重复传 id。底层直接转发到 <see cref="JSBind_HTML_Canvas"/>。
    /// <para>
    /// 所有写操作只改 CSS：后备缓冲 = CSS 尺寸 × DPR，由 <see cref="GraphicsDevice.SyncCanvasSize"/> 同步，
    /// 输入坐标也按画布 rect 自动换算。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 与 TS 导出的对应关系：create / applyLayout / restoreLayout / destroy / exists / getRect / getHTMLPageSize，
    /// 全部经 <see cref="JSBind_HTML_Canvas"/> 的 [JSImport] 直连，本类不再经 <see cref="HTML_Canvas_Func"/> 中转
    /// （<see cref="HTML_Canvas_Func"/> 现在只保留「建」与「销」两块画布的生命周期入口）。
    /// </remarks>
    public sealed class HTML_Canvas
    {
        public enum LayoutMode
        {
            /// <summary>按 (x, y, width, height) 摆放；不再跟随窗口居中。</summary>
            Rect = 0,

            /// <summary>只改 CSS 尺寸，保留当前位置（忽略 x / y）。</summary>
            Size = 1,

            /// <summary>按窗口居中（忽略 x / y），之后浏览器缩放会自动重新居中。</summary>
            Centered = 2,

            /// <summary>填满整个 HTML 页面（忽略全部尺寸参数），并解除居中模式。</summary>
            Fullscreen = 3,
        }

        public const string DefaultCanvasId = "game";
        public static HTML_Canvas Current = null;
        public Point CssSize;
        public Point DrawSize;
        public string Id { get; }

        public HTML_Canvas(string idOrSelector = DefaultCanvasId)
        {
            if(Current != null)
            {
                throw new InvalidOperationException("HTML_Canvas.Current 已存在，不能重复创建。");
            }

            Id = ToCanvasId(idOrSelector);
            if (!Create(LayoutMode.Fullscreen, 0, 0, 0, 0))
            {
                throw new Exception("创建Canvas失败");
            }
            SyncJSInfo();
            Current = this;
        }

        public static string ToCanvasId(string idOrSelector)
        {
            string trimmed = (idOrSelector ?? string.Empty).Trim();
            if (trimmed.Length > 0 && trimmed[0] == '#') trimmed = trimmed[1..];
            return trimmed.Length > 0 ? trimmed : DefaultCanvasId;
        }

        /// <summary>
        /// 画布当前的实际矩形（相对窗口左上角，单位 CSS 像素）—— 对应 HTML 里这块元素的 rect。
        /// </summary>
        /// <remarks>
        /// 值是从浏览器读回来的缓存：每次布局操作成功后会自动刷新；
        /// 若画布被页面自己的 CSS / 脚本改动过，调 <see cref="Refresh"/> 重新拉取。
        /// 画布不存在时为 <see cref="Rectangle.Empty"/>。
        /// </remarks>
        public Rectangle Rect => ReadRect();

        /// <summary>创建这块画布：按 <paramref name="mode"/> 布局（Rect 摆位 / Centered 居中 / Fullscreen 填满整个 HTML 页面 / Size 仅设尺寸）。已存在时返回 false。</summary>
        public bool Create(LayoutMode mode, int x, int y, int width, int height)
        { 
            return JSBind_HTML_Canvas.Create(Id, (int)mode, x, y, width, height);
        }

        /// <summary>
        /// 【通用布局入口】一次调用表达四种摆位需求，对应 TS 的 applyLayout。
        /// 引擎内部与上层业务都可以通过它组合出任意布局，无需为每种情况新增一个 JS 导出。
        /// </summary>
        /// <param name="mode">布局方式。</param>
        /// <param name="x">左上角 X（<see cref="LayoutMode.Centered"/> / <see cref="LayoutMode.Size"/> 时忽略）。</param>
        /// <param name="y">左上角 Y（同上）。</param>
        /// <param name="width">CSS 宽度（<see cref="LayoutMode.Fullscreen"/> 时忽略）。</param>
        /// <param name="height">CSS 高度（同上）。</param>
        /// <returns>画布存在且设置成功时返回 true。</returns>
        public bool SetLayout(LayoutMode mode, int x, int y, int width, int height)
            => JSBind_HTML_Canvas.ApplyLayout((int)mode, x, y, width, height);

        /// <summary>设置画布位置与 CSS 尺寸（像素，相对窗口左上角）。</summary>
        public bool SetRect(int x, int y, int width, int height)
            => SetLayout(LayoutMode.Rect, x, y, width, height);

        /// <summary>只改 CSS 尺寸，位置保持不动。</summary>
        public bool SetSize(int width, int height)
            => SetLayout(LayoutMode.Size, 0, 0, width, height);

        /// <summary>摆到浏览器窗口正中；之后窗口缩放会自动重新居中。</summary>
        public bool SetCentered(int width, int height)
            => SetLayout(LayoutMode.Centered, 0, 0, width, height);

        /// <summary>撤销引擎写在这块画布上的行内样式，恢复页面自身布局，并解除居中跟随。</summary>
        public bool RestoreLayout() => JSBind_HTML_Canvas.RestoreLayout();

        /// <summary>从浏览器读回画布矩形；画布不存在或尺寸非法时返回 <c>null</c>。</summary>
        private Rectangle ReadRect()
        {
            Span<int> view = stackalloc int[4];
            JSBind_HTML_Canvas.GetRect(view);
            return new Rectangle(view[0], view[1], view[2], view[3]);
        }

        /// <inheritdoc />
        public override string ToString() => $"HTML_Canvas(id={Id}, rect={Rect})";

        /// <summary>
        /// 从浏览器同步一次画布尺寸信息（CSS 尺寸 / 后备缓冲尺寸 / DPR），并刷新 <see cref="CssSize"/>、<see cref="DrawSize"/> 与 <see cref="HTML_Window.DevicePixelRatio"/>。
        /// 底层走 <see cref="JSBind_HTML_Canvas.SyncJSCanvasInfo"/>（对应 TS 的 <c>SyncJSCanvasInfo</c>）。
        /// </summary>
        public void SyncJSInfo()
        {
            Span<int> view = stackalloc int[5];
            JSBind_HTML_Canvas.SyncJSCanvasInfo(view);
            UpdateInfo(view[0], view[1], view[2], view[3], view[4]);
        }

        public void UpdateInfo(int cssWidth, int cssHeight, int backingWidth, int backingHeight, int dpr1000)
        {
            this.CssSize.X = cssWidth;
            this.CssSize.Y = cssHeight;
            this.DrawSize.X = backingWidth;
            this.DrawSize.Y = backingHeight;
            HTML_Window.DevicePixelRatio = dpr1000 / 1000.0f;
        }
    }
}
