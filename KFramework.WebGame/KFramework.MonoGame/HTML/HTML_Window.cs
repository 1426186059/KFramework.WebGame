namespace KFramework.MonoGame
{
    /// <summary>
    /// 浏览器窗口层（<c>&lt;window&gt;</c> / <c>&lt;document&gt;</c>）的对象封装，与 TS 层 <c>html_window.ts</c> 的 <c>window</c> 模块一一对应。
    /// 页面标题、地址栏参数、环境查询、外链打开、HTML 页面尺寸都走这里，底层转发到 <see cref="JSBind_HTML_Window"/>。
    /// </summary>
    public class HTML_Window
    {
        public static HTML_Window Current = null;

        /// <summary>设备像素比（window.devicePixelRatio）。由 <see cref="HTML_Canvas.SyncJSInfo"/> 回写，供画布尺寸换算使用。</summary>
        public static float DevicePixelRatio = 1000;

        public HTML_Window()
        {
            if (Current != null) throw new InvalidOperationException("HTML_Window.Current 已存在，不能重复创建。");
            Current = this;
        }

        /// <summary>设置页面标题（浏览器标签页文字）。</summary>
        public static void SetTitle(string title) => JSBind_HTML_Window.SetTitle(title);

        /// <summary>读取地址栏查询参数（?key=value 中的 value）。</summary>
        public static string GetQueryParameter(string name) => JSBind_HTML_Window.GetQueryParameter(name);

        /// <summary>当前是否移动端（触屏优先设备）。</summary>
        public static bool IsMobile() => JSBind_HTML_Window.IsMobile();

        /// <summary>页面基址（document.baseURI），用于把内容包的相对路径拼成绝对 URL。</summary>
        public static string GetBaseUri() => JSBind_HTML_Window.GetBaseUri();

        /// <summary>在浏览器中打开一个 URL（新标签）。</summary>
        public static void OpenUrl(string url) => JSBind_HTML_Window.OpenUrl(url);

        /// <summary>读 HTML 页面尺寸（innerWidth / innerHeight），常用于画布居中。</summary>
        public static Point GetPageSize()
        {
            Span<int> view = stackalloc int[2];
            JSBind_HTML_Window.GetPageSize(view);
            return new Point(view[0], view[1]);
        }
    }
}
