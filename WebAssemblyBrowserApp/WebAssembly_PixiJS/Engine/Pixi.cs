#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;
using System.Numerics;

namespace WebAssemblyBrowserApp.Engine;

/// <summary>
/// PixiJS 渲染封装（整帧批提交架构，与 WebGPU.cs 同 API 面）。
///
/// 图元（矩形 / 圆角矩形 / 圆）在 C# 侧累积进 float[] 缓冲，
/// 帧末一次 [JSImport("pixi.submit")] 交给 PixiJS 渲染 —— 跨 WASM↔JS 边界 O(1)。
/// 线 / 文本频率低，走独立轻量 [JSImport]。
///
/// 与 WebGPU 版的关键差异（也是改进）：
///   1. 全局 Alpha 在 Push 时直接乘进每图元颜色 —— 粒子半透明、树木等真实生效；
///   2. 变换全部在 C# 侧完成（坐标预变换到世界空间），JS 侧无需状态机。
/// </summary>
public static partial class Pixi
{
    // 每个实例的 float 数（须与 pixi-bridge.js 的 SHAPE_STRIDE 一致）
    private const int Stride = 13;
    private const float TRect = 0, TRound = 1, TCircle = 2;

    // 帧内累积缓冲（double[] 以便 [JSImport] 直接 marshall 到 JS Float64Array）
    private static readonly List<double> _batch = new();
    private static readonly List<double> _shadowBatch = new();

    // 当前变换（CPU 侧矩阵，避免每次绘制都跨边界）
    private static float _m11 = 1, _m12 = 0, _m21 = 0, _m22 = 1, _m31 = 0, _m32 = 0;
    private static readonly Stack<(float, float, float, float, float, float)> _matrixStack = new();
    private static float _alpha = 1f;
    private static (float blur, float r, float g, float b, float a)? _shadow = null;

    // ---- 生命周期 / 帧 ----
    [JSImport("pixi.init", "main.js")] public static partial void Init();
    [JSImport("pixi.clear", "main.js")] internal static partial void ClearColorJS(string color);
    [JSImport("pixi.submit", "main.js")] internal static partial void Submit(double[] shapes, double[] shadows);
    [JSImport("pixi.drawLine", "main.js")]
    internal static partial void DrawLineJS(float x1, float y1, float x2, float y2, float width,
        float r, float g, float b, float a,
        bool hasShadow, float sr, float sg, float sb, float sa, float blur);

    // ---- 文本（频率低，单独提交） ----
    [JSImport("pixi.fillText", "main.js")] public static partial void FillText(string text, float x, float y, string font, string color, string align);
    [JSImport("pixi.measureText", "main.js")] public static partial float MeasureText(string text, string font);

    // -----------------------------------------------------------------
    //  帧管理
    // -----------------------------------------------------------------
    public static void Clear(string color)
    {
        ClearColorJS(color);
        _batch.Clear();
        _shadowBatch.Clear();
    }

    public static void Clear(Vector4 c)
    {
        ClearColorJS(ColorToCss(c));
        _batch.Clear();
        _shadowBatch.Clear();
    }

    /// <summary>帧末：把整帧图元一次性提交给 PixiJS（跨边界 O(1)）。</summary>
    public static void Flush()
    {
        Submit(_batch.ToArray(), _shadowBatch.ToArray());
        _batch.Clear();
        _shadowBatch.Clear();
    }

    // -----------------------------------------------------------------
    //  变换（CPU 侧维护，绘制时用当前矩阵变换坐标）
    // -----------------------------------------------------------------
    public static void Save() => _matrixStack.Push((_m11, _m12, _m21, _m22, _m31, _m32));

    public static void Restore()
    {
        if (_matrixStack.Count <= 0) return;
        var m = _matrixStack.Pop();
        _m11 = m.Item1; _m12 = m.Item2; _m21 = m.Item3; _m22 = m.Item4; _m31 = m.Item5; _m32 = m.Item6;
    }

    public static void Transform(Matrix3x2 m)
    {
        _m11 = (float)m.M11; _m12 = (float)m.M12; _m21 = (float)m.M21;
        _m22 = (float)m.M22; _m31 = (float)m.M31; _m32 = (float)m.M32;
    }

    public static void Alpha(float a) => _alpha = (float)a;

    public static void Translate(float dx, float dy)
    {
        _m31 += dx;
        _m32 += dy;
    }

    public static void Shadow(string color, float blur)
    {
        var (r, g, b, a) = ParseColor(color);
        _shadow = (blur, r, g, b, a);
    }

    public static void NoShadow() => _shadow = null;

    // -----------------------------------------------------------------
    //  图元（累积进缓冲，不跨边界）
    // -----------------------------------------------------------------
    private static (float, float) TransformPoint(float x, float y)
        => (_m11 * x + _m21 * y + _m31, _m12 * x + _m22 * y + _m32);

    public static void FillRect(float x, float y, float w, float h, string color)
        => Push(TRect, x, y, w, h, 0, color);

    public static void RoundedRect(float x, float y, float w, float h, float r, string color)
        => Push(TRound, x, y, w, h, r, color);

    public static void FillCircle(float cx, float cy, float r, string color)
        => Push(TCircle, cx - r, cy - r, r * 2, r * 2, r, color);

    public static void DrawLine(float x1, float y1, float x2, float y2, float width, string color)
    {
        var (tx1, ty1) = TransformPoint(x1, y1);
        var (tx2, ty2) = TransformPoint(x2, y2);
        var (r, g, b, a) = ParseColor(color);
        a *= _alpha;

        bool has = _shadow.HasValue;
        float sr = 0, sg = 0, sb = 0, sa = 0, blur = 0;
        if (has)
        {
            var s = _shadow.Value;
            sr = s.r; sg = s.g; sb = s.b; sa = s.a * _alpha; blur = s.blur;
        }
        DrawLineJS(tx1, ty1, tx2, ty2, width, r, g, b, a, has, sr, sg, sb, sa, blur);
    }

    /// <summary>描边矩形：由 4 条填充边组成（支持阴影与全局 Alpha）。</summary>
    public static void StrokeRect(float x, float y, float w, float h, float thickness, string color)
    {
        if (thickness <= 0) return;
        FillRect(x, y, w, thickness, color);
        FillRect(x, y + h - thickness, w, thickness, color);
        FillRect(x, y, thickness, h, color);
        FillRect(x + w - thickness, y, thickness, h, color);
    }

    // 兼容重载（Vector2 / 尺寸）
    public static void FillRect(Vector2 pos, Vector2 size, string color) => FillRect(pos.X, pos.Y, size.X, size.Y, color);
    public static void FillCircle(Vector2 c, float r, string color) => FillCircle(c.X, c.Y, r, color);
    public static void FillText(string text, Vector2 pos, string font, string color, string align = "center")
        => FillText(text, pos.X, pos.Y, font, color, align);

    // -----------------------------------------------------------------
    //  内部：压入一个实例
    // -----------------------------------------------------------------
    private static void Push(float type, float x, float y, float w, float h, float radius, string color)
    {
        var (px, py) = TransformPoint(x, y);
        var (cx, cy) = TransformPoint(x + w, y + h);
        float wx = MathF.Min(px, cx), wy = MathF.Min(py, cy);
        float ww = MathF.Abs(cx - px), wh = MathF.Abs(cy - py);

        var (r, g, b, a) = ParseColor(color);
        a *= _alpha;
        _batch.Add(wx); _batch.Add(wy); _batch.Add(ww); _batch.Add(wh);
        _batch.Add(ww / 2f); _batch.Add(wh / 2f);
        _batch.Add((float)radius); _batch.Add(type);
        _batch.Add(r); _batch.Add(g); _batch.Add(b); _batch.Add(a);
        _batch.Add(0); // lineW 占位（本层线走独立通道）

        if (_shadow is { } s)
        {
            var (sx, sy) = TransformPoint(x + s.blur * 0f, y + s.blur * 0f); // 偏移为 0，与 WebGPU 一致
            var (scx, scy) = TransformPoint(x + w, y + h);
            float swx = MathF.Min(sx, scx), swy = MathF.Min(sy, scy);
            float sww = MathF.Abs(scx - sx), swh = MathF.Abs(scy - sy);
            _shadowBatch.Add(swx); _shadowBatch.Add(swy); _shadowBatch.Add(sww); _shadowBatch.Add(swh);
            _shadowBatch.Add(sww / 2f); _shadowBatch.Add(swh / 2f);
            _shadowBatch.Add((float)radius); _shadowBatch.Add(type);
            _shadowBatch.Add(s.r); _shadowBatch.Add(s.g); _shadowBatch.Add(s.b); _shadowBatch.Add(s.a * _alpha);
            _shadowBatch.Add(0);
        }
    }

    // -----------------------------------------------------------------
    //  颜色解析（C# 侧，避免把字符串丢给 JS 解析）
    // -----------------------------------------------------------------
    private static (float, float, float, float) ParseColor(string c)
    {
        if (string.IsNullOrEmpty(c)) return (1, 1, 1, 1);
        c = c.Trim();
        if (c[0] == '#')
        {
            var hex = c.Length == 4
                ? new string(new[] { c[1], c[1], c[2], c[2], c[3], c[3] })
                : c.Substring(1);
            if (hex.Length == 8)
                return (Hex(hex, 0), Hex(hex, 2), Hex(hex, 4), Hex(hex, 6));
            return (Hex(hex, 0), Hex(hex, 2), Hex(hex, 4), 1f);
        }
        if (c.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            var inner = c.Substring(c.IndexOf('(') + 1).TrimEnd(')');
            var p = inner.Split(',');
            float r = float.Parse(p[0]) / 255f;
            float g = float.Parse(p[1]) / 255f;
            float b = float.Parse(p[2]) / 255f;
            float a = p.Length > 3 ? float.Parse(p[3]) : 1f;
            return (r, g, b, a);
        }
        return (1, 1, 1, 1);
    }

    private static float Hex(string h, int i) => Convert.ToByte(h.Substring(i, 2), 16) / 255f;

    private static string ColorToCss(Vector4 c)
    {
        int r = (int)Math.Clamp(c.X * 255f, 0, 255);
        int g = (int)Math.Clamp(c.Y * 255f, 0, 255);
        int b = (int)Math.Clamp(c.Z * 255f, 0, 255);
        int a = (int)Math.Clamp(c.W * 255f, 0, 255);
        return $"rgba({r},{g},{b},{a / 255f})";
    }
}
