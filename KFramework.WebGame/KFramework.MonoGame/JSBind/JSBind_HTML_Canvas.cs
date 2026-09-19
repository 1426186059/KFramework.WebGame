using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 画布元素管理（<c>&lt;canvas&gt;</c>）的 C# ⇄ JS 绑定。
    /// 依赖 KFramework.TSEngine 项目：本类 JSImport 全部映射到 src/html_canvas.ts 的 "canvas" 模块；
    /// 编译产物 html_canvas.js 由各示例 SyncJsEngine 复制到 wwwroot/jsengine。
    /// </summary>
    internal static partial class JSBind_HTML_Canvas
    {
        /// <summary>
        /// 创建一块铺满视口的全屏画布。已存在同 id 画布时返回 false。
        /// </summary>
        /// <param name="id">画布 DOM id（模块内部会再归一化，允许传 "#id" 形式）。</param>
        [JSImport("createFullscreen", "canvas")]
        internal static partial bool CreateFullscreen(string id);

        /// <summary>
        /// 在指定位置创建一块画布。已存在同 id 画布时返回 false。
        /// </summary>
        /// <param name="id">画布 DOM id。</param>
        /// <param name="x">左上角 X（CSS 像素）。</param>
        /// <param name="y">左上角 Y（CSS 像素）。</param>
        /// <param name="width">CSS 宽度（像素）。</param>
        /// <param name="height">CSS 高度（像素）。</param>
        [JSImport("create", "canvas")]
        internal static partial bool Create(string id, int x, int y, int width, int height);

        /// <summary>设置已有画布的位置与 CSS 尺寸；画布不存在时返回 false。</summary>
        [JSImport("setRect", "canvas")]
        internal static partial bool SetRect(string id, int x, int y, int width, int height);

        /// <summary>把已有画布恢复为铺满视口；画布不存在时返回 false。</summary>
        [JSImport("setFullscreen", "canvas")]
        internal static partial bool SetFullscreen(string id);

        /// <summary>删除画布（从 DOM 移除并注销）；画布不存在时返回 false。</summary>
        [JSImport("destroy", "canvas")]
        internal static partial bool Destroy(string id);

        /// <summary>画布是否已存在（页面上已有，或曾由本模块创建）。</summary>
        [JSImport("exists", "canvas")]
        internal static partial bool Exists(string id);
    }
}
