using System;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 浏览器原生文本输入（IME）的 C# 封装，API 形态对齐 Unity 的 <c>TouchScreenKeyboard</c>
    public static class Input_IME
    {
        /// <summary>覆盖层当前是否正在接管输入（由 <see cref="Open"/> / <see cref="Close"/> 维护）。</summary>
        public static bool Active { get; private set; }
        public static string Text { get; private set; } = string.Empty;

        /// <summary>原生覆盖层转发的控制键（来自 JSBind_Input_IME.OnKeyDown）。
        /// 参数为 (key, ctrl, shift, alt)。TextBox 订阅此事件以维护光标 / 选区。</summary>
        public static event Action<string, bool, bool, bool> KeyDown;

        /// <summary>原生覆盖层转发的文本编辑结果（来自 JSBind_Input_IME.OnDomValue，含 DOM 真实光标）。
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
            JSBind_Input_IME.Show(cx, cy, cw, ch, fontPx, color, value ?? string.Empty,
                password, maxLength, multiline, cssFont ?? "10px sans-serif", true);
        }

        public static void Deactivate()
        {
            Active = false;
            Text = string.Empty;
            JSBind_Input_IME.Hide();
        }

        /// <summary>激活装置：IME 由 <see cref="Open"/> 显式激活覆盖层，无需统一激活（空实现）。</summary>
        public static void Activate() { }

        /// <summary>由 JSBind_Input_IME.OnKeyDown 调用：把控制键以事件形式转发给订阅方。</summary>
        internal static void RouteKeyDown(string key, bool ctrl, bool shift, bool alt)
            => KeyDown?.Invoke(key, ctrl, shift, alt);

        /// <summary>由 JSBind_Input_IME.OnDomValue 调用：把文本编辑结果以事件形式转发给订阅方。</summary>
        internal static void RouteDomValue(string value, int selStart, int selEnd, bool composing)
            => DomValue?.Invoke(value, selStart, selEnd, composing);

        public static void Update()
        {
            // 未激活时跳过 DOM 拉取，并清掉上一帧的回车上升沿。
            if (!Active)
            {
                return;
            }

            Text = JSBind_Input_IME.GetValue() ?? string.Empty;
        }
    }
}
