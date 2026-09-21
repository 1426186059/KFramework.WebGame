namespace KFramework.MonoGame
{
    /// <summary>
    /// 画布 (<c>&lt;canvas&gt;</c>) 的「创建 / 销毁」生命周期静态入口：每个方法对应 <c>html_canvas.ts</c> 的一个导出函数，
    /// 调用都要显式带 DOM id，并在内部归一化。
    /// <para>
    /// 其余画布操作（布局、读矩形、撤销布局、判存在、读页面尺寸）已由实例封装 <see cref="HTML_Canvas"/> 直接经
    /// <see cref="JSBind_HTML_Canvas"/> 调用，不再经过本类，因此本类只保留「建」与「销」两个入口。
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

        /// <summary>创建一块画布（按 <paramref name="mode"/> 布局）；已存在同 id 画布时返回 false。</summary>
        /// <param name="id">画布 DOM id（可写 "#id" 形式）。</param>
        /// <param name="mode">布局方式（<see cref="HTML_CanvasLayoutMode"/> 数值）。</param>
        /// <param name="x">左上角 X（CSS 像素），Rect 模式使用。</param>
        /// <param name="y">左上角 Y（CSS 像素），Rect 模式使用。</param>
        /// <param name="width">CSS 宽度（像素）。</param>
        /// <param name="height">CSS 高度（像素）。</param>
        public static bool Create(string id, int mode, int x, int y, int width, int height)
            => JSBind_HTML_Canvas.Create(ToCanvasId(id), mode, x, y, width, height);

        /// <summary>删除画布（从 DOM 移除）；画布不存在时返回 false。</summary>
        public static bool Destroy(string id) => JSBind_HTML_Canvas.Destroy(ToCanvasId(id));

        /// <summary>页面上这块画布是否已存在（含引擎曾创建过的）。</summary>
        public static bool Exists(string id)
        {
            return JSBind_HTML_Canvas.Exists(ToCanvasId(id));
        }
    }
}
