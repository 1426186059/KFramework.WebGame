using System;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 浏览器原生文本输入（IME）的 C# 封装，API 形态对齐 Unity 的 <c>TouchScreenKeyboard</c>：
    /// <list type="bullet">
    ///   <item><see cref="Open"/> 类比 <c>TouchScreenKeyboard.Open</c>：在指定位置激活覆盖层并聚焦（额外携带屏幕坐标，因为我们用透明 DOM &lt;input&gt; 覆盖在 canvas 上）。</item>
    ///   <item><see cref="Close"/> 类比 <c>keyboard.Close</c>：关闭覆盖层。</item>
    ///   <item><see cref="Text"/> 类比 <c>keyboard.text</c>：当前文本（含 IME 组字内容）。</item>
    ///   <item><see cref="Active"/> 类比 <c>keyboard.active</c>：是否仍在接管输入。</item>
    /// </list>
    /// 纯 Pull 模型：引擎每帧经 <see cref="Poll"/> 从 DOM 覆盖层拉取文本到 <see cref="Text"/>，并检测回车上升沿到 <see cref="EnterPressed"/>；
    /// 回车 / Esc 由全局键盘（<see cref="Input_KeyBoard"/>）从冒泡事件捕获，本类不暴露输入事件。
    ///
    /// <para>DOM 覆盖层经 [JSExport]（JSBind_InputHtmlIme）转发的控制键 / 编辑结果，先汇聚到本类，
    /// 再以事件形式分发出去（<see cref="KeyDown"/> / <see cref="DomValue"/>），供 TextBox 等订阅方监听，
    /// 避免 [JSExport] 直接耦合到具体控件。</para>
    /// </summary>
    public static class Input_IME
    {
        /// <summary>覆盖层当前是否正在接管输入（由 <see cref="Open"/> / <see cref="Close"/> 维护）。</summary>
        public static bool Active { get; private set; }

        /// <summary>最近一次 <see cref="Poll"/> 拉取到的文本（含 IME 组字内容）。未激活时为空串。</summary>
        public static string Text { get; private set; } = string.Empty;

        /// <summary>原生覆盖层转发的控制键（来自 JSBind_InputHtmlIme.OnKeyDown）。
        /// 参数为 (key, ctrl, shift, alt)。TextBox 订阅此事件以维护光标 / 选区。</summary>
        public static event Action<string, bool, bool, bool> KeyDown;

        /// <summary>原生覆盖层转发的文本编辑结果（来自 JSBind_InputHtmlIme.OnDomValue，含 DOM 真实光标）。
        /// 参数为 (value, selStart, selEnd, composing)。TextBox 订阅此事件以采纳文本与光标。</summary>
        public static event Action<string, int, int, bool> DomValue;

        /// <summary>在画布指定位置（后备缓冲像素）显示原生输入框并聚焦（transparent 恒为 true：DOM 只作捕获代理）。</summary>
        public static void Open(
            double cx, double cy, double cw, double ch,
            double fontPx, int color, string value, bool password, int maxLength, bool multiline,
            string cssFont)
        {
            Active = true;
            // transparent 恒为 true：DOM 只作 IME / 键盘捕获代理。
            JSBind_InputHtmlIme.Show(cx, cy, cw, ch, fontPx, color, value ?? string.Empty,
                password, maxLength, multiline, cssFont ?? "10px sans-serif", true);
        }

        public static void Close()
        {
            Active = false;
            Text = string.Empty;
            JSBind_InputHtmlIme.Hide();
        }

        /// <summary>由 JSBind_InputHtmlIme.OnKeyDown 调用：把控制键以事件形式转发给订阅方。</summary>
        internal static void RouteKeyDown(string key, bool ctrl, bool shift, bool alt)
            => KeyDown?.Invoke(key, ctrl, shift, alt);

        /// <summary>由 JSBind_InputHtmlIme.OnDomValue 调用：把文本编辑结果以事件形式转发给订阅方。</summary>
        internal static void RouteDomValue(string value, int selStart, int selEnd, bool composing)
            => DomValue?.Invoke(value, selStart, selEnd, composing);

        /// <summary>每帧由 <see cref="Input.Poll"/> 调用一次：仅在激活时从 DOM 覆盖层拉取文本，并检测回车上升沿。</summary>
        public static void Poll()
        {
            // 未激活时跳过 DOM 拉取，并清掉上一帧的回车上升沿。
            if (!Active)
            {
                return;
            }

            Text = JSBind_InputHtmlIme.GetValue() ?? string.Empty;
        }
    }
}
