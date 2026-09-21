using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 画布元素管理（<c>&lt;canvas&gt;</c>）的 C# ⇄ JS 绑定。
    /// 依赖 KFramework.TSEngine 项目：本类 JSImport 全部映射到 src/html_canvas.ts 的 "canvas" 模块；
    /// 编译产物 html_canvas.js 由各示例 SyncJsEngine 复制到 wwwroot/jsengine。
    /// </summary>
    public static partial class JSBind_HTML_Canvas
    {
        /// <summary>
        /// 创建一块画布（按 <paramref name="mode"/> 布局：Rect 摆位 / Centered 居中 / Fullscreen 填满整个 HTML 页面 / Size 仅设尺寸）。已存在同 id 画布时返回 false。
        /// </summary>
        /// <param name="id">画布 DOM id。</param>
        /// <param name="mode">布局方式，取 <see cref="HTML_CanvasLayoutMode"/> 的数值。</param>
        /// <param name="x">左上角 X（CSS 像素），Rect 模式使用。</param>
        /// <param name="y">左上角 Y（CSS 像素），Rect 模式使用。</param>
        /// <param name="width">CSS 宽度（像素）。</param>
        /// <param name="height">CSS 高度（像素）。</param>
        [JSImport("create", "canvas")]
        public static partial bool Create(string id, int mode, int x, int y, int width, int height);

        /// <summary>
        /// 【通用布局入口】对应 TS 的 applyLayout：mode 取 <see cref="HTML_CanvasLayoutMode"/> 的数值，
        /// 矩形摆位 / 只改尺寸 / 居中 / 铺满四种情况都走这一个导出。
        /// </summary>
        [JSImport("applyLayout", "canvas")]
        public static partial bool ApplyLayout(string id, int mode, int x, int y, int width, int height);

        /// <summary>读画布当前矩形：view[0]=left，view[1]=top，view[2]=width，view[3]=height（CSS 像素）。</summary>
        [JSImport("getRect", "canvas")]
        public static partial void GetRect(string id, [JSMarshalAs<JSType.MemoryView>] Span<int> view);

        /// <summary>撤销本模块写在画布上的行内样式，恢复页面自身布局；画布不存在时返回 false。</summary>
        [JSImport("restoreLayout", "canvas")]
        public static partial bool RestoreLayout(string id);

        /// <summary>读 Canvas 所在的 HTML 页面尺寸：view[0]=innerWidth，view[1]=innerHeight。</summary>
        [JSImport("getHTMLPageSize", "canvas")]
        public static partial void GetHTMLPageSize([JSMarshalAs<JSType.MemoryView>] Span<int> view);

        /// <summary>删除画布（从 DOM 移除并注销）；画布不存在时返回 false。</summary>
        [JSImport("destroy", "canvas")]
        public static partial bool Destroy(string id);

        /// <summary>画布是否已存在（页面上已有，或曾由本模块创建）。</summary>
        [JSImport("exists", "canvas")]
        public static partial bool Exists(string id);
    }
}
