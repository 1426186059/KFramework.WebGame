using System;

namespace MirEngine
{
    // 浏览器原生文本输入覆盖层（DOM <input>/<textarea>）。
    // 原 Web_Mir2.Engine 通过 [JSImport] mir.input* 桥接 tsengine 的 input.ts；迁移到 KFramework.MonoGame 后，
    // 原生输入覆盖层所需的 JS 后端（mir.input*）不再加载，这里统一为无操作桩并保留事件订阅点：
    // - 事件（ValueChanged/Enter/Blur）仍对游戏侧 MirTextBox 可见，待接入 KFramework 文本输入通道后再驱动真实 DOM 覆盖层；
    // - Show/Hide/Reposition/SetValue/GetValue 当前为无操作，不影响逻辑流程（仅原生输入框文字不显示，属运行时缺口）。
    public static class BrowserInputOverlay
    {
        public static event EventHandler<string> ValueChanged;
        public static event EventHandler Enter;
        public static event EventHandler Blur;

        public static void Show(double cx, double cy, double cw, double ch, double fontPx, int color, string value, bool password, int maxLength, bool multiline) { }
        public static void Hide() { }
        public static void SetValue(string value) { }
        public static string GetValue() => string.Empty;
        public static void Reposition(double cx, double cy, double cw, double ch) { }
    }
}
