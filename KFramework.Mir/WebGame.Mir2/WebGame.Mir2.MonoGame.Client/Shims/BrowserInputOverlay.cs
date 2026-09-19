using System;

namespace MirEngine
{
    /// <summary>
    /// 浏览器原生文本输入覆盖层（DOM &lt;input&gt;/&lt;textarea&gt;）的业务侧包装。
    ///
    /// 真正的 JS 互操作在引擎 <see cref="KFramework.MonoGame.JSBind_InputOverlay"/>（模块 "input_overlay"，
    /// 见 KFramework.TSEngine/src/input_overlay.ts）。本类只暴露给 MirTextBox 使用的纯 C# API 与事件，
    /// 由 JSBind 层把 DOM 的 input / Enter / blur 三类事件转交上来：
    ///   - ValueChanged → 同步 TextBox.Text；
    ///   - Enter        → 模拟回车确认（登录）；
    ///   - Blur         → 焦点切换 / 隐藏原生输入框。
    /// </summary>
    public static class BrowserInputOverlay
    {
        public static event EventHandler<string> ValueChanged
        {
            add => KFramework.MonoGame.JSBind_InputOverlay.ValueChanged += value;
            remove => KFramework.MonoGame.JSBind_InputOverlay.ValueChanged -= value;
        }

        public static event EventHandler Enter
        {
            add => KFramework.MonoGame.JSBind_InputOverlay.Enter += value;
            remove => KFramework.MonoGame.JSBind_InputOverlay.Enter -= value;
        }

        public static event EventHandler Blur
        {
            add => KFramework.MonoGame.JSBind_InputOverlay.Blur += value;
            remove => KFramework.MonoGame.JSBind_InputOverlay.Blur -= value;
        }

        public static void Show(double cx, double cy, double cw, double ch, double fontPx, int color, string value, bool password, int maxLength, bool multiline)
            => KFramework.MonoGame.JSBind_InputOverlay.Show(cx, cy, cw, ch, fontPx, color, value, password, maxLength, multiline);

        public static void Hide()
            => KFramework.MonoGame.JSBind_InputOverlay.Hide();

        public static void Reposition(double cx, double cy, double cw, double ch)
            => KFramework.MonoGame.JSBind_InputOverlay.Reposition(cx, cy, cw, ch);

        public static void SetValue(string value)
            => KFramework.MonoGame.JSBind_InputOverlay.SetValue(value);

        public static string GetValue()
            => KFramework.MonoGame.JSBind_InputOverlay.GetValue() ?? string.Empty;
    }
}
