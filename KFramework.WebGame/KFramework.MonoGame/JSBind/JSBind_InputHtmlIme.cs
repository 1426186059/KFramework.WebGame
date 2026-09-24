using System;
using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 浏览器 HTML IME 输入覆盖层的 JS 绑定。映射 KFramework.TSEngine 的 <c>src/input_html_ime.ts</c>
    /// （模块名 "input_html_ime"；原 input_overlay.ts 更名而来）。
    ///
    /// 该覆盖层<b>仅用于 IME / 键盘捕获</b>：DOM 元素始终透明，
    /// 文字与光标一律由引擎自绘（见 <see cref="TextRenderer.TextBoxRenderer"/> 与
    /// <see cref="TextRenderer.TextCaret"/>）。
    ///
    /// C# 侧经 [JSImport] 控制 DOM 覆盖层（Show / Hide / Reposition / SetValue / GetValue），
    /// 其中 GetValue 由 C# 每帧拉取当前文本；DOM 的 input / Enter / focus / blur / IME 事件
    /// 经本类 [JSExport] 回调（OnValueChanged / OnEnter / OnFocus / OnBlur / OnIme*）回传并触发对应事件。
    /// </summary>
    public static partial class JSBind_InputHtmlIme
    {
        /// <summary>DOM 文本变化时触发（IME 上屏、退格、粘贴等），参数为最新值。</summary>
        public static event EventHandler<string>? ValueChanged;

        /// <summary>在覆盖层输入框按下回车键时触发，用于确认 / 登录（非"获得焦点"）。</summary>
        public static event EventHandler? Enter;

        /// <summary>覆盖层输入框<b>获得焦点</b>时触发。</summary>
        public static event EventHandler? Focus;

        /// <summary>覆盖层输入框<b>失去焦点</b>时触发（按 Escape 也视作失焦）。</summary>
        public static event EventHandler? Blur;

        /// <summary>IME 组字开始（compositionstart）时触发，即输入法被<b>激活</b>。</summary>
        public static event EventHandler? ImeActivate;

        /// <summary>
        /// IME 组字过程中（compositionupdate）触发，参数为当前正在组字的 preedit 串
        /// （候选窗尚未上屏时的拼音 / 假名等），用于实时监听 IME 输入内容。
        /// </summary>
        public static event EventHandler<string>? ImeUpdate;

        /// <summary>
        /// IME 组字结束（compositionend）时触发，即输入法被<b>停用</b>；参数为最终上屏文本
        /// （与随后的 ValueChanged 一致，但此处更早拿到、且明确标记"停用"边界）。
        /// </summary>
        public static event EventHandler<string>? ImeDeactivate;

        // ===== DOM <input>/<textarea> 覆盖层控制（tsengine/src/input_html_ime.ts） =====

        /// <summary>在画布上以 cx/cy/cw/ch（后备缓冲像素）为位置显示一个原生输入框并聚焦（始终透明）。</summary>
        [JSImport("show", "input_html_ime")]
        public static partial void Show(double cx, double cy, double cw, double ch, double fontPx, int color,
            string value, bool password, int maxLength, bool multiline, string fontFamily, bool transparent);

        /// <summary>隐藏并移除当前输入框（失焦），同时清空最近一次 show 参数。</summary>
        [JSImport("hide", "input_html_ime")]
        public static partial void Hide();

        /// <summary>按最近一次 show 参数重新定位当前可见输入框（窗口缩放 / 页面滚动时由 TS 自动调用）。</summary>
        [JSImport("reposition", "input_html_ime")]
        public static partial void Reposition(double cx, double cy, double cw, double ch);

        /// <summary>以程序代码设置输入框文本（如打开登录框时回填账号）。</summary>
        [JSImport("setValue", "input_html_ime")]
        public static partial void SetValue(string value);

        /// <summary>取当前输入框文本；由 C# 每帧拉取以同步到游戏侧 TextBox（替代 DOM input 事件主动推送）。</summary>
        [JSImport("getValue", "input_html_ime")]
        public static partial string GetValue();

        // ===== JS → C# 回调 =====

        /// <summary>JS → C# 入口：DOM input 事件，转发为 <see cref="ValueChanged"/>（参数 v 为最新文本）。</summary>
        [JSExport]
        public static void OnValueChanged(string v) => ValueChanged?.Invoke(null, v);

        /// <summary>JS → C# 入口：覆盖层输入框按下回车键，转发为 <see cref="Enter"/>。</summary>
        [JSExport]
        public static void OnEnter() => Enter?.Invoke(null, EventArgs.Empty);

        /// <summary>JS → C# 入口：覆盖层输入框获得焦点，转发为 <see cref="Focus"/>。</summary>
        [JSExport]
        public static void OnFocus() => Focus?.Invoke(null, EventArgs.Empty);

        /// <summary>JS → C# 入口：覆盖层输入框失去焦点（含按 Escape），转发为 <see cref="Blur"/>。</summary>
        [JSExport]
        public static void OnBlur() => Blur?.Invoke(null, EventArgs.Empty);

        /// <summary>JS → C# 入口：DOM compositionstart，转发为 <see cref="ImeActivate"/>（输入法激活）。</summary>
        [JSExport]
        public static void OnImeActivate() => ImeActivate?.Invoke(null, EventArgs.Empty);

        /// <summary>JS → C# 入口：DOM compositionupdate，转发为 <see cref="ImeUpdate"/>（参数 text 为 preedit 串）。</summary>
        [JSExport]
        public static void OnImeUpdate(string text) => ImeUpdate?.Invoke(null, text);

        /// <summary>JS → C# 入口：DOM compositionend，转发为 <see cref="ImeDeactivate"/>（参数 text 为最终上屏文本）。</summary>
        [JSExport]
        public static void OnImeDeactivate(string text) => ImeDeactivate?.Invoke(null, text);
    }
}
