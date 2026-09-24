using SlimDX.Direct3D9;

// 对齐原版 System.Windows.Forms.TextRenderer 的 API 形态（仅 MeasureText / DrawText，全部接收 Font）：
//   MeasureText(string text, Font font[, TextFormatFlags flags])
//   MeasureText(string text, Font font, Size proposedSize[, TextFormatFlags flags])
//   MeasureText(Texture dc, string text, Font font[, TextFormatFlags flags | Size proposedSize [, TextFormatFlags flags]])
//   DrawText(Texture dc, string text, Font font, Point pt, int foreColor[, TextFormatFlags flags])
//   DrawText(Texture dc, string text, Font font, Rectangle bounds, int foreColor[, TextFormatFlags flags])
// 原版走 GDI；本端口把文字绘制进控件离屏纹理（RenderTarget2D），Font -> SpriteFont 的创建与缓存由本类内部管理。
//
// 注意：本文件不 using KFramework.MonoGame，避免 Texture / Color 与 shim 同名类型产生歧义
// （工程内约定：KFramework.MonoGame 类型一律完全限定，同 MirControl 等文件的做法）。
public static class TextRenderer
{
    // Font -> SpriteFont 缓存（按 族名|字号|单位|样式 缓存，进程内复用）。
    private static readonly System.Collections.Generic.Dictionary<string, KFramework.MonoGame.SpriteFont> _fontCache =
        new System.Collections.Generic.Dictionary<string, KFramework.MonoGame.SpriteFont>();

    internal static KFramework.MonoGame.SpriteFont GetSpriteFont(Font font)
    {
        if (font == null) return null;
        string key = $"{font.Name}|{font.Size}|{(int)font.Unit}|{(int)font.Style}";
        KFramework.MonoGame.SpriteFont cached;
        if (_fontCache.TryGetValue(key, out cached)) return cached;

        float px = font.Unit == MirEngine.GraphicsUnit.Pixel ? font.Size : font.Size * 4f / 3f;
        string family = font.Name;
        if (family.IndexOf("sans-serif", System.StringComparison.OrdinalIgnoreCase) < 0 &&
            family.IndexOf("serif", System.StringComparison.OrdinalIgnoreCase) < 0 &&
            family.IndexOf("monospace", System.StringComparison.OrdinalIgnoreCase) < 0)
            family += ", sans-serif";

        var device = Client.MirGraphics.DXManager.GDevice;
        var sf = new KFramework.MonoGame.SpriteFont(device, px, family, font.Bold);
        _fontCache[key] = sf;
        return sf;
    }

    // ---- MeasureText ----
    public static MirEngine.Size MeasureText(string text, Font font)
        => MeasureTextCore(text, font, 0, TextFormatFlags.Default);

    public static MirEngine.Size MeasureText(string text, Font font, TextFormatFlags flags)
        => MeasureTextCore(text, font, 0, flags);

    public static MirEngine.Size MeasureText(string text, Font font, MirEngine.Size proposedSize)
        => MeasureTextCore(text, font, proposedSize.Width, TextFormatFlags.Default);

    public static MirEngine.Size MeasureText(string text, Font font, MirEngine.Size proposedSize, TextFormatFlags flags)
        => MeasureTextCore(text, font, proposedSize.Width, flags);

    public static MirEngine.Size MeasureText(Texture dc, string text, Font font)
        => MeasureText(text, font);

    public static MirEngine.Size MeasureText(Texture dc, string text, Font font, TextFormatFlags flags)
        => MeasureText(text, font, flags);

    public static MirEngine.Size MeasureText(Texture dc, string text, Font font, MirEngine.Size proposedSize)
        => MeasureText(text, font, proposedSize);

    public static MirEngine.Size MeasureText(Texture dc, string text, Font font, MirEngine.Size proposedSize, TextFormatFlags flags)
        => MeasureText(text, font, proposedSize, flags);

    private static MirEngine.Size MeasureTextCore(string text, Font font, int maxWidth, TextFormatFlags flags)
    {
        if (font == null || string.IsNullOrEmpty(text)) return MirEngine.Size.Empty;
        var sf = GetSpriteFont(font);
        if (sf == null) return MirEngine.Size.Empty;

        bool wordBreak = (flags & TextFormatFlags.WordBreak) != 0 && maxWidth > 0;
        if (!wordBreak)
        {
            var v = sf.MeasureString(text);
            return new MirEngine.Size((int)System.Math.Ceiling(v.X), (int)System.Math.Ceiling(v.Y));
        }

        var lines = WrapLines(text, sf, maxWidth);
        int w = 0;
        foreach (var line in lines)
            if (!string.IsNullOrEmpty(line))
                w = System.Math.Max(w, (int)System.Math.Ceiling(sf.MeasureString(line).X));
        int h = (int)System.Math.Ceiling(lines.Count * sf.LineHeight);
        return new MirEngine.Size(w, h);
    }

    // ---- DrawText ----
    public static void DrawText(Texture dc, string text, Font font, MirEngine.Point pt, int foreColor)
        => DrawText(dc, text, font, new MirEngine.Rectangle(pt.X, pt.Y, int.MaxValue, int.MaxValue), foreColor, TextFormatFlags.Default);

    public static void DrawText(Texture dc, string text, Font font, MirEngine.Point pt, int foreColor, TextFormatFlags flags)
        => DrawText(dc, text, font, new MirEngine.Rectangle(pt.X, pt.Y, int.MaxValue, int.MaxValue), foreColor, flags);

    public static void DrawText(Texture dc, string text, Font font, MirEngine.Rectangle bounds, int foreColor)
        => DrawText(dc, text, font, bounds, foreColor, TextFormatFlags.Default);

    public static void DrawText(Texture dc, string text, Font font, MirEngine.Rectangle bounds, int foreColor, TextFormatFlags flags)
        => DrawTextCore(dc, text, font, bounds, foreColor, flags);

    private static void DrawTextCore(Texture dc, string text, Font font, MirEngine.Rectangle bounds, int foreColor, TextFormatFlags flags)
    {
        if (dc?.RenderTarget == null || string.IsNullOrEmpty(text)) return;
        var sf = GetSpriteFont(font);
        if (sf == null) return;

        var saved = Bind(dc);
        try
        {
            var batch = Client.MirGraphics.DXManager.Batch;
            batch.Begin(KFramework.MonoGame.SpriteSortMode.Deferred,
                        KFramework.MonoGame.BlendState.NonPremultiplied,
                        KFramework.MonoGame.SamplerState.PointClamp);
            DrawInto(batch, sf, text, bounds, foreColor, flags);
            batch.End();
        }
        finally
        {
            BindRestore(saved);
        }
    }

    private static void DrawInto(KFramework.MonoGame.SpriteBatch batch, KFramework.MonoGame.SpriteFont sf,
        string text, MirEngine.Rectangle bounds, int foreColor, TextFormatFlags flags)
    {
        bool hCenter = (flags & TextFormatFlags.HorizontalCenter) != 0;
        bool hRight = (flags & TextFormatFlags.Right) != 0;
        bool vCenter = (flags & TextFormatFlags.VerticalCenter) != 0;
        bool vBottom = (flags & TextFormatFlags.Bottom) != 0;
        bool wordBreak = (flags & TextFormatFlags.WordBreak) != 0;

        int maxWidth = wordBreak ? bounds.Width : 0;
        var lines = WrapLines(text, sf, maxWidth);
        float lineH = sf.LineHeight;
        float totalH = lineH * lines.Count;

        float startY = bounds.Y;
        if (vCenter) startY = bounds.Y + System.Math.Max(0f, (bounds.Height - totalH) / 2f);
        else if (vBottom) startY = bounds.Y + System.Math.Max(0f, bounds.Height - totalH);

        var fore = KFramework.MonoGame.Color.FromArgb((uint)foreColor);
        foreach (var line in lines)
        {
            float lineW = sf.MeasureString(line).X;
            float tx = bounds.X;
            if (hCenter) tx = bounds.X + System.Math.Max(0f, (bounds.Width - lineW) / 2f);
            else if (hRight) tx = bounds.X + System.Math.Max(0f, bounds.Width - lineW);

            sf.Draw(batch, line, new KFramework.MonoGame.Vector2(tx, startY), fore, 0f,
                    KFramework.MonoGame.Vector2.Zero, 1f, KFramework.MonoGame.SpriteEffects.None, 0f);
            startY += lineH;
        }
    }

    // 按显式 '\n' 切分；带 WordBreak 时在 maxWidth 内按词折行，连续中文/长串退化逐字折行（对齐原版 WordBreak 行为）。
    private static System.Collections.Generic.List<string> WrapLines(string text, KFramework.MonoGame.SpriteFont sf, int maxWidth)
    {
        var result = new System.Collections.Generic.List<string>();
        if (string.IsNullOrEmpty(text) || sf == null) return result;

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Replace("\r", "");
            if (maxWidth <= 0) { result.Add(line); continue; }

            var words = line.Split(' ');
            var current = new System.Text.StringBuilder();
            foreach (var word in words)
            {
                if (current.Length > 0 && sf.MeasureString(current.ToString() + " " + word).X > maxWidth && sf.MeasureString(word).X <= maxWidth)
                {
                    result.Add(current.ToString());
                    current.Clear();
                    current.Append(word);
                }
                else if (sf.MeasureString(word).X > maxWidth)
                {
                    foreach (var ch in word)
                    {
                        if (current.Length > 0 && sf.MeasureString(current.ToString() + ch).X > maxWidth)
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

    // 绑定控件离屏纹理为当前渲染目标（不清屏，由调用方负责清屏，对应 WinForm 控件 OnPaint 里 graphics.Clear）。
    internal static SlimDX.Direct3D9.Surface Bind(Texture texture)
    {
        var saved = Client.MirGraphics.DXManager.CurrentSurface;
        Client.MirGraphics.DXManager.SetSurface(new SlimDX.Direct3D9.Surface(texture));
        return saved;
    }

    internal static void BindRestore(SlimDX.Direct3D9.Surface saved)
    {
        Client.MirGraphics.DXManager.SetSurface(saved);
    }

    // 端口原语：控件在多次 DrawText 叠绘（如标签描边）前清一次背景。
    // 不属于原版 TextRenderer API——原版由控件自身负责清屏，此处仅桥接离屏纹理清屏。
    public static void Clear(Texture texture, int argb)
    {
        if (texture?.RenderTarget == null) return;
        var saved = Client.MirGraphics.DXManager.CurrentSurface;
        Client.MirGraphics.DXManager.SetSurface(new SlimDX.Direct3D9.Surface(texture));
        var clear = ((argb >> 24) & 0xFF) != 0
            ? KFramework.MonoGame.Color.FromArgb((uint)argb)
            : KFramework.MonoGame.Color.Transparent;
        Client.MirGraphics.DXManager.GDevice.Clear(clear);
        Client.MirGraphics.DXManager.SetSurface(saved);
    }
}
