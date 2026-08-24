using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;
using System.Numerics;

namespace WebAssemblyBrowserApp.Engine;

/// <summary>
/// WebGPU 渲染封装（整帧批提交架构）。
///
/// 关键优化：所有图元（矩形 / 圆角矩形 / 圆 / 线）在 C# 侧累积进 float[] 缓冲，
/// 帧末只调用一次 [JSImport("gpu.submit")] 把整批丢给 JS/GPU —— 跨 WASM↔JS 边界
/// 从「每图元一次」降到「每帧一两次」，桥厚度对齐 Bevy/wasm-bindgen 思路。
///
/// 变换 / 状态 / 阴影开关 / 文本 / 图片 因频率低，仍用轻量 [JSImport]（每帧少数几次）。
///
/// 注意：WebGPU 设备初始化是异步的 —— Init() 只负责在 JS 侧触发异步流程，
/// 设备真正就绪后由 JS 回调 C# 的 EngineReady()（见 Program.cs）。
/// </summary>
public static partial class WebGPU
{
    // 每个实例的 float 数（须与 main.js 的 SHAPE_STRIDE 一致）
    private const int Stride = 13;
    // type 常量
    private const float TRect = 0, TRound = 1, TCircle = 2, TLine = 3;

    // 帧内累积缓冲（用 double[] 以便 [JSImport] 直接 marshall 到 JS Float64Array）
    private static readonly List<double> _batch = new();
    private static readonly List<double> _shadowBatch = new();

    // 当前变换（CPU 侧矩阵，避免每次绘制都跨边界）
    private static float _m11 = 1, _m12 = 0, _m21 = 0, _m22 = 1, _m31 = 0, _m32 = 0;
    private static readonly Stack<(float, float, float, float, float, float)> _matrixStack = new();
    private static float _alpha = 1f;
    private static (float ox, float oy, float blur, float r, float g, float b, float a)? _shadow = null;

    // ---- 生命周期 ----
    [JSImport("gpu.init", "main.js")] public static partial void Init();
    [JSImport("gpu.resize", "main.js")] public static partial void Resize(int w, int h);
    [JSImport("gpu.beginFrame", "main.js")] public static partial void BeginFrame(double clearR, double clearG, double clearB, double clearA);
    [JSImport("gpu.clear", "main.js")] public static partial void ClearColor(string color);
    [JSImport("gpu.submit", "main.js")] public static partial void Submit(double[] shapes, double[] shadows, double alpha);

    // ---- 状态 / 变换（轻量，每帧少数几次） ----
    [JSImport("gpu.setTransform", "main.js")] public static partial void SetTransform(double m11, double m12, double m21, double m22, double dx, double dy);
    [JSImport("gpu.resetTransform", "main.js")] public static partial void ResetTransform();
    [JSImport("gpu.saveTransform", "main.js")] public static partial void SaveTransform();
    [JSImport("gpu.restoreTransform", "main.js")] public static partial void RestoreTransform();
    [JSImport("gpu.translate", "main.js")] public static partial void Translate(double x, double y);
    [JSImport("gpu.setAlpha", "main.js")] public static partial void SetAlpha(double a);

    // ---- 阴影开关（轻量） ----
    [JSImport("gpu.shadowColor", "main.js")] public static partial void ShadowColor(string color, double blur);
    [JSImport("gpu.noShadow", "main.js")] public static partial void NoShadowJS();

    // ---- 文本 / 图片（真纹理，频率低，单独提交） ----
    [JSImport("gpu.fillText", "main.js")] public static partial void FillText(string text, double x, double y, string font, string color, string align);
    [JSImport("gpu.loadImage", "main.js")] public static partial int LoadImage(string src);
    [JSImport("gpu.drawImage", "main.js")] public static partial void DrawImage(int id, double x, double y, double w, double h);
    [JSImport("gpu.measureText", "main.js")] public static partial double MeasureText(string text, string font);

    // -----------------------------------------------------------------
    //  帧管理
    // -----------------------------------------------------------------
    public static void Clear(string color)
    {
        ClearColor(color);
        _batch.Clear();
        _shadowBatch.Clear();
    }
    public static void Clear(Vector4 c)
    {
        BeginFrame(c.X, c.Y, c.Z, c.W);
        _batch.Clear();
        _shadowBatch.Clear();
    }

    /// <summary>帧末：把整帧图元一次性提交给 GPU（跨边界 O(1)）。</summary>
    public static void Flush()
    {
        Submit(_batch.ToArray(), _shadowBatch.ToArray(), _alpha);
        _batch.Clear();
        _shadowBatch.Clear();
    }

    // -----------------------------------------------------------------
    //  变换（CPU 侧维护，绘制时用当前矩阵变换坐标）
    // -----------------------------------------------------------------
    public static void Save() { _matrixStack.Push((_m11, _m12, _m21, _m22, _m31, _m32)); SaveTransform(); }
    public static void Restore() { if (_matrixStack.Count > 0) _matrixStack.Pop(); RestoreTransform(); }
    public static void Transform(Matrix3x2 m)
    {
        _m11 = (float)m.M11; _m12 = (float)m.M12; _m21 = (float)m.M21; _m22 = (float)m.M22; _m31 = (float)m.M31; _m32 = (float)m.M32;
        SetTransform(m.M11, m.M12, m.M21, m.M22, m.M31, m.M32);
    }
    public static void Alpha(double a) { _alpha = (float)a; SetAlpha(a); }

    public static void Shadow(string color, double blur)
    {
        _shadow = ParseShadow(color, blur);
        ShadowColor(color, blur);
    }
    public static void NoShadow() { _shadow = null; NoShadowJS(); }

    // -----------------------------------------------------------------
    //  图元（累积进缓冲，不跨边界）
    // -----------------------------------------------------------------
    private static (float, float) TransformPoint(double x, double y)
    {
        return (
            (float)(_m11 * x + _m21 * y + _m31),
            (float)(_m12 * x + _m22 * y + _m32)
        );
    }

    public static void FillRect(double x, double y, double w, double h, string color)
        => Push(TRect, x, y, w, h, 0, color, 0);
    public static void RoundedRect(double x, double y, double w, double h, double r, string color)
        => Push(TRound, x, y, w, h, r, color, 0);
    public static void FillCircle(double cx, double cy, double r, string color)
        => Push(TCircle, cx - r, cy - r, r * 2, r * 2, r, color, 0);
    public static void DrawLine(double x1, double y1, double x2, double y2, double width, string color)
    {
        var (tx, ty) = TransformPoint(x1, y1);
        var (bx, by) = TransformPoint(x2, y2);
        double minx = Math.Min(tx, bx), miny = Math.Min(ty, by);
        double w = Math.Abs(bx - tx) + width, h = Math.Abs(by - ty) + width;
        Push(TLine, minx, miny, w, h, 0, color, width);
    }
    public static void StrokeRect(double x, double y, double w, double h, double width, string color)
        => Push(TRect, x, y, w, h, 0, color, width);

    public static void FillRect(Vector2 pos, Vector2 size, string color) => FillRect(pos.X, pos.Y, size.X, size.Y, color);
    public static void FillCircle(Vector2 c, double r, string color) => FillCircle(c.X, c.Y, r, color);
    public static void FillText(string text, Vector2 pos, string font, string color, string align = "center")
        => FillText(text, pos.X, pos.Y, font, color, align);

    // -----------------------------------------------------------------
    //  内部：压入一个实例
    // -----------------------------------------------------------------
    private static void Push(float type, double x, double y, double w, double h, double radius, string color, double lineW)
    {
        var (px, py) = TransformPoint(x, y);
        var (cx, cy) = TransformPoint(x + w, y + h);
        // 用变换后的对角点算出世界坐标包围盒（处理平移/缩放；旋转场景用 Save/Transform 整体处理）
        double wx = Math.Min(px, cx), wy = Math.Min(py, cy);
        double ww = Math.Abs(cx - px), wh = Math.Abs(cy - py);

        var (r, g, b, a) = ParseColor(color);
        _batch.Add(wx); _batch.Add(wy); _batch.Add(ww); _batch.Add(wh);
        _batch.Add(ww / 2f); _batch.Add(wh / 2f);
        _batch.Add((float)radius); _batch.Add(type);
        _batch.Add(r); _batch.Add(g); _batch.Add(b); _batch.Add(a);
        _batch.Add((float)lineW);

        if (_shadow.HasValue)
        {
            var s = _shadow.Value;
            var (sx, sy) = TransformPoint(x + s.ox, y + s.oy);
            var (scx, scy) = TransformPoint(x + w + s.ox, y + h + s.oy);
            double swx = Math.Min(sx, scx), swy = Math.Min(sy, scy);
            double sww = Math.Abs(scx - sx), swh = Math.Abs(scy - sy);
            _shadowBatch.Add(swx); _shadowBatch.Add(swy); _shadowBatch.Add(sww); _shadowBatch.Add(swh);
            _shadowBatch.Add(sww / 2f); _shadowBatch.Add(swh / 2f);
            _shadowBatch.Add((float)radius); _shadowBatch.Add(type);
            _shadowBatch.Add(s.r); _shadowBatch.Add(s.g); _shadowBatch.Add(s.b); _shadowBatch.Add(s.a);
            _shadowBatch.Add((float)lineW);
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
                return (
                    Hex(hex, 0), Hex(hex, 2), Hex(hex, 4), Hex(hex, 6)
                );
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

    private static (float ox, float oy, float blur, float r, float g, float b, float a) ParseShadow(string color, double blur)
    {
        var (r, g, b, a) = ParseColor(color);
        return (0, 0, (float)blur, r, g, b, a);
    }
}
