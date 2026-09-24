using System;

namespace KFramework.MonoGame
{
    /// <summary>
    /// HTML DOM IME 输入覆盖层（&lt;input&gt;/&lt;textarea&gt;）的业务侧封装，基于 <see cref="JSBind_InputHtmlIme"/>。
    ///
    /// <b>覆盖层始终透明</b>：它只负责 IME 候选窗与键盘捕获，
    /// 文字与光标一律由引擎自绘（<see cref="TextBoxRenderer"/> + <see cref="TextCaret"/>）。
    /// 因此这里不再有任何"由 DOM 显示文字 / 光标"的模式。
    /// </summary>
    internal static class TextInputHtmlIme
    {
        /// <summary>覆盖层当前是否正在接管输入（由 Show / Hide 维护）。</summary>
        public static bool IsActive { get; private set; }

        public static event EventHandler<string> ValueChanged
        {
            add => JSBind_InputHtmlIme.ValueChanged += value;
            remove => JSBind_InputHtmlIme.ValueChanged -= value;
        }

        /// <summary>回车键（用于确认 / 登录），不是"获得焦点"。</summary>
        public static event EventHandler Enter
        {
            add => JSBind_InputHtmlIme.Enter += value;
            remove => JSBind_InputHtmlIme.Enter -= value;
        }

        /// <summary>覆盖层获得焦点。</summary>
        public static event EventHandler Focus
        {
            add => JSBind_InputHtmlIme.Focus += value;
            remove => JSBind_InputHtmlIme.Focus -= value;
        }

        public static event EventHandler Blur
        {
            add => JSBind_InputHtmlIme.Blur += value;
            remove => JSBind_InputHtmlIme.Blur -= value;
        }

        /// <summary>
        /// 在画布指定位置（后备缓冲像素）显示原生输入框并聚焦。
        /// fontPx / cssFont 由调用方按其字体描述提供（见 FontFactory.BuildCssFont），
        /// 用于让 IME 候选窗与字形尺寸对齐；界面上的文字并不由 DOM 呈现。
        /// </summary>
        public static void Show(
            double cx, double cy, double cw, double ch,
            double fontPx, int color, string value, bool password, int maxLength, bool multiline,
            string cssFont)
        {
            IsActive = true;
            // transparent 恒为 true：DOM 只作 IME / 键盘捕获代理。
            JSBind_InputHtmlIme.Show(cx, cy, cw, ch, fontPx, color, value ?? string.Empty,
                password, maxLength, multiline, cssFont ?? "10px sans-serif", true);
        }

        public static void Hide()
        {
            IsActive = false;
            JSBind_InputHtmlIme.Hide();
        }

        public static void Reposition(double cx, double cy, double cw, double ch)
            => JSBind_InputHtmlIme.Reposition(cx, cy, cw, ch);

        public static void SetValue(string value) => JSBind_InputHtmlIme.SetValue(value);

        public static string GetValue() => JSBind_InputHtmlIme.GetValue() ?? string.Empty;
    }
}
