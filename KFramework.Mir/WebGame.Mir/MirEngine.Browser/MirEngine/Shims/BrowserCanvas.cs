using System;
using System.Numerics;
using System.Runtime.InteropServices.JavaScript;

namespace MirEngine;

/// <summary>
/// HTML5 Canvas 渲染后端（JS interop）。每个绘制目标/纹理用 int 句柄标识：
/// 0 = 主画布，&gt;0 = JS 端的 Image（精灵）或离屏 canvas（RenderTarget）。
/// 对应 tsengine/src/render/webgl/webgl-engine.ts（mir.cr* 函数）。
/// </summary>
public static partial class BrowserCanvas
{
    public const int MainTarget = 0;

    [JSImport("mir.crCreateOffscreen", "main.js")]
    private static partial int CreateOffscreenImpl(int w, int h);

    [JSImport("mir.crSetTarget", "main.js")]
    private static partial void SetTargetImpl(int id);

    [JSImport("mir.crSetBlend", "main.js")]
    private static partial void SetBlendImpl(int mode, float rate, bool enabled);

    [JSImport("mir.setBlendProfile", "main.js")]
    private static partial void SetBlendProfileImpl(string profile);

    [JSImport("mir.crClear", "main.js")]
    private static partial void ClearImpl(int r, int g, int b, int a);

    [JSImport("mir.crDraw", "main.js")]
    private static partial void DrawImpl(int tex, int sx, int sy, int sw, int sh, float dx, float dy, float dw, float dh, int colorArgb);

    [JSImport("mir.crMeasureText", "main.js")]
    private static partial string MeasureTextImpl(string text, string fontCss, int maxWidth);

    [JSImport("mir.crFillRect", "main.js")]
    private static partial void FillRectImpl(int x, int y, int w, int h, int colorArgb);

    [JSImport("mir.crDrawLine", "main.js")]
    private static partial void DrawLineImpl(float x1, float y1, float x2, float y2, float w, int colorArgb);

    [JSImport("mir.crFlush", "main.js")]
    private static partial void FlushImpl();

    // 上传已解码的 RGBA 像素为一个纹理句柄（真实 Zircon 客户端 MirImage 用此把 DXT 解码结果送上 canvas）。
    // 复用 main.js 的 mir.createImage（与 demo 的 MirCanvas.CreateImage 同一 JS 函数，按 id 存入 textures Map）。
    [JSImport("mir.createImage", "main.js")]
    private static partial void UploadImageImpl(int id, byte[] rgba, int w, int h);

    [JSImport("mir.disposeImage", "main.js")]
    private static partial void DisposeImageImpl(int id);

    public static int CreateOffscreen(int w, int h) => CreateOffscreenImpl(w, h);
    public static void UploadImage(int id, byte[] rgba, int w, int h) => UploadImageImpl(id, rgba, w, h);
    public static void DisposeImage(int id) => DisposeImageImpl(id);
    public static void SetTarget(int id) => SetTargetImpl(id);
    public static void SetBlendState(int mode, float rate, bool enabled) => SetBlendImpl(mode, rate, enabled);

    /// <summary>
    /// 选择混合模式表。Crystal(Mir2) 与 Zircon(Mir3) 的原版 SetBlend 语义不同，不能共用：
    /// Crystal 的 SetBlend(true, …) 除 INVLIGHT 外一律是加色（SourceAlpha/One），
    /// Zircon 则按 BlendMode 分别对应 screen/multiply/lighter。
    /// 传 "crystal" 用 Mir2 的表，其余（含默认）用 Mir3 的表。必须在首次绘制前调用。
    /// </summary>
    public static void SetBlendProfile(string profile) => SetBlendProfileImpl(profile);
    public static void Clear(int r, int g, int b, int a) => ClearImpl(r, g, b, a);
    public static void Clear(Color c) => ClearImpl(c.R, c.G, c.B, c.A);
    public static void DrawImage(int tex, int sx, int sy, int sw, int sh, float dx, float dy, float dw, float dh, int colorArgb)
        => DrawImpl(tex, sx, sy, sw, sh, dx, dy, dw, dh, colorArgb);

    /// <summary>矩阵变换绘制：把源矩形当作基础几何 (0,0,sw,sh)，经 2D 仿射矩阵变换后绘制。
    /// 对应 canvas-engine.js 的 mir.crDrawTransform，语义对齐原版 DrawTexture 的 transform/center/translation 契约。</summary>
    [JSImport("mir.crDrawTransform", "main.js")]
    private static partial void DrawTransformImpl(int tex, int sx, int sy, int sw, int sh,
        float m11, float m12, float m21, float m22, float m31, float m32, int colorArgb);

    public static void DrawImageTransform(int tex, int sx, int sy, int sw, int sh, Matrix3x2 transform, int colorArgb)
        => DrawTransformImpl(tex, sx, sy, sw, sh,
            transform.M11, transform.M12, transform.M21, transform.M22, transform.M31, transform.M32, colorArgb);
    public static void FillRect(int x, int y, int w, int h, int colorArgb) => FillRectImpl(x, y, w, h, colorArgb);
    public static void DrawLine(float x1, float y1, float x2, float y2, float w, int colorArgb) => DrawLineImpl(x1, y1, x2, y2, w, colorArgb);
    public static void Flush() => FlushImpl();

    /// <summary>用 canvas 2D 的文本测量（fontCss 形如 "bold 12px Tahoma"）。</summary>
    public static Size MeasureText(string text, string fontCss, int maxWidth)
    {
        if (string.IsNullOrEmpty(text)) return Size.Empty;

        string s = MeasureTextImpl(text, fontCss, maxWidth);
        int i = s.IndexOf(',');
        if (i < 0) return new Size(0, 12);

        int w = int.Parse(s.AsSpan(0, i));
        int h = int.Parse(s.AsSpan(i + 1));
        return new Size(w, h);
    }

    /// <summary>把 System.Drawing.Font 转成 canvas 2D 的 CSS font 串，如 "bold 12px 'Arial', sans-serif"。</summary>
    public static string FontToCss(Font f)
    {
        if (f == null) return "10px sans-serif";
        var style = f.Style;
        string weight = (style & FontStyle.Bold) != 0 ? "bold " : string.Empty;
        string slant = (style & FontStyle.Italic) != 0 ? "italic " : string.Empty;
        string name = string.IsNullOrEmpty(f.Name) ? "Tahoma" : f.Name;
        return $"{weight}{slant}{f.Size}px '{name}', sans-serif";
    }

    /// <summary>在离屏 canvas 上把文字渲染成纹理句柄（替代 GDI 的 Bitmap/Graphics/TextRenderer 文本烘焙）。
    /// 对应 tsengine/src/render/webgl/webgl2d.ts 的 mir.drawLabel。</summary>
    [JSImport("mir.drawLabel", "main.js")]
    private static partial void DrawLabelImpl(int handle, int w, int h, string text, string fontCss, int foreArgb, int outlineArgb, int format, int backArgb, int gradTopArgb, int gradBottomArgb, bool gradient);

    public static void DrawLabel(int handle, int w, int h, string text, string fontCss, int foreArgb, int outlineArgb, int format, int backArgb, int gradTopArgb, int gradBottomArgb, bool gradient)
        => DrawLabelImpl(handle, w, h, text, fontCss, foreArgb, outlineArgb, format, backArgb, gradTopArgb, gradBottomArgb, gradient);

    /// <summary>在离屏 canvas 上把文本框内容（背景+选择高亮+文本+光标）渲染成纹理句柄。
    /// 对应 tsengine/src/render/webgl/webgl2d.ts 的 mir.drawTextBox。</summary>
    [JSImport("mir.drawTextBox", "main.js")]
    private static partial void DrawTextBoxImpl(int handle, int w, int h, string text, string fontCss, int foreArgb, int backArgb, int selBackArgb, int caretArgb, int selStart, int selLength, int caretPos, bool caretVisible, bool verticalCenter);

    public static void DrawTextBox(int handle, int w, int h, string text, string fontCss, int foreArgb, int backArgb, int selBackArgb, int caretArgb, int selStart, int selLength, int caretPos, bool caretVisible, bool verticalCenter)
        => DrawTextBoxImpl(handle, w, h, text, fontCss, foreArgb, backArgb, selBackArgb, caretArgb, selStart, selLength, caretPos, caretVisible, verticalCenter);
}

/// <summary>DXT1/DXT5 软件解码器（Canvas 纹理锁定路径用，把压缩块解码成 RGBA）。</summary>
public static class DxtDecoder
{
    public static byte[] Decode(ReadOnlySpan<byte> data, int width, int height, bool dxt1)
    {
        int blocksX = (width + 3) / 4;
        int blocksY = (height + 3) / 4;
        byte[] rgba = new byte[width * height * 4];

        for (int by = 0; by < blocksY; by++)
        {
            for (int bx = 0; bx < blocksX; bx++)
            {
                int blockIndex = by * blocksX + bx;
                int srcOff = blockIndex * (dxt1 ? 8 : 16);
                if (srcOff + (dxt1 ? 8 : 16) > data.Length) continue;

                ushort c0 = (ushort)(data[srcOff] | (data[srcOff + 1] << 8));
                ushort c1 = (ushort)(data[srcOff + 2] | (data[srcOff + 3] << 8));

                int[] colors = new int[4];
                colors[0] = ExpandRGB565(c0);
                colors[1] = ExpandRGB565(c1);
                if (dxt1)
                {
                    if (c0 > c1) { colors[2] = Lerp(colors[0], colors[1], 1, 3); colors[3] = Lerp(colors[0], colors[1], 2, 3); }
                    else { colors[2] = Lerp(colors[0], colors[1], 1, 2); colors[3] = 0; }
                }
                else
                {
                    colors[2] = Lerp(colors[0], colors[1], 1, 3);
                    colors[3] = Lerp(colors[0], colors[1], 2, 3);
                }

                int colorIdxOff = dxt1 ? srcOff + 4 : srcOff + 8;
                byte[] alpha = null;
                if (!dxt1)
                {
                    alpha = new byte[16];
                    DecodeDXT5Alpha(data, srcOff + 2, data[srcOff], data[srcOff + 1], alpha);
                }

                for (int y = 0; y < 4; y++)
                {
                    for (int x = 0; x < 4; x++)
                    {
                        int px = bx * 4 + x;
                        int py = by * 4 + y;
                        if (px >= width || py >= height) continue;

                        int idx = (data[colorIdxOff + (y * 4 + x) / 4] >> ((x % 4) * 2)) & 0x3;
                        int ci = colors[idx];
                        int di = (py * width + px) * 4;
                        rgba[di] = (byte)(ci & 0xFF);
                        rgba[di + 1] = (byte)((ci >> 8) & 0xFF);
                        rgba[di + 2] = (byte)((ci >> 16) & 0xFF);
                        if (dxt1)
                            rgba[di + 3] = ((c0 <= c1) && idx == 3) ? (byte)0 : (byte)255;
                        else
                            rgba[di + 3] = alpha[y * 4 + x];
                    }
                }
            }
        }
        return rgba;
    }

    private static int ExpandRGB565(ushort c)
    {
        int r = (c >> 11) & 0x1F, g = (c >> 5) & 0x3F, b = c & 0x1F;
        r = (r << 3) | (r >> 2);
        g = (g << 2) | (g >> 4);
        b = (b << 3) | (b >> 2);
        return (r << 16) | (g << 8) | b;
    }

    private static int Lerp(int c0, int c1, int t0, int t1)
    {
        int r0 = (c0 >> 16) & 0xFF, r1 = (c1 >> 16) & 0xFF;
        int g0 = (c0 >> 8) & 0xFF, g1 = (c1 >> 8) & 0xFF;
        int b0 = c0 & 0xFF, b1 = c1 & 0xFF;
        int r = (r0 * (t1 - t0) + r1 * t0) / t1;
        int g = (g0 * (t1 - t0) + g1 * t0) / t1;
        int b = (b0 * (t1 - t0) + b1 * t0) / t1;
        return (r << 16) | (g << 8) | b;
    }

    private static void DecodeDXT5Alpha(ReadOnlySpan<byte> data, int off, byte a0, byte a1, byte[] alpha)
    {
        int[] ac = new int[8];
        ac[0] = a0; ac[1] = a1;
        if (a0 > a1)
        {
            ac[2] = (6 * a0 + a1) / 7; ac[3] = (5 * a0 + 2 * a1) / 7; ac[4] = (4 * a0 + 3 * a1) / 7;
            ac[5] = (3 * a0 + 4 * a1) / 7; ac[6] = (2 * a0 + 5 * a1) / 7; ac[7] = (a0 + 6 * a1) / 7;
        }
        else
        {
            ac[2] = (4 * a0 + a1) / 5; ac[3] = (3 * a0 + 2 * a1) / 5; ac[4] = (2 * a0 + 3 * a1) / 5;
            ac[5] = (a0 + 4 * a1) / 5; ac[6] = 0; ac[7] = 255;
        }
        ulong bits = 0;
        for (int i = 0; i < 6; i++) bits |= (ulong)data[off + i] << (8 * i);
        for (int i = 0; i < 16; i++)
            alpha[i] = (byte)ac[(int)((bits >> (3 * i)) & 0x7)];
    }
}
