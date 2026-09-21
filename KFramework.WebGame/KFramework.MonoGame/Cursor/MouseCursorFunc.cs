namespace KFramework.MonoGame
{
    /// <list type="bullet">
    ///   <item><description>通用：<c>auto</c>（交给浏览器按上下文决定，不一定是箭头）、<c>default</c>（默认箭头，即系统那个普通指针）、<c>none</c>（隐藏光标）</description></item>
    ///   <item><description>链接 / 状态：<c>pointer</c>（手型）、<c>help</c>、<c>wait</c>（等待转圈）、<c>progress</c>、<c>not-allowed</c>（禁止圆圈）、<c>context-menu</c>、<c>cell</c>、<c>copy</c>、<c>alias</c></description></item>
    ///   <item><description>文本选择：<c>text</c>（I 型竖线）、<c>vertical-text</c></description></item>
    ///   <item><description>拖拽：<c>move</c>、<c>grab</c>（可抓取手）、<c>grabbing</c>（抓取中）</description></item>
    ///   <item><description>缩放：<c>zoom-in</c>（放大镜 +）、<c>zoom-out</c></description></item>
    ///   <item><description>改变尺寸 / 滚动：<c>col-resize</c>、<c>row-resize</c>、<c>n-resize</c>、<c>e-resize</c>、<c>s-resize</c>、<c>w-resize</c>、<c>ne-resize</c>、<c>nw-resize</c>、<c>se-resize</c>、<c>sw-resize</c>、<c>ew-resize</c>、<c>ns-resize</c>、<c>nesw-resize</c>、<c>nwse-resize</c>、<c>all-scroll</c></description></item>
    ///   <item><description>自定义图片：<c>url(图片地址), auto</c>（可带热点坐标，如 <c>url(a.png) 2 2, pointer</c>；末尾必须跟一个关键字作兜底）</description></item>
    /// </list>
    public static class MouseCursorFunc
    {
        /// <summary>默认箭头（系统普通指针）。
        public const string Default = "default";
        /// <summary>交给浏览器按上下文决定，不一定是箭头。等价于 CSS <c>auto</c>。</summary>
        public const string Auto = "auto";
        /// <summary>隐藏光标。等价于 CSS <c>none</c>。</summary>
        public const string None = "none";
        /// <summary>手型（链接）。等价于 CSS <c>pointer</c>。</summary>
        public const string Pointer = "pointer";
        /// <summary>等待（转圈）。等价于 CSS <c>wait</c>。</summary>
        public const string Wait = "wait";


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
