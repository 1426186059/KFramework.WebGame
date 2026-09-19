namespace KFramework.MonoGame
{
    /// <summary>
    /// 一块 HTML 画布（<c>&lt;canvas&gt;</c>）—— 与 TS 层 <c>html_canvas.ts</c> 的 <c>canvas</c> 模块一一对应的对象封装。
    /// 一个实例 = 页面上的一块画布（按 DOM id 定位），自带当前矩形 <see cref="Rect"/> 与「能设置到的最大尺寸」
    /// <see cref="MaxSize"/>，调用方不必每次重复传 id。底层转发到 <see cref="HTML_Canvas_Func"/>。
    /// <para>
    /// 所有写操作只改 CSS：后备缓冲 = CSS 尺寸 × DPR，由 <see cref="GraphicsDevice.SyncCanvasSize"/> 同步，
    /// 输入坐标也按画布 rect 自动换算。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 与 TS 导出的对应关系：create / createFullscreen / applyLayout（= SetRect / SetSize / SetCentered /
    /// SetFullscreen）/ requestFullscreen / exitFullscreen / restoreLayout / destroy / exists / getRect / getMaxSize。
    /// </remarks>
    public sealed class HTML_Canvas
    {
        private Rectangle? _rect;

        /// <summary>
        /// 用 DOM id（或 "#id" 选择器）关联一块画布。构造本身不创建 DOM 元素，
        /// 需要新建时调用 <see cref="Create(int, int, int, int)"/> 或 <see cref="CreateFullscreen"/>。
        /// </summary>
        /// <param name="idOrSelector">画布 DOM id，可写 "#id" 形式；空串回落到默认 id。</param>
        public HTML_Canvas(string idOrSelector)
        {
            Id = HTML_Canvas_Func.ToCanvasId(idOrSelector);
        }

        /// <summary>本实例对应的 DOM id（已归一化，无 "#" 前缀）。</summary>
        public string Id { get; }

        /// <summary>页面上这块画布是否已存在（含引擎曾创建过的）。</summary>
        public bool Exists => HTML_Canvas_Func.Exists(Id);

        /// <summary>
        /// 画布当前的实际矩形（相对视口左上角，单位 CSS 像素）—— 对应 HTML 里这块元素的 rect。
        /// </summary>
        /// <remarks>
        /// 值是从浏览器读回来的缓存：每次布局操作成功后会自动刷新；
        /// 若画布被页面自己的 CSS / 脚本改动过，调 <see cref="Refresh"/> 重新拉取。
        /// 画布不存在时为 <see cref="Rectangle.Empty"/>。
        /// </remarks>
        public Rectangle Rect => _rect ??= HTML_Canvas_Func.GetRect(Id) ?? Rectangle.Empty;

        /// <summary>
        /// 当前能给这块画布设置的最大 CSS 尺寸（未进原生全屏时等于视口大小）。
        /// 设得更大只会超出可见区域，所以布局前用它做 clamp 即可。
        /// </summary>
        /// <remarks><see cref="Point.X"/> = 最大宽度，<see cref="Point.Y"/> = 最大高度。</remarks>
        public Point MaxSize => HTML_Canvas_Func.GetMaxSize();

        /// <summary>重新从浏览器读回 <see cref="Rect"/>（画布被外部改动后调用）。</summary>
        public void Refresh() => _rect = HTML_Canvas_Func.GetRect(Id);

        /// <summary>创建这块画布：铺满视口。已存在时返回 false。</summary>
        public bool CreateFullscreen() => Refresh(HTML_Canvas_Func.CreateFullscreen(Id));

        /// <summary>创建这块画布：摆到 <paramref name="x"/>, <paramref name="y"/>，CSS 尺寸 width × height。已存在时返回 false。</summary>
        public bool Create(int x, int y, int width, int height)
            => Refresh(HTML_Canvas_Func.Create(Id, x, y, width, height));

        /// <summary>
        /// 【通用布局入口】一次调用表达四种摆位需求，对应 TS 的 applyLayout。
        /// 引擎内部与上层业务都可以通过它组合出任意布局，无需为每种情况新增一个 JS 导出。
        /// </summary>
        /// <param name="mode">布局方式。</param>
        /// <param name="x">左上角 X（<see cref="HTML_CanvasLayoutMode.Centered"/> / <see cref="HTML_CanvasLayoutMode.Size"/> 时忽略）。</param>
        /// <param name="y">左上角 Y（同上）。</param>
        /// <param name="width">CSS 宽度（<see cref="HTML_CanvasLayoutMode.Fullscreen"/> 时忽略）。</param>
        /// <param name="height">CSS 高度（同上）。</param>
        /// <returns>画布存在且设置成功时返回 true。</returns>
        public bool SetLayout(HTML_CanvasLayoutMode mode, int x, int y, int width, int height)
            => Refresh(HTML_Canvas_Func.ApplyLayout(Id, mode, x, y, width, height));

        /// <summary>设置画布位置与 CSS 尺寸（像素，相对视口左上角）。</summary>
        public bool SetRect(int x, int y, int width, int height)
            => SetLayout(HTML_CanvasLayoutMode.Rect, x, y, width, height);

        /// <summary>只改 CSS 尺寸，位置保持不动。</summary>
        public bool SetSize(int width, int height)
            => SetLayout(HTML_CanvasLayoutMode.Size, 0, 0, width, height);

        /// <summary>摆到浏览器视口正中；之后窗口缩放会自动重新居中。</summary>
        public bool SetCentered(int width, int height)
            => SetLayout(HTML_CanvasLayoutMode.Centered, 0, 0, width, height);

        /// <summary>铺满视口（软全屏，不改显示模式）。</summary>
        public bool SetFullscreen() => SetLayout(HTML_CanvasLayoutMode.Fullscreen, 0, 0, 0, 0);

        /// <summary>
        /// 请求浏览器原生全屏（<c>canvas.requestFullscreen</c>）。
        /// 需用户手势，被拒绝时自动回落到铺满视口。
        /// </summary>
        public bool RequestFullscreen() => Refresh(HTML_Canvas_Func.RequestFullscreen(Id));

        /// <summary>退出浏览器原生全屏（document 级动作，与具体画布无关）。</summary>
        public void ExitFullscreen()
        {
            HTML_Canvas_Func.ExitFullscreen();
            Refresh();
        }

        /// <summary>撤销引擎写在这块画布上的行内样式，恢复页面自身布局，并解除居中跟随。</summary>
        public bool RestoreLayout() => Refresh(HTML_Canvas_Func.RestoreLayout(Id));

        /// <summary>从 DOM 移除这块画布。</summary>
        public bool Destroy()
        {
            bool ok = HTML_Canvas_Func.Destroy(Id);
            if (ok) _rect = null;
            return ok;
        }

        /// <summary>写操作成功后顺带回读一次真实矩形，保证 <see cref="Rect"/> 与浏览器一致。</summary>
        private bool Refresh(bool ok)
        {
            if (ok) Refresh();
            return ok;
        }

        /// <inheritdoc />
        public override string ToString() => $"HTML_Canvas(id={Id}, rect={Rect})";
    }
}
