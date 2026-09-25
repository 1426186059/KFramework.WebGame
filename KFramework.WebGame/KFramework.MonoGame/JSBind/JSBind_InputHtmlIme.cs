using System;
using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 浏览器 HTML IME 输入覆盖层的 JS 绑定。映射 KFramework.TSEngine 的 <c>src/input_html_ime.ts</c>。
    ///
    /// 该覆盖层<b>仅用于 IME / 键盘捕获</b>：DOM 元素始终透明，文字与光标一律由引擎自绘
    /// （见 <see cref="TextRenderer.TextBoxRenderer"/> 与 <see cref="TextRenderer.TextCaret"/>）。
    ///
    /// 数据流向：
    /// <list type="bullet">
    ///   <item>C# 经 [JSImport] 主动控制 DOM 覆盖层：<see cref="Show"/>（激活）、<see cref="Hide"/>（关闭）、
    ///     <see cref="Reposition"/>（重新定位）。</item>
    ///   <item>C# 每帧经 <see cref="GetValue"/> 拉取当前文本（含 IME 组字内容）。</item>
    ///   <item>DOM 经 [JSExport] 把原生编辑结果 / 控制键回传引擎：<see cref="OnDomValue"/>、<see cref="OnKeyDown"/>。</item>
    /// </list>
    /// 回车 / Esc 由 TS 放行冒泡到全局键盘（input_keyboard），供 C# 侧判断确认 / 取消，
    /// 因此本类不在此处处理确认 / 取消。
    /// </summary>
    public static partial class JSBind_InputHtmlIme
    {
        // ===== DOM <input>/<textarea> 覆盖层控制（tsengine/src/input_html_ime.ts） =====

        /// <summary>在画布上以 cx/cy/cw/ch（后备缓冲像素）为位置显示一个原生输入框并聚焦（始终透明）。</summary>
        [JSImport("show", "input_html_ime")]
        public static partial void Show(double cx, double cy, double cw, double ch, double fontPx, int color,
            string value, bool password, int maxLength, bool multiline, string fontFamily, bool transparent);

        /// <summary>隐藏并移除当前输入框（失焦），同时清空最近一次 show 参数。</summary>
        [JSImport("hide", "input_html_ime")]
        public static partial void Hide();

        /// <summary>取当前输入框文本（含 IME 组字内容）。</summary>
        [JSImport("getValue", "input_html_ime")]
        public static partial string GetValue();

        /// <summary>设置当前输入框文本（引擎把自身 text + IME 预览写回 DOM，使镜像一致）。</summary>
        [JSImport("setValue", "input_html_ime")]
        public static partial void SetValue(string value);

        /// <summary>设置当前输入框光标区间（引擎把自身光标写回 DOM，对齐 IME 候选窗位置）。</summary>
        [JSImport("setSelectionRange", "input_html_ime")]
        public static partial void SetSelectionRange(int start, int end);

        // ===== DOM -> 引擎 的桥接（[JSExport]，对应 tsengine/src/input_html_ime.ts 的前向调用） =====
        // 原生编辑结果以“事件”进入引擎：控制键转发给引擎 TextBox（自行维护光标），
        // 文本回传经 OnDomValue（携带 DOM 真实光标），由引擎按 DOM 光标采纳文本。
        // 引擎始终是 text / 光标 / 选区的唯一真相源（对齐 UGUI InputField）。

        /// <summary>DOM 控制键转发（Backspace / 方向键 / Home / End / Enter / Esc / Tab 等）。先汇聚到 Input_IME 以事件分发，避免直接耦合 TextBox。</summary>
        [JSExport]
        public static void OnKeyDown(string key, bool ctrl, bool shift, bool alt)
            => Input_IME.RouteKeyDown(key, ctrl, shift, alt);

        /// <summary>DOM 原生编辑结果回传（含光标位置）。先汇聚到 Input_IME 以事件分发，引擎按 DOM 光标采纳文本。</summary>
        [JSExport]
        public static void OnDomValue(string value, int selStart, int selEnd, bool composing)
            => Input_IME.RouteDomValue(value, selStart, selEnd, composing);
    }
}
