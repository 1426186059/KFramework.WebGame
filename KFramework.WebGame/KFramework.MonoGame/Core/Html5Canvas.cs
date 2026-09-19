namespace KFramework.MonoGame
{
    /// <summary>
    /// HTML5 画布（<c>&lt;canvas&gt;</c>）的位置与尺寸控制。
    /// <para>
    /// 游戏启动时由引擎自己准备画布：<see cref="GraphicsDevice"/> 初始化上下文时会告诉 JS 侧
    /// 「用哪块画布」，页面里已有（如 index.html 里的 <c>&lt;canvas id="game"&gt;</c>）就直接用它，
    /// 没有则由 html_canvas.ts 自动创建一块铺满视口的默认画布——也就是默认全屏绘制的那块。
    /// </para>
    /// <para>
    /// 改尺寸 / 位置只写 CSS：下一帧 <see cref="GraphicsDevice.SyncCanvasSize"/> 会把新的 CSS 尺寸
    /// （× DPR）同步成后备缓冲，并更新 <see cref="Viewport"/>、触发 <see cref="GameWindow.SizeChanged"/>，
    /// 输入坐标也会跟着画布 rect 自动换算，无需额外处理。
    /// </para>
    /// </summary>
    public static class Html5Canvas
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

        /// <summary>设置画布位置与 CSS 尺寸；画布不存在时返回 false。</summary>
        public static bool SetRect(string id, int x, int y, int width, int height)
            => JSBind_HTML_Canvas.SetRect(ToCanvasId(id), x, y, width, height);

        /// <summary>把画布恢复为铺满视口；画布不存在时返回 false。</summary>
        public static bool SetFullscreen(string id) => JSBind_HTML_Canvas.SetFullscreen(ToCanvasId(id));

        /// <summary>删除画布（从 DOM 移除）；画布不存在时返回 false。</summary>
        public static bool Destroy(string id) => JSBind_HTML_Canvas.Destroy(ToCanvasId(id));

        /// <summary>画布是否已存在。</summary>
        public static bool Exists(string id) => JSBind_HTML_Canvas.Exists(ToCanvasId(id));
    }
}
