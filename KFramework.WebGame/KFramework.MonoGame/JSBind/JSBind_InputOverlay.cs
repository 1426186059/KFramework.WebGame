using System;
using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 原生文本输入覆盖层的 JS 绑定。映射 KFramework.TSEngine 的 src/input_overlay.ts（模块名 "input_overlay"），
    /// 编译产物经 SyncJsEngine 复制到各示例 / 游戏工程的 wwwroot/jsengine。
    /// 模块名必须与 main.ts 的 setModuleImports 一致。
    ///
    /// 一帧至多一次跨界调用（仅在 Show/Hide/Reposition/SetValue/GetValue 时），而 DOM 的 input / Enter / blur
    /// 三类事件则经本类的 [JSExport] 回调回传，由业务层的 MirEngine.BrowserInputOverlay 转交给 MirTextBox。
    /// </summary>
    public static partial class JSBind_InputOverlay
    {
        /// <summary>DOM &lt;input&gt; 文本变化时触发（IME 上屏、退格、粘贴等），参数为最新值。</summary>
        public static event EventHandler<string>? ValueChanged;

        /// <summary>在覆盖层输入框按下回车（非多行，或多行且未按 Shift）时触发，用于确认/登录。</summary>
        public static event EventHandler? Enter;

        /// <summary>覆盖层输入框失焦时触发，业务层据此切换/隐藏原生输入框。</summary>
        public static event EventHandler? Blur;

        // ===== DOM <input>/<textarea> 覆盖层控制（tsengine/src/input_overlay.ts） =====

        /// <summary>在画布上以 cx/cy/cw/ch（后备缓冲像素）为位置显示一个原生输入框并聚焦。</summary>
        [JSImport("show", "input_overlay")]
        public static partial void Show(double cx, double cy, double cw, double ch, double fontPx, int color, string value, bool password, int maxLength, bool multiline);

        /// <summary>隐藏并失焦当前覆盖层输入框。</summary>
        [JSImport("hide", "input_overlay")]
        public static partial void Hide();

        /// <summary>控件移动 / 尺寸变化时重新定位已显示的覆盖层输入框。</summary>
        [JSImport("reposition", "input_overlay")]
        public static partial void Reposition(double cx, double cy, double cw, double ch);

        /// <summary>以程序侧新值覆盖覆盖层输入框内容（如清空）。</summary>
        [JSImport("setValue", "input_overlay")]
        public static partial void SetValue(string value);

        /// <summary>读取覆盖层输入框当前内容。</summary>
        [JSImport("getValue", "input_overlay")]
        public static partial string GetValue();

        // ===== JS → C# 回调（由 main.ts 经 input_overlay.setHandlers 注册后回传） =====

        [JSExport]
        public static void OnValueChanged(string v) => ValueChanged?.Invoke(null, v);

        [JSExport]
        public static void OnEnter() => Enter?.Invoke(null, EventArgs.Empty);

        [JSExport]
        public static void OnBlur() => Blur?.Invoke(null, EventArgs.Empty);
    }
}
