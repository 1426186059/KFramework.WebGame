using System.Runtime.InteropServices.JavaScript;

namespace MirClient.Rendering;

/// <summary>
/// HTML5 Canvas 绘制门面。
///
/// 两种互操作模式（可运行时切换，用于性能对比）：
///   - 直调模式：每次 Draw 都跨 C#/JS 边界一次调用；
///   - 批命令模式：C# 侧把绘制指令写入 int 缓冲，每帧只做一次跨边界调用。
///
/// 对应 tsengine/src/render/webgl/webgl2d.ts（mir.createImage / drawImage / drawBatch / fillRect / drawText / measureText / clear / setStatus）。
/// </summary>
internal static partial class MirCanvas
{
    private const int CmdStride = 9; // key,sx,sy,sw,sh,dx,dy,dw,dh

    private static int[] _cmd = new int[CmdStride * 16384];
    private static int _cmdCount;

    /// <summary>启用批命令模式（每帧一次跨边界调用）。</summary>
    public static bool Batched;

    public static int DrawCalls { get; private set; }

    public static void BeginFrame()
    {
        _cmdCount = 0;
        DrawCalls = 0;
    }

    public static void Draw(int key, int sx, int sy, int sw, int sh, int dx, int dy, int dw, int dh)
    {
        DrawCalls++;

        if (!Batched)
        {
            DrawImageImpl(key, sx, sy, sw, sh, dx, dy, dw, dh);
            return;
        }

        if ((_cmdCount + 1) * CmdStride > _cmd.Length)
            Flush();

        int o = _cmdCount * CmdStride;
        _cmd[o] = key;
        _cmd[o + 1] = sx;
        _cmd[o + 2] = sy;
        _cmd[o + 3] = sw;
        _cmd[o + 4] = sh;
        _cmd[o + 5] = dx;
        _cmd[o + 6] = dy;
        _cmd[o + 7] = dw;
        _cmd[o + 8] = dh;
        _cmdCount++;
    }

    public static void Flush()
    {
        if (_cmdCount == 0) return;

        DrawBatchImpl(_cmd, _cmdCount);
        _cmdCount = 0;
    }

    public static void Fill(int x, int y, int w, int h, int argb)
    {
        Flush();
        FillRectImpl(x, y, w, h, argb);
    }

    public static void Text(string text, int x, int y, int argb, string font)
    {
        Flush();
        DrawTextImpl(text, x, y, argb, font);
    }

    public static int Measure(string text, string font) => MeasureTextImpl(text, font);

    // ===================== JS interop =====================

    [JSImport("mir.drawImage", "main.js")]
    private static partial void DrawImageImpl(int key, int sx, int sy, int sw, int sh, int dx, int dy, int dw, int dh);

    [JSImport("mir.drawBatch", "main.js")]
    private static partial void DrawBatchImpl(int[] cmd, int count);

    [JSImport("mir.fillRect", "main.js")]
    private static partial void FillRectImpl(int x, int y, int w, int h, int argb);

    [JSImport("mir.drawText", "main.js")]
    private static partial void DrawTextImpl(string text, int x, int y, int argb, string font);

    [JSImport("mir.measureText", "main.js")]
    private static partial int MeasureTextImpl(string text, string font);

    [JSImport("mir.clear", "main.js")]
    public static partial void Clear(int argb);

    /// <summary>把解码好的 RGBA 像素注册为一张纹理，返回句柄 key。</summary>
    [JSImport("mir.createImage", "main.js")]
    public static partial void CreateImage(int key, byte[] rgba, int width, int height);

    [JSImport("mir.setStatus", "main.js")]
    public static partial void SetStatus(string html);

    [JSImport("mir.log", "main.js")]
    public static partial void Log(string message);
}
