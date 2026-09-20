namespace KFramework.MonoGame
{
    /// <summary>
    /// 画布的布局方式，与 <c>html_canvas.ts</c> 里 <c>applyLayout</c> 的 <c>mode</c> 参数一一对应。
    /// 四种摆位需求（矩形 / 只改尺寸 / 居中 / 铺满）统一由这一个枚举 + 一个通用方法表达。
    /// </summary>
    public enum HTML_CanvasLayoutMode
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
}
