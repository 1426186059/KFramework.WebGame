using System;
using System.Collections.Generic;
using SlimDX.Direct3D9;

namespace MirEngine
{
    // 浏览器端 GDI 文本渲染桥接。原版走 Canvas 2D（mir.cr* JS）。
    // 迁移到 KFramework.MonoGame 后，控件文字真实绘制走引擎字体通道（SpriteFont / SpriteBatch），
    // 把文字栅格化（jsengine/text.js）后绘制进控件离屏纹理（RenderTarget2D）。
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
                while (j >= 0 && (char.IsDigit(css[j]) || css[j] == '.')) j--;
                if (float.TryParse(css.Substring(j + 1, i - j - 1), out float v)) return (int)Math.Round(v);
            }
            return 12;
        }

        // ---- 字体缓存：同一 css 复用同一个 SpriteFont（含其字形图集纹理）----
        private static readonly Dictionary<string, KFramework.MonoGame.SpriteFont> _fontCache = new Dictionary<string, KFramework.MonoGame.SpriteFont>();

        private static KFramework.MonoGame.SpriteFont GetFont(string css)
        {
            if (string.IsNullOrEmpty(css)) css = "10px sans-serif";
            if (_fontCache.TryGetValue(css, out var cached) && cached != null) return cached;

            bool bold = css.Contains("bold");
            float size = 12f;
            string family = "sans-serif";
            int pxIdx = css.IndexOf("px");
            if (pxIdx > 0)
            {
                int j = pxIdx - 1;
                // 注意：字号可能是小数（如 13.333333），小数点 '.' 也要算作字号的一部分
                while (j >= 0 && (char.IsDigit(css[j]) || css[j] == '.')) j--;
                if (float.TryParse(css.Substring(j + 1, pxIdx - j - 1), out float v)) size = v;
                int after = pxIdx + 2;
                if (after < css.Length) family = css.Substring(after).Trim();
            }

            var font = new KFramework.MonoGame.SpriteFont(Client.MirGraphics.DXManager.GDevice, size, family, bold);
            _fontCache[css] = font;
            return font;
        }

        // 绑定控件离屏纹理为当前渲染目标，按 clearArgb 清屏（alpha==0 时清成透明），返回原渲染目标以便恢复。
        private static SlimDX.Direct3D9.Surface BeginOnTexture(Texture texture, int clearArgb)
        {
            var saved = Client.MirGraphics.DXManager.CurrentSurface;
            Client.MirGraphics.DXManager.SetSurface(new SlimDX.Direct3D9.Surface(texture));
            var clear = ((clearArgb >> 24) & 0xFF) != 0
                ? KFramework.MonoGame.Color.FromArgb((uint)clearArgb)
                : KFramework.MonoGame.Color.Transparent;
            Client.MirGraphics.DXManager.GDevice.Clear(clear);
            return saved;
        }

        private static void EndOnTexture(SlimDX.Direct3D9.Surface saved)
        {
            Client.MirGraphics.DXManager.SetSurface(saved);
        }

        public static void DrawLabel(Texture texture, int w, int h, string text, string css, int foreColor, int outlineColor, int drawFormat, int backColor, int x, int y, bool shadow)
        {
            if (texture?.RenderTarget == null) return;

            var saved = BeginOnTexture(texture, backColor);
            try
            {
                if (string.IsNullOrEmpty(text)) return;

                var font = GetFont(css);
                if (font == null) return;

                var batch = Client.MirGraphics.DXManager.Batch;
                batch.Begin(KFramework.MonoGame.SpriteSortMode.Deferred,
                            KFramework.MonoGame.BlendState.NonPremultiplied,
                            KFramework.MonoGame.SamplerState.PointClamp);

                // 简单水平对齐：drawFormat 含 Center(1) / Far(2)
                float tx = x;
                float ty = y;
                float textW = font.MeasureString(text).X;
                if ((drawFormat & 1) != 0) tx = x + Math.Max(0f, (w - textW) / 2f);
                else if ((drawFormat & 2) != 0) tx = x + Math.Max(0f, w - textW);

                var pos = new KFramework.MonoGame.Vector2(tx, ty);
                var fore = KFramework.MonoGame.Color.FromArgb((uint)foreColor);

                if (outlineColor != 0)
                {
                    var outline = KFramework.MonoGame.Color.FromArgb((uint)outlineColor);
                    font.Draw(batch, text, new KFramework.MonoGame.Vector2(tx - 1, ty), outline, 0f, KFramework.MonoGame.Vector2.Zero, 1f, KFramework.MonoGame.SpriteEffects.None, 0f);
                    font.Draw(batch, text, new KFramework.MonoGame.Vector2(tx + 1, ty), outline, 0f, KFramework.MonoGame.Vector2.Zero, 1f, KFramework.MonoGame.SpriteEffects.None, 0f);
                    font.Draw(batch, text, new KFramework.MonoGame.Vector2(tx, ty - 1), outline, 0f, KFramework.MonoGame.Vector2.Zero, 1f, KFramework.MonoGame.SpriteEffects.None, 0f);
                    font.Draw(batch, text, new KFramework.MonoGame.Vector2(tx, ty + 1), outline, 0f, KFramework.MonoGame.Vector2.Zero, 1f, KFramework.MonoGame.SpriteEffects.None, 0f);
                }

                font.Draw(batch, text, pos, fore, 0f, KFramework.MonoGame.Vector2.Zero, 1f, KFramework.MonoGame.SpriteEffects.None, 0f);
                batch.End();
            }
            finally
            {
                EndOnTexture(saved);
            }
        }

        public static void DrawTextBox(Texture texture, int w, int h, string text, string css, int foreColor, int backColor, int selBackColor, int textColor, int selectionStart, int selectionLength, int caretPos, bool focused, bool multiline)
        {
            if (texture?.RenderTarget == null)
            {
                System.Console.WriteLine($"[DrawTextBox] SKIP rt=null text='{text}' focused={focused}");
                return;
            }

            var saved = BeginOnTexture(texture, backColor);
            var batch = Client.MirGraphics.DXManager.Batch;
            try
            {
                batch.Begin(KFramework.MonoGame.SpriteSortMode.Deferred,
                            KFramework.MonoGame.BlendState.NonPremultiplied,
                            KFramework.MonoGame.SamplerState.PointClamp);

                var font = GetFont(css);
                if (font == null)
                {
                    System.Console.WriteLine($"[DrawTextBox] font null css='{css}'");
                    return;
                }

                float padLeft = 3f;
                float ty = multiline ? 2f : Math.Max(0f, (h - font.LineHeight) / 2f);
                var pos = new KFramework.MonoGame.Vector2(padLeft, ty);
                var fore = KFramework.MonoGame.Color.FromArgb((uint)textColor);

                if (!string.IsNullOrEmpty(text))
                {
                    font.Draw(batch, text, pos, fore, 0f, KFramework.MonoGame.Vector2.Zero, 1f, KFramework.MonoGame.SpriteEffects.None, 0f);
                }

                // DOM 覆盖层透明后，光标由引擎自绘：focused 时画一条竖线。
                // caret 绘制若异常绝不能影响上面文字的提交，故单独保护并打印。
                if (focused)
                {
                    try
                    {
                        string prefix = (text != null && caretPos > 0)
                            ? text.Substring(0, Math.Min(caretPos, text.Length))
                            : "";
                        float caretX = padLeft + font.Measure(prefix).X;
                        float caretW = Math.Max(1f, font.Size * 0.08f);
                        float caretH = font.LineHeight;
                        batch.Draw(WhitePixel(),
                                   new KFramework.MonoGame.Rectangle(
                                       (int)Math.Round(caretX), (int)Math.Round(ty),
                                       (int)Math.Round(caretW), (int)Math.Round(caretH)),
                                   fore);
                    }
                    catch (System.Exception ex)
                    {
                        System.Console.WriteLine($"[DrawTextBox] caret EX: {ex}");
                    }
                }
            }
            finally
            {
                batch.End();
                EndOnTexture(saved);
            }
        }

        // 复用的 1x1 白纹理：用于画 caret / 选区等纯色矩形。
        private static KFramework.MonoGame.Texture2D _whitePixel;
        private static KFramework.MonoGame.Texture2D WhitePixel()
        {
            if (_whitePixel == null)
            {
                var dev = Client.MirGraphics.DXManager.GDevice;
                _whitePixel = dev.CreateTexture(1, 1);
                _whitePixel.SetData(new byte[] { 255, 255, 255, 255 }, 0, 0, 1, 1);
            }
            return _whitePixel;
        }

        public static void DrawText(Texture texture, int x, int y, string text, string css, int foreColor, int backColor, int outlineColor, int drawFormat)
        {
            if (texture?.RenderTarget == null) return;

            var saved = BeginOnTexture(texture, backColor);
            try
            {
                if (string.IsNullOrEmpty(text)) return;

                var font = GetFont(css);
                if (font == null) return;

                var batch = Client.MirGraphics.DXManager.Batch;
                batch.Begin(KFramework.MonoGame.SpriteSortMode.Deferred,
                            KFramework.MonoGame.BlendState.NonPremultiplied,
                            KFramework.MonoGame.SamplerState.PointClamp);

                var pos = new KFramework.MonoGame.Vector2(x, y);
                var fore = KFramework.MonoGame.Color.FromArgb((uint)foreColor);

                if (outlineColor != 0)
                {
                    var outline = KFramework.MonoGame.Color.FromArgb((uint)outlineColor);
                    font.Draw(batch, text, new KFramework.MonoGame.Vector2(x - 1, y), outline, 0f, KFramework.MonoGame.Vector2.Zero, 1f, KFramework.MonoGame.SpriteEffects.None, 0f);
                    font.Draw(batch, text, new KFramework.MonoGame.Vector2(x + 1, y), outline, 0f, KFramework.MonoGame.Vector2.Zero, 1f, KFramework.MonoGame.SpriteEffects.None, 0f);
                    font.Draw(batch, text, new KFramework.MonoGame.Vector2(x, y - 1), outline, 0f, KFramework.MonoGame.Vector2.Zero, 1f, KFramework.MonoGame.SpriteEffects.None, 0f);
                    font.Draw(batch, text, new KFramework.MonoGame.Vector2(x, y + 1), outline, 0f, KFramework.MonoGame.Vector2.Zero, 1f, KFramework.MonoGame.SpriteEffects.None, 0f);
                }

                font.Draw(batch, text, pos, fore, 0f, KFramework.MonoGame.Vector2.Zero, 1f, KFramework.MonoGame.SpriteEffects.None, 0f);
                batch.End();
            }
            finally
            {
                EndOnTexture(saved);
            }
        }

        // 兼容其他可能的调用点（DXManager 已不再使用），保留空实现。
        public static void DrawImage(int handle, int x, int y, int w, int h, int srcX, int srcY, int srcW, int srcH, float opacity = 1f) { }
        public static void UploadImage(int id, byte[] data, int w, int h) { }
        public static void Clear(MirEngine.Color color) { }
        public static void Flush() { }
        public static void CreateOffscreen(int w, int h) { }
        public static void DisposeImage(int handle) { }
        public static void SetTarget(int handle) { }
        public static void SetBlendProfile(string name) { }

        // DrawLabel/DrawTextBox/DrawText 现在走上面基于 KFramework.MonoGame.SpriteFont 的真实绘制。
    }
}
