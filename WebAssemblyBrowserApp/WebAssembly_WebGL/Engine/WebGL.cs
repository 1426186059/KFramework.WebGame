#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

namespace WebAssemblyBrowserApp.Engine;

/// <summary>
/// 纯 WebGL 渲染层（重构后：C# 侧管理合批 + 全部状态）。
///
/// 架构分层：
/// - JS 层（薄 API）：只暴露 gl.init / gl.clear / gl.drawShapeBatch /
///   gl.drawImageInstance / gl.loadImage / gl.bakeTextTexture
///   （见 wwwroot/jsengine/render/renderer.js & shapes.js）
/// - C# 层（本类）：管理
///     1. 矩阵栈、透明度栈、阴影栈（Save/Restore/Translate/Rotate/Alpha/Shadow）
///     2. 实例缓冲组装：PushShape 写入 _instData（每实例 20 floats，紧凑布局）
///     3. 合批刷新：FlushShapes → JS gl.drawShapeBatch（一次 drawArraysInstanced）
///     4. strokeRect/line 几何展开
///     5. 颜色 hex→rgba float 转换
///     6. 文本纹理烘焙后的绘制、图片绘制
///
/// 对上层游戏逻辑（MainMenuScene/BreakoutScene/TankScene）保持完全相同的 API，
/// 因此 Render() 实现无需任何改动。
/// </summary>
public static partial class WebGL
{
    public const int LogicalWidth = 800;
    public const int LogicalHeight = 600;

    // ---- 与 JS 侧 renderer.js 常量保持一致 ----
    private const int FLOATS_PER_INST = 20;
    private const int MAX_INSTANCES = 4096;

    // =====================================================================
    // 1. JSImport：薄 JS 层桥接
    // =====================================================================

    [JSImport("gl.init", "main.js")]
    private static partial void JsInit(string selector, int width, int height);

    /// <summary>清屏（C# 侧传 rgba float，0..1）。</summary>
    [JSImport("gl.clear", "main.js")]
    private static partial void JsClear(double r, double g, double b, double a);

    /// <summary>批量绘制形状（C# 侧已组装好实例数据）。
    /// data: double[]，长度 = instanceCount * FLOATS_PER_INST。</summary>
    [JSImport("gl.drawShapeBatch", "main.js")]
    private static partial void JsDrawShapeBatch(double[] data, int instanceCount);

    /// <summary>绘制单实例纹理（图片/文本）。
    /// data: double[FLOATS_PER_INST]。</summary>
    [JSImport("gl.drawImageInstance", "main.js")]
    private static partial void JsDrawImageInstance(double[] data, int texId, double uvW, double uvH);

    [JSImport("gl.loadImage", "main.js")]
    public static partial Task<bool> LoadImage(string id, string url);

    /// <summary>
    /// 烘焙文本纹理。返回对象（JSObject）包含 texId/tw/th/ascent/pad。
    /// 失败返回 null。
    /// </summary>
    [JSImport("gl.bakeTextTexture", "main.js")]
    private static partial JSObject? JsBakeTextTexture(string text, string font, string color);

    // =====================================================================
    // 2. 状态（矩阵栈 / 透明度 / 阴影）
    // =====================================================================

    // 行主序 3x3 矩阵：index = row*3 + col
    [ThreadStatic] private static List<float[]>? _matrixStack;
    [ThreadStatic] private static List<float>? _alphaStack;
    [ThreadStatic] private static List<(string? Color, float Blur)>? _shadowStack;

    private static float _globalAlpha = 1f;
    private static string? _shadowColor = null;
    private static float _shadowBlur = 0f;

    // =====================================================================
    // 3. 实例缓冲（合批）
    // =====================================================================

    // 每实例 20 floats，紧凑布局：
    //   0-3   rect (x, y, w, h)
    //   4-7   color (r, g, b, a)
    //   8     radius
    //   9     kind（0=圆角矩形, 1=圆）
    //   10    reserved
    //   11-19 实例矩阵 mat3（列主序 9 floats，attribute mat3 的 GL 布局）
    private static readonly double[] _instData = new double[MAX_INSTANCES * FLOATS_PER_INST];
    private static int _instCount = 0;

    // =====================================================================
    // 4. 矩阵工具
    // =====================================================================

    private static float[] Identity() => new[] { 1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f };

    /// <summary>标准矩阵乘法 r = a·b（行主序），Canvas2D 右乘语义：M_new = M_old · M_transform。</summary>
    private static float[] Multiply(float[] a, float[] b)
    {
        var r = new float[9];
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                r[i * 3 + j] = a[i * 3 + 0] * b[0 * 3 + j] + a[i * 3 + 1] * b[1 * 3 + j] + a[i * 3 + 2] * b[2 * 3 + j];
        return r;
    }

    private static float[] CurrentMatrix()
    {
        _matrixStack ??= new List<float[]> { Identity() };
        return _matrixStack[_matrixStack.Count - 1];
    }

    /// <summary>把行主序 3x3 矩阵按「列主序」写入实例数据的 [base+11..base+19]（GLSL attribute mat3 布局）。</summary>
    private static void StoreMatrixColMajor(double[] arr, int baseIdx, float[] m)
    {
        // 列主序：按列写 —— mat3 的第一列是 (m[0], m[3], m[6])，第二列 (m[1], m[4], m[7])，第三列 (m[2], m[5], m[8])
        arr[baseIdx + 0] = m[0]; arr[baseIdx + 1] = m[3]; arr[baseIdx + 2] = m[6];
        arr[baseIdx + 3] = m[1]; arr[baseIdx + 4] = m[4]; arr[baseIdx + 5] = m[7];
        arr[baseIdx + 6] = m[2]; arr[baseIdx + 7] = m[5]; arr[baseIdx + 8] = m[8];
    }

    // =====================================================================
    // 5. 颜色转换（hex string → rgba float[4] 0..1）
    // =====================================================================

    private static double[] HexToRgba(string hex, double alphaMul)
    {
        double r = 1, g = 1, b = 1, a = 1;
        if (!string.IsNullOrEmpty(hex))
        {
            string h = hex.StartsWith('#') ? hex.Substring(1) : hex;
            if (h.Length == 3)
                h = $"{h[0]}{h[0]}{h[1]}{h[1]}{h[2]}{h[2]}";
            if (h.Length == 8)
            {
                a = Convert.ToByte(h.Substring(6, 2), 16) / 255.0;
                h = h.Substring(0, 6);
            }
            if (h.Length == 6)
            {
                uint n = Convert.ToUInt32(h, 16);
                r = ((n >> 16) & 255) / 255.0;
                g = ((n >> 8) & 255) / 255.0;
                b = (n & 255) / 255.0;
            }
        }
        return new[] { r, g, b, a * alphaMul };
    }

    // =====================================================================
    // 6. 合批核心：PushShape（写入 _instData，必要时 Flush）
    // =====================================================================

    /// <summary>
    /// 推入一个形状实例 + 如果有阴影则先推入它的阴影实例。
    /// 阴影语义（Canvas2D 等价）：偏移 (+2, +3) 像素在局部坐标系先应用再乘矩阵，
    /// 颜色 = shadowColor 并降低不透明度。
    /// 这样无论 Save/Translate/Rotate 怎么变换，阴影都跟本体保持一致的相对位置。
    /// </summary>
    private static void PushShape(double x, double y, double w, double h,
                                  string color, double radius, int kind)
    {
        var m = CurrentMatrix();
        var rgba = HexToRgba(color, _globalAlpha);

        // --- 阴影先推入（在本体下方） ---
        if (!string.IsNullOrEmpty(_shadowColor))
        {
            if (_instCount >= MAX_INSTANCES) FlushShapes();
            var sRgba = HexToRgba(_shadowColor, _globalAlpha * 0.35);
            WriteInstance(_instCount, x + 2, y + 3, w, h, sRgba[0], sRgba[1], sRgba[2], sRgba[3], radius, kind, m);
            _instCount++;
        }

        // --- 本体 ---
        if (_instCount >= MAX_INSTANCES) FlushShapes();
        WriteInstance(_instCount, x, y, w, h, rgba[0], rgba[1], rgba[2], rgba[3], radius, kind, m);
        _instCount++;
    }

    private static void WriteInstance(int idx,
                                      double x, double y, double w, double h,
                                      double cr, double cg, double cb, double ca,
                                      double radius, int kind,
                                      float[] matrix)
    {
        int o = idx * FLOATS_PER_INST;
        _instData[o + 0] = x; _instData[o + 1] = y; _instData[o + 2] = w; _instData[o + 3] = h;
        _instData[o + 4] = cr; _instData[o + 5] = cg; _instData[o + 6] = cb; _instData[o + 7] = ca;
        _instData[o + 8] = radius;
        _instData[o + 9] = kind;
        _instData[o + 10] = 0;
        StoreMatrixColMajor(_instData, o + 11, matrix);
    }

    /// <summary>
    /// 刷新当前累计的形状实例（如果有）：调用一次 gl.drawArraysInstanced。
    /// 外部不需要手动调用，以下情况会自动 flush：
    /// - Clear / FillText / DrawImage 前
    /// - 实例数达到 MAX_INSTANCES
    /// - 每帧 Render() 结束时（GameEngine.Tick 里调用 Reset/Flush 对）
    /// </summary>
    public static void FlushShapes()
    {
        if (_instCount <= 0) return;
        JsDrawShapeBatch(_instData, _instCount);
        _instCount = 0;
    }

    // =====================================================================
    // 7. 每帧 Reset/Flush 对：由 GameEngine.Tick 调用
    // =====================================================================

    /// <summary>
    /// 每帧渲染开始：重置矩阵/透明度/阴影栈。
    /// 由 GameEngine.Tick 在调用 Current.Render 前调用。
    /// </summary>
    public static void ResetFrameState()
    {
        _matrixStack = new List<float[]> { Identity() };
        _alphaStack = new List<float> { 1f };
        _shadowStack = new List<(string?, float)> { (null, 0f) };
        _globalAlpha = 1f;
        _shadowColor = null;
        _shadowBlur = 0f;
        _instCount = 0;
    }

    // =====================================================================
    // 8. 公开 API：与旧 API 完全一致
    // =====================================================================

    public static void Init(string selector, int width, int height)
    {
        JsInit(selector, width, height);
    }

    public static void Clear(string color)
    {
        FlushShapes();
        var rgba = HexToRgba(color, 1.0);
        JsClear(rgba[0], rgba[1], rgba[2], rgba[3]);
    }

    public static void FillRect(double x, double y, double w, double h, string color)
        => PushShape(x, y, w, h, color, 0, 0);

    public static void StrokeRect(double x, double y, double w, double h, string color, double lineWidth)
    {
        double t = lineWidth > 0 ? lineWidth : 1;
        // 四条边用 FillRect，各自独立实例化（合批依然在同一批次）
        FillRect(x, y, w, t, color);
        FillRect(x, y + h - t, w, t, color);
        FillRect(x, y, t, h, color);
        FillRect(x + w - t, y, t, h, color);
    }

    public static void RoundedRect(double x, double y, double w, double h, double r, string color)
        => PushShape(x, y, w, h, color, r, 0);

    public static void FillCircle(double x, double y, double r, string color)
        => PushShape(x - r, y - r, r * 2, r * 2, color, r, 1);

    public static void Line(double x1, double y1, double x2, double y2, string color, double lineWidth)
    {
        double t = lineWidth > 0 ? lineWidth : 1;
        double dx = x2 - x1, dy = y2 - y1;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 0.001) return;
        // 用临时变换把线段转成旋转细矩形
        Save();
        Translate(x1, y1);
        Rotate(Math.Atan2(dy, dx));
        FillRect(0, -t / 2, len, t, color);
        Restore();
    }

    public static void FillText(string text, double x, double y, string font, string color, string align)
    {
        // 文本使用 IMG program 单实例绘制（SHAPE program 不擅长字形 SDF），
        // 所以绘制前必须 Flush 当前 SHAPE 批次，避免着色器切换时实例丢失
        FlushShapes();

        var meta = JsBakeTextTexture(text, font, color);
        if (meta == null) return;
        int texId = meta.GetPropertyAsInt32("texId");
        double tw = meta.GetPropertyAsDouble("tw");
        double th = meta.GetPropertyAsDouble("th");
        double ascent = meta.GetPropertyAsDouble("ascent");
        double pad = meta.GetPropertyAsDouble("pad");

        double ox = x;
        if (align == "center") ox = x - tw / 2;
        else if (align == "right") ox = x - tw;
        // Canvas2D 语义：y = 基线(baseline)；quad 顶部 = y - ascent - pad
        double oy = y - ascent - pad;

        // 构造单实例数据（与形状相同的布局，rect/color/matrix）
        var m = CurrentMatrix();
        var data = new double[FLOATS_PER_INST];
        data[0] = ox; data[1] = oy; data[2] = tw; data[3] = th;
        data[4] = 1; data[5] = 1; data[6] = 1; data[7] = _globalAlpha;
        data[8] = 0; data[9] = 0; data[10] = 0;
        StoreMatrixColMajor(data, 11, m);

        double uvW = tw / 1024.0;   // 与 renderer.js _fontCanvas.width=1024 一致
        double uvH = th / 256.0;    // _fontCanvas.height=256
        JsDrawImageInstance(data, texId, uvW, uvH);
    }

    public static void Save()
    {
        _matrixStack ??= new List<float[]> { Identity() };
        _alphaStack ??= new List<float> { 1f };
        _shadowStack ??= new List<(string?, float)> { (null, 0f) };
        _matrixStack.Add((float[])CurrentMatrix().Clone());
        _alphaStack.Add(_globalAlpha);
        _shadowStack.Add((_shadowColor, _shadowBlur));
    }

    public static void Restore()
    {
        if (_matrixStack != null && _matrixStack.Count > 1)
        {
            _matrixStack.RemoveAt(_matrixStack.Count - 1);
            _globalAlpha = _alphaStack![_alphaStack.Count - 1];
            _alphaStack.RemoveAt(_alphaStack.Count - 1);
            var s = _shadowStack![_shadowStack.Count - 1];
            _shadowStack.RemoveAt(_shadowStack.Count - 1);
            _shadowColor = s.Color; _shadowBlur = s.Blur;
        }
    }

    public static void Translate(double x, double y)
    {
        _matrixStack ??= new List<float[]> { Identity() };
        var m = CurrentMatrix();
        var t = Identity();
        t[2] = (float)x; t[5] = (float)y;
        _matrixStack[_matrixStack.Count - 1] = Multiply(m, t);
    }

    public static void Rotate(double radians)
    {
        _matrixStack ??= new List<float[]> { Identity() };
        var m = CurrentMatrix();
        double c = Math.Cos(radians), s = Math.Sin(radians);
        var r = Identity();
        r[0] = (float)c; r[1] = (float)(-s);
        r[3] = (float)s; r[4] = (float)c;
        _matrixStack[_matrixStack.Count - 1] = Multiply(m, r);
    }

    public static void Alpha(double a) { _globalAlpha = (float)a; }

    public static void Shadow(string color, double blur)
    {
        _shadowColor = color;
        _shadowBlur = (float)blur;
    }

    public static void NoShadow()
    {
        _shadowColor = null;
        _shadowBlur = 0;
    }

    public static void DrawImage(string id, double dx, double dy, double dw, double dh)
    {
        // DrawImage 切换到 IMG program，先 flush SHAPE 批次
        FlushShapes();

        // float[] matrix → double[]（供 JSInterop）
        var m = CurrentMatrix();
        var md = new double[9];
        for (int i = 0; i < 9; i++) md[i] = m[i];
        JsDrawImageById(id, dx, dy, dw, dh, md, _globalAlpha);
    }

    // 图片 DrawImage 的专用桥：直接通过 string id 取纹理 + 构造单实例 + 绘制
    [JSImport("gl.drawImageById", "main.js")]
    private static partial void JsDrawImageById(
        string id,
        double dx, double dy, double dw, double dh,
        double[] matrix,
        double alpha);
}
