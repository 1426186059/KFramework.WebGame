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

        // 与 DrawLabel 共用同一 SpriteFont 度量，供控件（如 NPC 链接叠层）按 canvas 实际渲染位置定位。
        public static float MeasureCharWidth(char ch, string css)
        {
            if (ch == '\n' || ch == '\r') return 0f;
            var font = GetFont(css);
            if (font == null) return 0f;
            return font.MeasureString(ch.ToString()).X;
        }

        public static float LineHeight(string css)
        {
            var font = GetFont(css);
            if (font == null) return 0f;
            return font.LineHeight;
        }

        public static float MeasureStringWidth(string text, string css)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            var font = GetFont(css);
            if (font == null) return 0f;
            return font.MeasureString(text).X;
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

        // drawFormat 直接用客户端 Shims/MirEngineForms.cs 的 TextFormatFlags 位定义，
        // 不再用“Center=1 / Right=2”的硬编码猜测（那套位值和实际枚举对不上，导致对齐全部失效）。
        public static void DrawLabel(Texture texture, int w, int h, string text, string css, int foreColor, int outlineColor, TextFormatFlags drawFormat, int backColor, int x, int y, bool shadow)
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

                bool hCenter = (drawFormat & TextFormatFlags.HorizontalCenter) != 0;
                bool hRight  = (drawFormat & TextFormatFlags.Right) != 0;
                bool vCenter = (drawFormat & TextFormatFlags.VerticalCenter) != 0;
                bool vBottom = (drawFormat & TextFormatFlags.Bottom) != 0;
                bool wordBreak = (drawFormat & TextFormatFlags.WordBreak) != 0;

                var lines = WrapLines(text, font, w, wordBreak);
                float lineH = font.LineHeight;
                float totalH = lineH * lines.Count;

                float startY = y;
                if (vCenter) startY = y + Math.Max(0f, (h - totalH) / 2f);
                else if (vBottom) startY = y + Math.Max(0f, h - totalH);

                foreach (var line in lines)
                {
                    float lineW = font.Measure(line).X;
                    float tx = x;
                    if (hCenter) tx = x + Math.Max(0f, (w - lineW) / 2f);
                    else if (hRight) tx = x + Math.Max(0f, w - lineW);

                    DrawStringWithOutline(batch, font, line, tx, startY, foreColor, outlineColor);
                    startY += lineH;
                }

                batch.End();
            }
            finally
            {
                EndOnTexture(saved);
            }
        }

        // 按显式 '\n' 切分；带 WordBreak 时在宽度 w 内按词折行，连续中文/长串退化逐字折行。
        private static List<string> WrapLines(string text, KFramework.MonoGame.SpriteFont font, int maxWidth, bool wordBreak)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(text)) return result;

            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Replace("\r", "");
                if (!wordBreak || maxWidth <= 0)
                {
                    result.Add(line);
                    continue;
                }

                var words = line.Split(' ');
                var current = new System.Text.StringBuilder();
                foreach (var word in words)
                {
                    if (current.Length > 0 && font.Measure(current.ToString() + " " + word).X > maxWidth && font.Measure(word).X <= maxWidth)
                    {
                        result.Add(current.ToString());
                        current.Clear();
                        current.Append(word);
                    }
                    else if (font.Measure(word).X > maxWidth)
                    {
                        // 单个词（连续中文/长串）仍超宽：逐字折行
                        foreach (var ch in word)
                        {
                            if (current.Length > 0 && font.Measure(current.ToString() + ch).X > maxWidth)
                            {
                                result.Add(current.ToString());
                                current.Clear();
                            }
                            current.Append(ch);
                        }
                    }
                    else
                    {
                        current.Append(current.Length == 0 ? word : " " + word);
                    }
                }
                if (current.Length > 0) result.Add(current.ToString());
            }
            return result;
        }

        // 带描边文字绘制：描边在四个方向各偏移 1px，最后画前景。
        private static void DrawStringWithOutline(KFramework.MonoGame.SpriteBatch batch, KFramework.MonoGame.SpriteFont font, string line, float tx, float ty, int foreColor, int outlineColor)
        {
            var fore = KFramework.MonoGame.Color.FromArgb((uint)foreColor);
            if (outlineColor != 0)
            {
                var outline = KFramework.MonoGame.Color.FromArgb((uint)outlineColor);
                font.Draw(batch, line, new KFramework.MonoGame.Vector2(tx - 1, ty), outline, 0f, KFramework.MonoGame.Vector2.Zero, 1f, KFramework.MonoGame.SpriteEffects.None, 0f);
                font.Draw(batch, line, new KFramework.MonoGame.Vector2(tx + 1, ty), outline, 0f, KFramework.MonoGame.Vector2.Zero, 1f, KFramework.MonoGame.SpriteEffects.None, 0f);
                font.Draw(batch, line, new KFramework.MonoGame.Vector2(tx, ty - 1), outline, 0f, KFramework.MonoGame.Vector2.Zero, 1f, KFramework.MonoGame.SpriteEffects.None, 0f);
                font.Draw(batch, line, new KFramework.MonoGame.Vector2(tx, ty + 1), outline, 0f, KFramework.MonoGame.Vector2.Zero, 1f, KFramework.MonoGame.SpriteEffects.None, 0f);
            }
            font.Draw(batch, line, new KFramework.MonoGame.Vector2(tx, ty), fore, 0f, KFramework.MonoGame.Vector2.Zero, 1f, KFramework.MonoGame.SpriteEffects.None, 0f);
        }

        public static void DrawTextBox(Texture texture, int w, int h, string text, string css, int foreColor, int backColor, int selBackColor, int textColor, int selectionStart, int selectionLength, int caretPos, bool focused, bool multiline)
        {
            if (texture?.RenderTarget == null)
            {
                KFramework.MonoGame.PrintTool.Log($"[DrawTextBox] SKIP rt=null text='{text}' focused={focused}");
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
                    KFramework.MonoGame.PrintTool.Log($"[DrawTextBox] font null css='{css}'");
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
                        KFramework.MonoGame.PrintTool.Log($"[DrawTextBox] caret EX: {ex}");
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

        // DrawLabel/DrawTextBox/DrawText 现在走上面基于 KFramework.MonoGame.SpriteFont 的真实绘制。
    }
}
