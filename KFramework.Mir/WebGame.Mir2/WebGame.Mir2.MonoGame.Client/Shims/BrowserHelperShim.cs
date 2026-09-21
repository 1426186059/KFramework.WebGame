using KFramework.MonoGame;

namespace Client
{
    // 浏览器端 BrowserHelper 实现（Mir2/Utils/BrowserHelper.cs 被 csproj 排除，这里补齐逻辑代码引用的成员）。
    // 打开外部链接走 KFramework.MonoGame.JSBind_Platform.OpenUrl（platform.ts 的 window.open，新标签打开）。
    // 注意：浏览器通常只在「用户手势」（如点击）内允许 window.open，否则可能被弹窗拦截。
    public static class BrowserHelper
    {
        public static void OpenDefaultBrowser(string url)
        {
            try { JSBind_Platform.OpenUrl(url); }
            catch { }
        }
    }
}
