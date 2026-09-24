using System;

namespace KFramework.MonoGame.TextRenderer
{
    /// <summary>
    /// 文本输入的原生覆盖层（浏览器 DOM &lt;input&gt;/&lt;textarea&gt;）业务侧封装，
    /// 基于引擎的 <see cref="JSBind_InputOverlay"/>（模块 "input_overlay"）。
    ///
    /// 它与 <see cref="TextCaretMode"/> 配合实现两种输入形态：
    ///   - Browser ：transparent=false，由 DOM 显示文字与光标（原生 IME 候选窗 / 选区 / 光标）；
    ///   - Rendered ：transparent=true，DOM 仅作 IME / 键盘捕获代理，文字与光标由引擎绘制。
    ///
    /// DOM 回传三类事件：ValueChanged（文本变化，含 IME 上屏）、Enter（回车）、Blur（失焦）。
    /// </summary>
    public static class TextInputOverlay
    {
        public static event EventHandler<string> ValueChanged
        {
            add => JSBind_InputOverlay.ValueChanged += value;
            remove => JSBind_InputOverlay.ValueChanged -= value;
        }

        public static event EventHandler Enter
        {
            add => JSBind_InputOverlay.Enter += value;
            remove => JSBind_InputOverlay.Enter -= value;
        }

        public static event EventHandler Blur
        {
            add => JSBind_InputOverlay.Blur += value;
            remove => JSBind_InputOverlay.Blur -= value;
        }

        /// <summary>在画布指定位置（后备缓冲像素）显示原生输入框并聚焦。</summary>
        /// <summary>
        /// 在画布指定位置（后备缓冲像素）显示原生输入框并聚焦。
        /// fontScale：字号的额外缩放（画布按视口高度缩放时，字号需与位置/尺寸同步缩放），默认 1.0。
        /// </summary>
        public static void Show(
            double cx, double cy, double cw, double ch,
            Font font, int color, string value, bool password, int maxLength, bool multiline,
            bool transparent, double fontScale = 1.0)
        {
            string css = BuildCssFont(font, fontScale);
            double fontPx = font != null ? font.GetPixelSize() * fontScale : 10d;
            JSBind_InputOverlay.Show(cx, cy, cw, ch, fontPx, color, value ?? string.Empty,
                password, maxLength, multiline, css, transparent);
        }

        public static void Hide() => JSBind_InputOverlay.Hide();

        public static void Reposition(double cx, double cy, double cw, double ch)
            => JSBind_InputOverlay.Reposition(cx, cy, cw, ch);

        public static void SetValue(string value) => JSBind_InputOverlay.SetValue(value);

        public static string GetValue() => JSBind_InputOverlay.GetValue() ?? string.Empty;

        /// <summary>把 Font 还原成完整 CSS 字体串（如 "bold 14px Tahoma"），供 DOM 覆盖层保持字形一致。</summary>
        public static string BuildCssFont(Font font, double fontScale = 1.0)
        {
            if (font == null) return "10px sans-serif";

            string style = font.Bold ? "bold " : (font.Italic ? "italic " : string.Empty);
            float px = (float)(font.GetPixelSize() * fontScale);

            string family = font.Name;
            if (string.IsNullOrEmpty(family)) family = "sans-serif";
            else if (family.IndexOf("sans-serif", StringComparison.OrdinalIgnoreCase) < 0 &&
                     family.IndexOf("serif", StringComparison.OrdinalIgnoreCase) < 0 &&
                     family.IndexOf("monospace", StringComparison.OrdinalIgnoreCase) < 0)
                family += ", sans-serif";

            return $"{style}{px.ToString(System.Globalization.CultureInfo.InvariantCulture)}px {family}";
        }

        /// <summary>
        /// 绘制一个"输入框"：文本按 <paramref name="bounds"/> 排版，并按 <paramref name="caret"/> 的模式决定是否绘制光标。
        /// Browser 模式下本方法只画文本（光标由 DOM 显示）；若调用方希望完全交给 DOM，可自行跳过绘制。
        /// </summary>
        public static void DrawTextBox(
            SpriteBatch batch, GraphicsDevice device, Font font, string text,
            Rectangle bounds, Color foreColor, int caretIndex, TextCaret caret,
            bool multiline, float padLeft = 3f)
        {
            if (batch == null || device == null || font == null) return;

            SpriteFont sf = TextRenderer.GetSpriteFont(device, font);
            if (sf == null) return;

            float lineH = sf.LineHeight;
            float y = multiline ? 2f : System.Math.Max(0f, (bounds.Height - lineH) / 2f);

            if (!string.IsNullOrEmpty(text))
            {
                sf.Draw(batch, text, new Vector2(bounds.X + padLeft, bounds.Y + y), foreColor,
                    0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            }

            caret?.Draw(batch, device, font, text, caretIndex, bounds, foreColor, multiline, padLeft);
        }
    }
}
