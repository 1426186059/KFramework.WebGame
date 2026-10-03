using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 浏览器窗口层（<c>&lt;window&gt;</c> / <c>&lt;document&gt;</c>）的 C# ⇄ JS 绑定。
    /// 依赖 KFramework.TSEngine 项目：本类 JSImport 全部映射到 src/html_window.ts 的 "window" 模块；
    /// 编译产物 html_window.js 由各示例 SyncJsEngine 复制到 wwwroot/jsengine。
    /// </summary>
    public static partial class JSBind_HTML_Window
    {
        /// <summary>设置页面标题（浏览器标签页文字）。</summary>
        [JSImport("setTitle", "window")]
        public static partial void SetTitle(string title);

        /// <summary>读取地址栏查询参数（?key=value 中的 value）。</summary>
        [JSImport("getQueryParameter", "window")]
        public static partial string GetQueryParameter(string name);

        /// <summary>当前是否移动端（触屏优先设备）。</summary>
        [JSImport("isMobile", "window")]
        public static partial bool IsMobile();

        /// <summary>页面基址（document.baseURI），用于把内容包的相对路径拼成绝对 URL。</summary>
        [JSImport("getBaseUri", "window")]
        public static partial string GetBaseUri();

        /// <summary>在浏览器中打开一个 URL（新标签）。</summary>
        [JSImport("openUrl", "window")]
        public static partial void OpenUrl(string url);

        /// <summary>读 HTML 页面尺寸：view[0]=innerWidth，view[1]=innerHeight。</summary>
        [JSImport("getPageSize", "window")]
        public static partial void GetPageSize([JSMarshalAs<JSType.MemoryView>] Span<int> view);
    }
}
