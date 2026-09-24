using SlimDX.Direct3D9;

// 文本渲染桥接。对齐原版 System.Windows.Forms.TextRenderer 的 API 形态：
// 测量/绘制一律直接接收 Font，不走 css 字符串中转。
// 原版 API：
//   TextRenderer.MeasureText(g, text, font[, proposedSize, flags])  -> Size
//   TextRenderer.DrawText(g, text, font, bounds, foreColor[, flags]) -> void
// 原版走 GDI，本端口把文字绘制进控件离屏纹理（RenderTarget2D），度量/绘制复用 Font 上的 SpriteFont 通道。
// 仅 DOM 覆盖层字体仍需 css，由 FontToCss 提供（与 in-canvas 渲染口径一致）。
public static class TextRenderer
{
    public static string FontToCss(Font font)
    {
        if (font == null) return "10px sans-serif";
        return EnsureFontFallback(font.ToCss());
    }

    // 字体族始终带一个系统 sans-serif 兜底：中文专用/子集化字体常不含 ASCII 数字、标点等字形，
    // 缺少时由系统字体补齐。仅用于 DOM 覆盖层字体（需把 Font 还原成 css 交给浏览器）。
    private static string EnsureFontFallback(string css)
    {
        if (string.IsNullOrEmpty(css)) return css;
        if (css.Contains("sans-serif", StringComparison.OrdinalIgnoreCase) ||
            css.Contains("serif", StringComparison.OrdinalIgnoreCase) ||
            css.Contains("monospace", StringComparison.OrdinalIgnoreCase))
            return css;
        return css + ", sans-serif";
    }

    // 与 DrawLabel 共用同一 SpriteFont 度量，供控件（如 NPC 链接叠层）按 canvas 实际渲染位置定位。
    public static float MeasureCharWidth(char ch, Font font)
    {
        if (ch == '\n' || ch == '\r') return 0f;
        if (font == null) return 0f;
        return font.MeasureCharWidth(ch);
    }

    // 对齐原版 TextRenderer.MeasureText：直接接收 Font。
    public static float MeasureStringWidth(string text, Font font)
    {
        if (string.IsNullOrEmpty(text) || font == null) return 0f;
        return font.MeasureWidthF(text);
    }

    // 对齐原版 TextRenderer.MeasureText(g, text, font, proposedSize)（proposedSize.Width 限制时按词/字折行）。
    public static Size MeasureText(string text, Font font, int maxWidth)
    {
        if (font == null) return Size.Empty;
        return font.MeasureText(text, new Size(maxWidth, int.MaxValue));
    }

    // 对齐原版 TextRenderer.MeasureText(g, text, font, proposedSize, flags)。
    public static Size MeasureText(string text, Font font, Size proposedSize, TextFormatFlags flags)
    {
        if (font == null) return Size.Empty;
        return font.MeasureText(text, proposedSize, flags);
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

    // Font 版绘制核心：按 TextFormatFlags 在 (w,h) 区域内对齐、按词/字折行、可选描边。
    // DrawLabel / DrawText(Rectangle) 都复用它。
    private static void DrawInto(Texture texture, Font font, string text,
        int w, int h, int x, int y, int foreColor, int outlineColor, int backColor, TextFormatFlags drawFormat)
    {
        if (texture?.RenderTarget == null) return;

        var saved = BeginOnTexture(texture, backColor);
        try
        {
            if (string.IsNullOrEmpty(text)) return;

            var sf = font?.GetFont();
            if (sf == null) return;

            var batch = Client.MirGraphics.DXManager.Batch;
            batch.Begin(KFramework.MonoGame.SpriteSortMode.Deferred,
                        KFramework.MonoGame.BlendState.NonPremultiplied,
                        KFramework.MonoGame.SamplerState.PointClamp);

            bool hCenter = (drawFormat & TextFormatFlags.HorizontalCenter) != 0;
            bool hRight = (drawFormat & TextFormatFlags.Right) != 0;
            bool vCenter = (drawFormat & TextFormatFlags.VerticalCenter) != 0;
            bool vBottom = (drawFormat & TextFormatFlags.Bottom) != 0;
            bool wordBreak = (drawFormat & TextFormatFlags.WordBreak) != 0;

            var lines = font.WrapLines(text, w, wordBreak);
            float lineH = sf.LineHeight;
            float totalH = lineH * lines.Count;

            float startY = y;
            if (vCenter) startY = y + Math.Max(0f, (h - totalH) / 2f);
            else if (vBottom) startY = y + Math.Max(0f, h - totalH);

            foreach (var line in lines)
            {
                float lineW = sf.Measure(line).X;
                float tx = x;
                if (hCenter) tx = x + Math.Max(0f, (w - lineW) / 2f);
                else if (hRight) tx = x + Math.Max(0f, w - lineW);

                DrawStringWithOutline(batch, sf, line, tx, startY, foreColor, outlineColor);
                startY += lineH;
            }

            batch.End();
        }
        finally
        {
            EndOnTexture(saved);
        }
    }

    // 对齐原版 TextRenderer.DrawText(g, text, font, bounds, foreColor, flags)。
    public static void DrawText(Texture texture, string text, Font font,
        KFramework.MonoGame.Rectangle bounds, int foreColor, TextFormatFlags flags)
    {
        DrawInto(texture, font, text, bounds.Width, bounds.Height, bounds.X, bounds.Y,
                 foreColor, 0, 0, flags);
    }

    // 旧式 (x,y) 单点绘制（不带折行/对齐），保留以兼容既有调用。
    public static void DrawText(Texture texture, int x, int y, string text, Font font, int foreColor, int backColor, int outlineColor, int drawFormat)
    {
        if (texture?.RenderTarget == null) return;

        var saved = BeginOnTexture(texture, backColor);
        try
        {
            if (string.IsNullOrEmpty(text)) return;

            var sf = font?.GetFont();
            if (sf == null) return;

            var batch = Client.MirGraphics.DXManager.Batch;
            batch.Begin(KFramework.MonoGame.SpriteSortMode.Deferred,
                        KFramework.MonoGame.BlendState.NonPremultiplied,
                        KFramework.MonoGame.SamplerState.PointClamp);

            var pos = new KFramework.MonoGame.Vector2(x, y);
            var fore = KFramework.MonoGame.Color.FromArgb((uint)foreColor);

            if (outlineColor != 0)
            {
                var outline = KFramework.MonoGame.Color.FromArgb((uint)outlineColor);
                sf.Draw(batch, text, new KFramework.MonoGame.Vector2(x - 1, y), outline, 0f, KFramework.MonoGame.Vector2.Zero, 1f, KFramework.MonoGame.SpriteEffects.None, 0f);
                sf.Draw(batch, text, new KFramework.MonoGame.Vector2(x + 1, y), outline, 0f, KFramework.MonoGame.Vector2.Zero, 1f, KFramework.MonoGame.SpriteEffects.None, 0f);
                sf.Draw(batch, text, new KFramework.MonoGame.Vector2(x, y - 1), outline, 0f, KFramework.MonoGame.Vector2.Zero, 1f, KFramework.MonoGame.SpriteEffects.None, 0f);
                sf.Draw(batch, text, new KFramework.MonoGame.Vector2(x, y + 1), outline, 0f, KFramework.MonoGame.Vector2.Zero, 1f, KFramework.MonoGame.SpriteEffects.None, 0f);
            }

            sf.Draw(batch, text, pos, fore, 0f, KFramework.MonoGame.Vector2.Zero, 1f, KFramework.MonoGame.SpriteEffects.None, 0f);
            batch.End();
        }
        finally
        {
            EndOnTexture(saved);
        }
    }

    // 控件标签绘制（带描边/背景）。shadow 当前未在引擎侧生效，保留以对齐调用方签名。
    public static void DrawLabel(Texture texture, int w, int h, string text, Font font,
        int foreColor, int outlineColor, TextFormatFlags drawFormat, int backColor, int x, int y, bool shadow)
    {
        DrawInto(texture, font, text, w, h, x, y, foreColor, outlineColor, backColor, drawFormat);
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

    // 文本框自绘（DOM 未接管时，由引擎在离屏纹理上绘制文字与光标）。
    // 说明：原版 TextBox 是 WinForm 原生控件、不走 TextRenderer；此方法仅为在 canvas 上还原文本框外观而设的
    // 端口辅助方法，仍直接接收 Font（不传 css）。
    public static void DrawTextBox(Texture texture, int w, int h, string text, Font font, int foreColor, int backColor, int selBackColor, int textColor, int selectionStart, int selectionLength, int caretPos, bool focused, bool multiline)
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

            var sf = font?.GetFont();
            if (sf == null)
            {
                KFramework.MonoGame.PrintTool.Log($"[DrawTextBox] font null");
                return;
            }

            float padLeft = 3f;
            float ty = multiline ? 2f : Math.Max(0f, (h - sf.LineHeight) / 2f);
            var pos = new KFramework.MonoGame.Vector2(padLeft, ty);
            var fore = KFramework.MonoGame.Color.FromArgb((uint)textColor);

            if (!string.IsNullOrEmpty(text))
            {
                sf.Draw(batch, text, pos, fore, 0f, KFramework.MonoGame.Vector2.Zero, 1f, KFramework.MonoGame.SpriteEffects.None, 0f);
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
                    float caretX = padLeft + sf.Measure(prefix).X;
                    float caretW = Math.Max(1f, font.Size * 0.08f);
                    float caretH = sf.LineHeight;
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
}
