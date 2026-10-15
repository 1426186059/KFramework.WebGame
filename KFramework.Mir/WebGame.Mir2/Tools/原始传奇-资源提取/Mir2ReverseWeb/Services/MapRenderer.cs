using Mir2ReverseWeb.Models;
using SkiaSharp;

namespace Mir2ReverseWeb.Services;

/// <summary>
/// 把 MirMapData 的单元格标志渲染为可走性掩码 PNG。
/// 仅负责呈现（Skia），不持有业务状态，可与 MapDecoder 配合用于导出。
/// </summary>
public static class MapRenderer
{
    // 真实单元格像素比 = MAP_GRID 48:32 = 3:2，保持此比例即等于真实地图比例
    private const int CellAspectW = 3;
    private const int CellAspectH = 2;

    /// <summary>渲染掩码，最长边不超过 maxSide，并严格保持 3:2 单元格比例（即真实地图比例）。</summary>
    public static SKBitmap RenderMask(MirMapData map, int maxSide)
    {
        int cols = Math.Max(1, map.Cols);
        int rows = Math.Max(1, map.Rows);

        // 由 maxSide 与网格比反推每格像素，使宽、高都不超 maxSide
        double cwFromW = maxSide / (double)cols;
        double cwFromH = (maxSide / (double)rows) * (CellAspectH / (double)CellAspectW);
        int cw = Math.Max(1, (int)Math.Floor(Math.Min(cwFromW, cwFromH)));
        int ch = Math.Max(1, (int)Math.Round(cw * CellAspectH / (double)CellAspectW));

        var bmp = new SKBitmap(Math.Max(1, cols) * cw, Math.Max(1, rows) * ch);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(new SKColor(18, 22, 28));
        using var pWalk = new SKPaint { Color = new SKColor(56, 180, 99) };
        using var pBlock = new SKPaint { Color = new SKColor(38, 46, 58) };
        using var pSafe = new SKPaint { Color = new SKColor(230, 200, 40) };
        using var pCover = new SKPaint { Color = new SKColor(220, 90, 80) };

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                int i = r * cols + c;
                if (i >= map.CellFlags.Length) break;
                int nib = map.CellFlags[i];
                bool w = (nib & 8) != 0, cov = (nib & 4) != 0, sf = (nib & 2) != 0;
                var paint = !w ? pBlock : (sf ? pSafe : pWalk);
                canvas.DrawRect(c * cw, r * ch, cw, ch, paint);
                if (cov) canvas.DrawRect(c * cw, r * ch, cw, Math.Max(1, ch / 2), pCover); // 遮挡：上半红
            }
        }
        return bmp;
    }

    /// <summary>等比缩放位图，最长边不超过 maxW（返回新位图，调用方负责释放）</summary>
    public static SKBitmap ScaleDown(SKBitmap src, int maxW)
    {
        if (src.Width <= maxW) return src.Copy()!;
        float scale = maxW / (float)src.Width;
        var info = new SKImageInfo((int)(src.Width * scale), (int)(src.Height * scale));
        var outBmp = new SKBitmap(info);
        using var canvas = new SKCanvas(outBmp);
        canvas.DrawBitmap(src, SKRect.Create(0, 0, outBmp.Width, outBmp.Height), SKSamplingOptions.Default);
        return outBmp;
    }

    /// <summary>编码为 PNG 字节</summary>
    public static byte[] EncodePng(SKBitmap bmp)
    {
        using var img = SKImage.FromBitmap(bmp);
        using var png = img.Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }
}
