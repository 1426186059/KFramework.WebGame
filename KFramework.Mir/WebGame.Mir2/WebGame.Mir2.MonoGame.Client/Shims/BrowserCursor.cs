using System;

namespace MirEngine
{
    // 浏览器端光标设置（迁移到 KFramework.MonoGame 后由画布 CSS 控制；这里统一为无操作，避免依赖旧 JS）。
    public static class BrowserCursor
    {
        public static void Set(string name)
        {
            try { SetImpl(name); }
            catch { }
        }

        // 真实实现应操作 GL 画布的 style.cursor；当前无操作（不影响逻辑流程）。
        private static void SetImpl(string name) { }
    }
}
