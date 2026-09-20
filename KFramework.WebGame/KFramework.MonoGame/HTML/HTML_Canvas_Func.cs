namespace KFramework.MonoGame
{
    /// <summary>
    /// 画布 (<c>&lt;canvas&gt;</c>) 功能的静态入口：每个方法对应 <c>html_canvas.ts</c> 的一个导出函数，
    /// 调用都要显式带 DOM id。
    /// <para>
    /// 日常推荐用 <see cref="HTML_Canvas"/>（实例封装，自带 id 与状态）；本类面向「还没拿到实例」或
    /// 与具体画布无关的操作（例如 <see cref="ToCanvasId"/>、<see cref="GetViewportSize"/>、<see cref="GetMaxSize"/>）。
    /// </para>
    /// <para>
    /// 尺寸链路：这里只改 CSS，后备缓冲 = CSS 尺寸 × DPR，由 <see cref="GraphicsDevice.SyncCanvasSize"/> 同步。
    /// </para>
    /// </summary>
    public static class HTML_Canvas_Func
    {
        /// <summary>未指定画布时使用的 DOM id（与 <see cref="Game"/> 的默认选择器 "#game" 对齐）。</summary>
        public const string DefaultCanvasId = "game";

        /// <summary>
        /// 把 "#game" 这类选择器或纯 id 归一化成 DOM id（去掉前导 #，空串回落到 <see cref="DefaultCanvasId"/>）。
        /// </summary>
        /// <param name="idOrSelector">画布 id 或选择器。</param>
        public static string ToCanvasId(string idOrSelector)
        {
            string trimmed = (idOrSelector ?? string.Empty).Trim();
            if (trimmed.Length > 0 && trimmed[0] == '#') trimmed = trimmed[1..];
            return trimmed.Length > 0 ? trimmed : DefaultCanvasId;
        }

        /// <summary>创建一块铺满视口的全屏画布；已存在同 id 画布时返回 false。</summary>
        /// <param name="id">画布 DOM id（可写 "#id" 形式）。</param>
        public static bool CreateFullscreen(string id) => JSBind_HTML_Canvas.CreateFullscreen(ToCanvasId(id));

        /// <summary>在指定位置创建一块画布；已存在同 id 画布时返回 false。</summary>
        /// <param name="id">画布 DOM id（可写 "#id" 形式）。</param>
        /// <param name="x">左上角 X（CSS 像素）。</param>
        /// <param name="y">左上角 Y（CSS 像素）。</param>
        /// <param name="width">CSS 宽度（像素）。</param>
        /// <param name="height">CSS 高度（像素）。</param>
        public static bool Create(string id, int x, int y, int width, int height)
            => JSBind_HTML_Canvas.Create(ToCanvasId(id), x, y, width, height);

        /// <summary>
        /// 【通用布局入口】一次调用覆盖矩形摆位 / 只改尺寸 / 居中 / 铺满四种情况（对应 TS 的 applyLayout）。
        /// <see cref="HTML_Canvas"/> 的所有 Set* 都转发到这里。
        /// </summary>
        /// <param name="id">画布 DOM id（可写 "#id" 形式）。</param>
        /// <param name="mode">布局方式。</param>
        /// <param name="x">左上角 X（<see cref="HTML_CanvasLayoutMode.Centered"/> / <see cref="HTML_CanvasLayoutMode.Size"/> 时忽略）。</param>
        /// <param name="y">左上角 Y（同上）。</param>
        /// <param name="width">CSS 宽度（<see cref="HTML_CanvasLayoutMode.Fullscreen"/> 时忽略）。</param>
        /// <param name="height">CSS 高度（同上）。</param>
        /// <returns>画布存在且设置成功时返回 true。</returns>
        public static bool ApplyLayout(string id, HTML_CanvasLayoutMode mode, int x, int y, int width, int height)
            => JSBind_HTML_Canvas.ApplyLayout(ToCanvasId(id), (int)mode, x, y, width, height);

        /// <summary>
        /// 读画布当前的实际矩形（相对视口左上角，CSS 像素）。
        /// </summary>
        /// <returns>画布不存在时返回 <c>null</c>（TS 侧此时写回全 0）。</returns>
        public static Rectangle? GetRect(string id)
        {
            Span<int> view = stackalloc int[4];
            JSBind_HTML_Canvas.GetRect(ToCanvasId(id), view);

            Rectangle rect = new(view[0], view[1], view[2], view[3]);
            return rect.Width <= 0 || rect.Height <= 0 ? null : rect;
        }
        
        public static Point GetMaxSize()
        {
            Span<int> view = stackalloc int[2];
            JSBind_HTML_Canvas.GetMaxSize(view);
            return new Point(view[0], view[1]);
        }

        /// <summary>请求浏览器原生全屏（真全屏）。被拒绝时自动回落到「铺满视口」的软全屏。</summary>
        /// <remarks>浏览器要求由用户手势触发（点按 / 按键），否则会被拒绝。</remarks>
        public static bool RequestFullscreen(string id) => JSBind_HTML_Canvas.RequestFullscreen(ToCanvasId(id));

        /// <summary>退出浏览器原生全屏（document 级动作，与具体画布无关）。</summary>
        public static void ExitFullscreen() => JSBind_HTML_Canvas.ExitFullscreen();

        /// <summary>撤销本模块写在画布上的行内样式，恢复到页面自身布局，同时解除居中跟随。</summary>
        public static bool RestoreLayout(string id) => JSBind_HTML_Canvas.RestoreLayout(ToCanvasId(id));

        /// <summary>读浏览器视口尺寸（window.innerWidth / innerHeight），常用于自己算位置。</summary>
        /// <returns><see cref="Point.X"/> = 视口宽，<see cref="Point.Y"/> = 视口高。</returns>
        public static Point GetViewportSize()
        {
            Span<int> view = stackalloc int[2];
            JSBind_HTML_Canvas.GetViewportSize(view);
            return new Point(view[0], view[1]);
        }

        /// <summary>删除画布（从 DOM 移除）；画布不存在时返回 false。</summary>
        public static bool Destroy(string id) => JSBind_HTML_Canvas.Destroy(ToCanvasId(id));

        /// <summary>画布是否已存在（页面上已有，或曾由引擎创建）。</summary>
        public static bool Exists(string id) => JSBind_HTML_Canvas.Exists(ToCanvasId(id));
    }
}
