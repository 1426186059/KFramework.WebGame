using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using SkiaSharp;

namespace WebAssemblyBrowserApp.Engine;

/// <summary>
/// SkiaSharp 2D 渲染上下文。
///
/// 渲染管线（全在 .NET 侧完成，浏览器只负责把像素贴到 canvas）：
///   C# 用 SKBitmap + SKCanvas 软件光栅化 → 每帧结束 Marshal.Copy 出 RGBA 像素
///   → 一次 JSImport 交给 main.js → putImageData 呈现。
///
/// 绘制 API 与 Canvas2D 版引擎（WebAssembly_Canvas2D/Engine/Canvas2D.cs）逐一对齐，
/// 便于两个后端之间平移游戏逻辑。
/// </summary>
public static partial class SkiaCanvas
{
    public const int LogicalWidth = 800;
    public const int LogicalHeight = 600;

    private static SKBitmap _bitmap = null!;
    private static SKCanvas _canvas = null!;
    private static byte[] _pixels = Array.Empty<byte>();

    /// <summary>固定 _pixels 的句柄，与位图同生命周期，保证光栅化直接写入托管数组。</summary>
    private static GCHandle _pixelsHandle;

    private static readonly SKPaint FillPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private static readonly SKPaint StrokePaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private static readonly SKPaint TextPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    private static readonly Dictionary<int, SKMaskFilter> BlurCache = new();
    private static readonly Stack<State> StateStack = new();

    private static State _state = new(1f, new SKColor(0, 0, 0, 0), 0f);
    private static SKTypeface? _typeface;

    private readonly record struct State(float Alpha, SKColor ShadowColor, float ShadowBlur);

    // ------------------------- 生命周期 -------------------------

    /// <summary>创建离屏位图并通知 JS 准备同尺寸的 canvas。</summary>
    public static void Init(string selector, int width, int height)
    {
        JsInit(selector, width, height);

        // 关键优化：把 _pixels 固定后直接作为 SKBitmap 的像素存储，
        // 这样软件光栅化的结果直接落在托管数组里，Flush 时无需再做一次 Marshal.Copy。
        _pixels = new byte[width * height * 4];
        _pixelsHandle = GCHandle.Alloc(_pixels, GCHandleType.Pinned);
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        _bitmap = new SKBitmap();
        if (!_bitmap.InstallPixels(info, _pixelsHandle.AddrOfPinnedObject(), info.RowBytes))
            throw new InvalidOperationException("无法为 SKBitmap 安装托管像素缓冲区");
        _canvas = new SKCanvas(_bitmap);

        ResolveFonts();
    }

    /// <summary>把本帧像素提交给浏览器 canvas。</summary>
    public static void Flush()
    {
        if (_canvas is null) return;
        _canvas.Flush();
        Present(_pixels);
    }

    public static SKCanvas Canvas => _canvas;

    // ------------------------- 图层缓存 -------------------------

    /// <summary>
    /// 把一段静态绘制结果缓存为离屏位图。
    /// 全屏渐变这类着色器填充在软件光栅化下每帧要逐像素求值（800×600 约 48 万像素，实测约 20ms），
    /// 而背景通常完全静态 —— 缓存后每帧只需一次位图拷贝。
    /// </summary>
    public static SKBitmap CacheLayer(int width, int height, Action draw)
    {
        var layer = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        var saved = _canvas;
        _canvas = new SKCanvas(layer);
        try { draw(); }
        finally { _canvas.Dispose(); _canvas = saved; }
        return layer;
    }

    /// <summary>把缓存图层贴回当前画布（快速位图拷贝）。</summary>
    public static void DrawLayer(SKBitmap layer, float x, float y) => _canvas.DrawBitmap(layer, x, y);

    [JSImport("engine.initCanvas", "main.js")]
    private static partial void JsInit(string selector, int width, int height);

    [JSImport("skia.present", "main.js")]
    private static partial void Present(byte[] pixels);

    // ------------------------- 字体 -------------------------

    /// <summary>
    /// 探测运行环境是否有可用字体：有则用系统字体绘制（更平滑），
    /// 没有（WASM 常见情况）则回退到内置点阵字体。
    /// </summary>
    private static void ResolveFonts()
    {
        try
        {
            var fm = SKFontManager.Default;
            if (fm is not null && fm.FontFamilyCount > 0)
            {
                foreach (var family in fm.GetFontFamilies())
                {
                    var tf = SKTypeface.FromFamilyName(family);
                    if (tf is null) continue;
                    using var probe = new SKPaint { Typeface = tf, TextSize = 32, IsAntialias = true };
                    if (probe.MeasureText("MWi") > 4f)
                    {
                        _typeface = tf;
                        Console.WriteLine($"[Skia] 使用系统字体：{family}");
                        return;
                    }
                    tf.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Skia] 字体探测失败，使用内置点阵字体：{ex.Message}");
        }

        _typeface = null;
        Console.WriteLine("[Skia] 未发现系统字体，使用内置 5x7 点阵字体");
    }

    /// <summary>当前是否使用系统字体绘制文本。</summary>
    public static bool HasSystemTypeface => _typeface is not null;

    // ------------------------- 状态 -------------------------

    public static void Save()
    {
        StateStack.Push(_state);
        _canvas.Save();
    }

    public static void Restore()
    {
        _canvas.Restore();
        if (StateStack.Count > 0) _state = StateStack.Pop();
    }

    public static void Translate(float x, float y) => _canvas.Translate(x, y);

    public static void Rotate(float radians) => _canvas.RotateRadians(radians);

    public static void Scale(float sx, float sy) => _canvas.Scale(sx, sy);

    public static void Alpha(float a) => _state = _state with { Alpha = Math.Clamp(a, 0f, 1f) };

    /// <summary>开启发光/阴影：后续绘制会在图形外圈叠加一层模糊。</summary>
    public static void Shadow(string color, float blur)
        => _state = _state with { ShadowColor = ToColor(color), ShadowBlur = MathF.Max(0f, blur) };

    /// <summary>关闭发光/阴影。</summary>
    public static void NoShadow() => _state = _state with { ShadowBlur = 0f };

    // ------------------------- 绘制原语 -------------------------

    public static void Clear(string color) => _canvas.Clear(ToColor(color));

    public static void FillRect(float x, float y, float w, float h, string color)
        => FillShape(new SKRect(x, y, x + w, y + h), color, 0f);

    public static void RoundedRect(float x, float y, float w, float h, float r, string color)
        => FillShape(new SKRect(x, y, x + w, y + h), color, r);

    public static void StrokeRect(float x, float y, float w, float h, string color, float lineWidth)
    {
        var rect = new SKRect(x, y, x + w, y + h);
        Configure(StrokePaint, ToColor(color), 1f);
        StrokePaint.StrokeWidth = lineWidth;
        StrokePaint.MaskFilter = null;
        DrawShadowed(rect, 0f, StrokePaint);
        _canvas.DrawRoundRect(rect, 0, 0, StrokePaint);
    }

    public static void FillCircle(float x, float y, float r, string color)
        => FillShape(new SKRect(x - r, y - r, x + r, y + r), color, r);

    public static void Line(float x1, float y1, float x2, float y2, string color, float lineWidth)
    {
        Configure(StrokePaint, ToColor(color), 1f);
        StrokePaint.StrokeWidth = lineWidth;
        StrokePaint.MaskFilter = null;
        _canvas.DrawLine(x1, y1, x2, y2, StrokePaint);
    }

    /// <summary>
    /// 文本绘制。font 沿用 CSS 写法（如 "bold 46px system-ui"），仅解析字号与粗体；
    /// 对齐方式 align = left / center / right，y 表示文本垂直中心（与 Canvas2D 一致）。
    /// </summary>
    public static void FillText(string text, float x, float y, string font, string color, string align = "center")
        => DrawText(text, x, y, font, ToColor(color), align);

    /// <summary>测量文本宽度（逻辑像素），用于自建布局。</summary>
    public static float MeasureText(string text, string font)
    {
        var (size, _) = ParseFont(font);
        if (_typeface is null) return BitmapFont.Measure(text, size);
        using var paint = new SKPaint { Typeface = _typeface, TextSize = size, IsAntialias = true };
        return paint.MeasureText(text);
    }

    // ------------------------- Skia 专属增强 -------------------------

    /// <summary>垂直线性渐变圆角矩形（Skia 着色器，Canvas2D 版本无此能力）。</summary>
    public static void GradientRoundRect(float x, float y, float w, float h, float r, string top, string bottom)
    {
        var rect = new SKRect(x, y, x + w, y + h);
        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(x, y), new SKPoint(x, y + h),
            new[] { ToColor(top), ToColor(bottom) },
            null, SKShaderTileMode.Clamp);
        Configure(FillPaint, SKColors.White, 1f);
        FillPaint.MaskFilter = null;
        FillPaint.Shader = shader;
        _canvas.DrawRoundRect(rect, r, r, FillPaint);
        FillPaint.Shader = null;
    }

    /// <summary>径向光晕（弹球、子弹、爆炸等的辉光）。</summary>
    public static void Glow(float x, float y, float r, string color)
    {
        var c = ToColor(color);
        using var shader = SKShader.CreateRadialGradient(
            new SKPoint(x, y), r,
            new[] { c.WithAlpha((byte)(c.Alpha * _state.Alpha)), new SKColor(c.Red, c.Green, c.Blue, 0) },
            null, SKShaderTileMode.Clamp);
        Configure(FillPaint, SKColors.White, 1f);
        FillPaint.MaskFilter = null;
        FillPaint.Shader = shader;
        _canvas.DrawCircle(x, y, r, FillPaint);
        FillPaint.Shader = null;
    }

    // ------------------------- 内部实现 -------------------------

    private static void FillShape(SKRect rect, string color, float radius)
    {
        var c = ToColor(color);
        Configure(FillPaint, c, 1f);
        DrawShadowed(rect, radius, FillPaint);
        _canvas.DrawRoundRect(rect, radius, radius, FillPaint);
    }

    /// <summary>若开启了 Shadow，先画一层外发光。</summary>
    private static void DrawShadowed(SKRect rect, float radius, SKPaint paint)
    {
        if (_state.ShadowBlur <= 0f) { paint.MaskFilter = null; return; }

        var saved = paint.Color;
        var glow = _state.ShadowColor;
        paint.Color = new SKColor(glow.Red, glow.Green, glow.Blue, (byte)(glow.Alpha * _state.Alpha * 0.55f));
        paint.MaskFilter = GetBlur(_state.ShadowBlur * 0.5f);
        _canvas.DrawRoundRect(rect, radius, radius, paint);
        paint.MaskFilter = null;
        paint.Color = saved;
    }

    private static void Configure(SKPaint paint, SKColor color, float extra)
    {
        paint.Color = new SKColor(color.Red, color.Green, color.Blue,
            (byte)(color.Alpha * Math.Clamp(_state.Alpha * extra, 0f, 1f)));
    }

    private static SKMaskFilter GetBlur(float sigma)
    {
        int key = (int)MathF.Round(sigma * 4);
        if (BlurCache.TryGetValue(key, out var filter)) return filter;
        filter = SKMaskFilter.CreateBlur(SKBlurStyle.Outer, MathF.Max(0.5f, key / 4f));
        BlurCache[key] = filter;
        return filter;
    }

    private static void DrawText(string text, float x, float y, string font, SKColor color, string align)
    {
        if (string.IsNullOrEmpty(text)) return;
        var (size, bold) = ParseFont(font);

        Configure(TextPaint, color, 1f);
        TextPaint.MaskFilter = null;

        if (_typeface is null)
        {
            // 内置点阵字体：文本自带绘制，外发光用一次偏移重绘模拟
            if (_state.ShadowBlur > 0f)
            {
                var glow = _state.ShadowColor;
                TextPaint.Color = new SKColor(glow.Red, glow.Green, glow.Blue, (byte)(glow.Alpha * _state.Alpha * 0.5f));
                float off = MathF.Max(1f, _state.ShadowBlur * 0.18f);
                BitmapFont.Draw(_canvas, TextPaint, text, x + off, y + off, size, align, bold);
                Configure(TextPaint, color, 1f);
            }
            BitmapFont.Draw(_canvas, TextPaint, text, x, y, size, align, bold);
            return;
        }

        TextPaint.Typeface = _typeface;
        TextPaint.TextSize = size;
        TextPaint.FakeBoldText = bold;

        float w = TextPaint.MeasureText(text);
        float left = align switch
        {
            "center" => x - w / 2,
            "right" => x - w,
            _ => x,
        };
        TextPaint.GetFontMetrics(out var metrics);
        float ascent = -metrics.Ascent;
        float descent = metrics.Descent;
        float baseline = y + (ascent - descent) / 2f;

        if (_state.ShadowBlur > 0f)
        {
            var glow = _state.ShadowColor;
            var saved = TextPaint.Color;
            TextPaint.Color = new SKColor(glow.Red, glow.Green, glow.Blue, (byte)(glow.Alpha * _state.Alpha * 0.7f));
            TextPaint.MaskFilter = GetBlur(_state.ShadowBlur * 0.5f);
            _canvas.DrawText(text, left, baseline, TextPaint);
            TextPaint.MaskFilter = null;
            TextPaint.Color = saved;
        }
        _canvas.DrawText(text, left, baseline, TextPaint);
    }

    private static (float Size, bool Bold) ParseFont(string font)
    {
        float size = 16f;
        bool bold = false;
        if (!string.IsNullOrEmpty(font))
        {
            bold = font.Contains("bold", StringComparison.OrdinalIgnoreCase);
            int px = font.IndexOf("px", StringComparison.OrdinalIgnoreCase);
            if (px > 0)
            {
                int start = px - 1;
                while (start > 0 && (char.IsDigit(font[start - 1]) || font[start - 1] == '.')) start--;
                if (float.TryParse(font.AsSpan(start, px - start), out float parsed) && parsed > 0)
                    size = parsed;
            }
        }
        return (size, bold);
    }

    /// <summary>
    /// 解析 Web 风格颜色：#rgb / #rgba / #rrggbb / #rrggbbaa（不带 # 亦可）。
    /// 与 Canvas2D 版游戏代码里使用的色值格式保持一致。
    /// </summary>
    public static SKColor ToColor(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return SKColors.White;
        string h = hex.Trim().TrimStart('#');

        static byte Pair(char a) => (byte)Math.Clamp(Convert.ToInt32(char.ToString(a), 16) * 17, 0, 255);
        static byte Byte(string s, int i) => Convert.ToByte(s.Substring(i, 2), 16);

        try
        {
            switch (h.Length)
            {
                case 3:
                    return new SKColor(Pair(h[0]), Pair(h[1]), Pair(h[2]), (byte)255);
                case 4:
                    return new SKColor(Pair(h[0]), Pair(h[1]), Pair(h[2]), Pair(h[3]));
                case 6:
                    return new SKColor(Byte(h, 0), Byte(h, 2), Byte(h, 4), (byte)255);
                case 8:
                    return new SKColor(Byte(h, 0), Byte(h, 2), Byte(h, 4), Byte(h, 6));
            }
        }
        catch
        {
            // 落到默认白色
        }
        return SKColors.White;
    }
}
