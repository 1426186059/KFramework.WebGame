namespace KFramework.MonoGame
{
    /// <summary>
    /// 画布 CSS 光标（通用封装，调用 <see cref="JSBind_Cursor"/>）。
    /// 把桌面端「切换 .CUR 文件」的语义改成「切换 canvas 的 style.cursor」，接受任意合法 CSS cursor 值。
    /// </summary>
    /// <remarks>
    /// <see cref="Set(string)"/> / <see cref="Set(string, string)"/> 的 <paramref name="name"/> 即为 CSS 的 cursor 取值，
    /// 直接写入画布 style.cursor。常用取值如下（完整列表见 MDN 的 “CSS cursor” 词条）：
    /// <list type="bullet">
    ///   <item><description>通用：<c>auto</c>（交给浏览器按上下文决定，不一定是箭头）、<c>default</c>（默认箭头，即系统那个普通指针）、<c>none</c>（隐藏光标）</description></item>
    ///   <item><description>链接 / 状态：<c>pointer</c>（手型）、<c>help</c>、<c>wait</c>（等待转圈）、<c>progress</c>、<c>not-allowed</c>（禁止圆圈）、<c>context-menu</c>、<c>cell</c>、<c>copy</c>、<c>alias</c></description></item>
    ///   <item><description>文本选择：<c>text</c>（I 型竖线）、<c>vertical-text</c></description></item>
    ///   <item><description>拖拽：<c>move</c>、<c>grab</c>（可抓取手）、<c>grabbing</c>（抓取中）</description></item>
    ///   <item><description>缩放：<c>zoom-in</c>（放大镜 +）、<c>zoom-out</c></description></item>
    ///   <item><description>改变尺寸 / 滚动：<c>col-resize</c>、<c>row-resize</c>、<c>n-resize</c>、<c>e-resize</c>、<c>s-resize</c>、<c>w-resize</c>、<c>ne-resize</c>、<c>nw-resize</c>、<c>se-resize</c>、<c>sw-resize</c>、<c>ew-resize</c>、<c>ns-resize</c>、<c>nesw-resize</c>、<c>nwse-resize</c>、<c>all-scroll</c></description></item>
    ///   <item><description>自定义图片：<c>url(图片地址), auto</c>（可带热点坐标，如 <c>url(a.png) 2 2, pointer</c>；末尾必须跟一个关键字作兜底）</description></item>
    /// </list>
    /// <see cref="Reset()"/> / <see cref="Reset(string)"/> 会把光标复位成 <c>default</c>，也就是系统那个普通箭头指针。
    public static class MouseCursorFunc
    {
        // ---------- CSS cursor 取值常量（与本类 <remarks> 中的取值列表一一对应） ----------

        /// <summary>默认箭头（系统普通指针）。等价于 CSS <c>default</c>；<see cref="Reset()"/> 使用的就是它。</summary>
        public const string Default = "default";
        /// <summary>交给浏览器按上下文决定，不一定是箭头。等价于 CSS <c>auto</c>。</summary>
        public const string Auto = "auto";
        /// <summary>隐藏光标。等价于 CSS <c>none</c>。</summary>
        public const string None = "none";

        /// <summary>手型（链接）。等价于 CSS <c>pointer</c>。</summary>
        public const string Pointer = "pointer";
        /// <summary>十字准星。等价于 CSS <c>crosshair</c>。</summary>
        public const string Crosshair = "crosshair";
        /// <summary>帮助。等价于 CSS <c>help</c>。</summary>
        public const string Help = "help";
        /// <summary>等待（转圈）。等价于 CSS <c>wait</c>。</summary>
        public const string Wait = "wait";
        /// <summary>进行中。等价于 CSS <c>progress</c>。</summary>
        public const string Progress = "progress";
        /// <summary>禁止（圆圈）。等价于 CSS <c>not-allowed</c>。</summary>
        public const string NotAllowed = "not-allowed";
        /// <summary>右键菜单。等价于 CSS <c>context-menu</c>。</summary>
        public const string ContextMenu = "context-menu";
        /// <summary>单元格。等价于 CSS <c>cell</c>。</summary>
        public const string Cell = "cell";
        /// <summary>复制。等价于 CSS <c>copy</c>。</summary>
        public const string Copy = "copy";
        /// <summary>别名 / 快捷方式。等价于 CSS <c>alias</c>。</summary>
        public const string Alias = "alias";

        /// <summary>I 型竖线（文本选择）。等价于 CSS <c>text</c>。</summary>
        public const string Text = "text";
        /// <summary>横向文本。等价于 CSS <c>vertical-text</c>。</summary>
        public const string VerticalText = "vertical-text";

        /// <summary>移动。等价于 CSS <c>move</c>。</summary>
        public const string Move = "move";
        /// <summary>可抓取的手。等价于 CSS <c>grab</c>。</summary>
        public const string Grab = "grab";
        /// <summary>抓取中。等价于 CSS <c>grabbing</c>。</summary>
        public const string Grabbing = "grabbing";

        /// <summary>放大镜 +。等价于 CSS <c>zoom-in</c>。</summary>
        public const string ZoomIn = "zoom-in";
        /// <summary>放大镜 -。等价于 CSS <c>zoom-out</c>。</summary>
        public const string ZoomOut = "zoom-out";

        /// <summary>列宽改变。等价于 CSS <c>col-resize</c>。</summary>
        public const string ColResize = "col-resize";
        /// <summary>行高改变。等价于 CSS <c>row-resize</c>。</summary>
        public const string RowResize = "row-resize";
        /// <summary>可左右拖动（水平改变尺寸）。等价于 CSS <c>ew-resize</c>。</summary>
        public const string EwResize = "ew-resize";
        /// <summary>可上下拖动（垂直改变尺寸）。等价于 CSS <c>ns-resize</c>。</summary>
        public const string NsResize = "ns-resize";
        /// <summary>沿对角线（东北-西南）拖动。等价于 CSS <c>nesw-resize</c>。</summary>
        public const string NeswResize = "nesw-resize";
        /// <summary>沿对角线（西北-东南）拖动。等价于 CSS <c>nwse-resize</c>。</summary>
        public const string NwseResize = "nwse-resize";
        /// <summary>任意方向滚动。等价于 CSS <c>all-scroll</c>。</summary>
        public const string AllScroll = "all-scroll";

        /// <summary>向上（北）改变尺寸。等价于 CSS <c>n-resize</c>。</summary>
        public const string NResize = "n-resize";
        /// <summary>向右（东）改变尺寸。等价于 CSS <c>e-resize</c>。</summary>
        public const string EResize = "e-resize";
        /// <summary>向下（南）改变尺寸。等价于 CSS <c>s-resize</c>。</summary>
        public const string SResize = "s-resize";
        /// <summary>向左（西）改变尺寸。等价于 CSS <c>w-resize</c>。</summary>
        public const string WResize = "w-resize";
        /// <summary>向右上（东北）改变尺寸。等价于 CSS <c>ne-resize</c>。</summary>
        public const string NeResize = "ne-resize";
        /// <summary>向左上（西北）改变尺寸。等价于 CSS <c>nw-resize</c>。</summary>
        public const string NwResize = "nw-resize";
        /// <summary>向右下（东南）改变尺寸。等价于 CSS <c>se-resize</c>。</summary>
        public const string SeResize = "se-resize";
        /// <summary>向左下（西南）改变尺寸。等价于 CSS <c>sw-resize</c>。</summary>
        public const string SwResize = "sw-resize";

        public static void Set(string name)
        {
            JSBind_Cursor.SetCursor(GraphicsDevice.CanvasId, name);
        }

        public static void Reset()
        {
            JSBind_Cursor.ResetCursor(GraphicsDevice.CanvasId);
        }
    }
}
