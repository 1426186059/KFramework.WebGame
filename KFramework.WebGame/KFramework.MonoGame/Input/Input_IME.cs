using System;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 浏览器原生文本输入（IME）的 C# 封装，API 形态对齐 Unity 的 <c>TouchScreenKeyboard</c>：
    /// <list type="bullet">
    ///   <item><see cref="Open"/> 类比 <c>TouchScreenKeyboard.Open</c>：在指定位置激活覆盖层并聚焦（额外携带屏幕坐标，因为我们用透明 DOM &lt;input&gt; 覆盖在 canvas 上）。</item>
    ///   <item><see cref="Close"/> 类比 <c>keyboard.Close</c>：关闭覆盖层。</item>
    ///   <item><see cref="Active"/> 类比 <c>keyboard.active</c>：是否仍在接管输入。</item>
    /// </list>
    ///
    /// <para>架构（对齐 UGUI InputField）：<b>引擎 TextBox 持有 text / 光标 / 选区 / IME 预览，是文本与光标的唯一真相源</b>。
    /// DOM &lt;input&gt; 仅是“按键 / IME 捕获代理”：控制键经 <c>BrowserInputIme</c> 转发给引擎（引擎自行维护光标），
    /// 可打印字符与 IME 组字由 DOM 原生处理后，经 <c>OnDomValue</c> 回传，引擎再按<b>自身光标</b>合并进 text。
    /// 引擎每改动一次就把 text + 光标写回 DOM（<see cref="SyncActive"/>），但绝不反向读取 DOM 的光标位置。</para>
    /// </summary>
    public static class Input_IME
    {
        /// <summary>覆盖层当前是否正在接管输入（由 <see cref="Open"/> / <see cref="Close"/> 维护）。</summary>
        public static bool Active { get; private set; }

        /// <summary>当前激活文本框的引擎文本（仅兼容读取；写入请走 TextBox.InsertText）。</summary>
        public static string Text => TextBox.ActiveTextBox?.Text ?? string.Empty;

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
            JSBind_InputHtmlIme.Hide();
        }

        /// <summary>每帧由 <see cref="Input.Poll"/> 调用；文本与光标由事件驱动，这里无需再拉取 DOM。</summary>
        public static void Poll()
        {
        }

        /// <summary>
        /// 把引擎文本框的状态（text + IME 预览 + 光标）写回 DOM 覆盖层，
        /// 使浏览器 IME 候选窗 / 文本选区与引擎保持一致。引擎是真相源，DOM 只是镜像。
        /// </summary>
        internal static void SyncActive(TextBox tb)
        {
            if (!Active || tb == null || tb.IsDisposed) return;
            string v = tb.Text + tb.CompositionString;
            int caret = tb.SelectionStart + tb.CompositionString.Length;
            // 组字预览期间仅写回文本（且与 el.value 相同，value 赋值被守卫为 no-op），
            // 不写回光标区间——某些浏览器下 setSelectionRange 会打断 IME 组字。
            JSBind_InputHtmlIme.SetValue(v);
            if (string.IsNullOrEmpty(tb.CompositionString))
                JSBind_InputHtmlIme.SetSelectionRange(caret, caret + tb.SelectionLength);
        }
    }
}
