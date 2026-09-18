using System;

namespace MirEngine
{
    // 浏览器端 GDI 文本渲染桥接。原版走 Canvas 2D（mir.cr* JS）。
    // 迁移到 KFramework.MonoGame 后，控件文字真实绘制需走其文本/纹理通道（jsengine/text.js），
    // 当前先提供编译所需的签名：FontToCss 可直接用 Font.ToCss() 实现；MeasureText 给出近似尺寸（避免控件尺寸塌缩）；
    // DrawLabel/DrawTextBox/DrawText 暂为无操作桩——控件文字（按钮/标签/输入框）暂不显示，
    // 待接入 KFramework 文本 API 后再实现真实绘制。
    public static class BrowserCanvas
    {
        public static string FontToCss(Font font)
        {
            if (font == null) return "10px sans-serif";
            return font.ToCss();
        }

        public static Size MeasureText(string text, string css, int maxWidth)
        {
            int px = ParsePx(css);
            int w = string.IsNullOrEmpty(text) ? 0 : (int)(text.Length * px * 0.62) + 4;
            int h = px + 4;
            if (maxWidth > 0 && w > maxWidth)
            {
                h = (int)(h * Math.Ceiling((double)w / maxWidth));
                w = maxWidth;
            }
            return new Size(w, h);
        }

        private static int ParsePx(string css)
        {
            if (string.IsNullOrEmpty(css)) return 12;
            int i = css.IndexOf("px");
            if (i > 0)
            {
                int j = i - 1;
                while (j >= 0 && char.IsDigit(css[j])) j--;
                if (int.TryParse(css.Substring(j + 1, i - j - 1), out int v)) return v;
            }
            return 12;
        }

        public static void DrawLabel(int handle, int w, int h, string text, string css, int foreColor, int outlineColor, int drawFormat, int backColor, int x, int y, bool shadow) { }
        public static void DrawTextBox(int handle, int w, int h, string text, string css, int foreColor, int backColor, int selBackColor, int textColor, int selectionStart, int selectionLength, int caretPos, bool focused, bool multiline) { }
        public static void DrawText(int handle, int x, int y, string text, string css, int foreColor, int backColor, int outlineColor, int drawFormat) { }

        // 兼容其他可能的调用点（DXManager 已不再使用），保留空实现。
        public static void DrawImage(int handle, int x, int y, int w, int h, int srcX, int srcY, int srcW, int srcH, float opacity = 1f) { }
        public static void UploadImage(int id, byte[] data, int w, int h) { }
        public static void Clear(Color color) { }
        public static void Flush() { }
        public static void CreateOffscreen(int w, int h) { }
        public static void DisposeImage(int handle) { }
        public static void SetTarget(int handle) { }
        public static void SetBlendProfile(string name) { }
    }
}
