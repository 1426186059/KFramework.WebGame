using System.Collections.Generic;
using Client.MirGraphics;
using KFramework.MonoGame;

/// <summary>
/// 统一管理所有“缺失/未就绪”占位的马赛克纹理，避免马赛克生成逻辑散落各处。
/// - MosaicImage：暗灰细棋盘（46/22），用于库图/物件（与原版国服传奇一致，不刺眼不抢画面）；
/// - GetFloorMosaic(w,h)：土/金亮色调马赛克，用于 CDN 地图瓦片缺失占位（大地观感、比库图更亮）。
/// 颜色与格子尺寸集中在此，便于统一调参。
/// </summary>
public static class MosaicManager
{
    // —— 库图/物件占位：国服传奇风格暗细棋盘 ——
    private const int ObjCell = 4;          // 4px 小格
    private const byte ObjLight = 46, ObjDark = 22;
    private static MImage _mosaicImage;
    public static MImage MosaicImage
    {
        get
        {
            if (_mosaicImage == null)
            {
                int s = 32;
                byte[] rgba = new byte[s * s * 4];
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        bool a = ((x / ObjCell) + (y / ObjCell)) % 2 == 0;
                        int i = (y * s + x) * 4;
                        if (a) { rgba[i] = ObjLight; rgba[i + 1] = ObjLight; rgba[i + 2] = ObjLight; rgba[i + 3] = 255; }
                        else { rgba[i] = ObjDark; rgba[i + 1] = ObjDark; rgba[i + 2] = ObjDark; rgba[i + 3] = 255; }
                    }
                _mosaicImage = new MImage { Width = (short)s, Height = (short)s, Image = DXManager.GDevice.CreateTexture(s, s, rgba), TextureValid = true };
            }
            return _mosaicImage;
        }
    }

    // —— 地图地板占位：土/金亮色调，比库图更亮，贴合“大地”观感 ——
    private const int FloorCell = 16;        // 16px 小格 → 512 瓦片上呈 32×32 细马赛克
    private const byte FloorLightR = 214, FloorLightG = 184, FloorLightB = 116;  // 沙金亮格
    private const byte FloorDarkR = 165, FloorDarkG = 128, FloorDarkB = 72;      // 土褐暗格
    private static readonly Dictionary<long, Texture2D> _floorMosaics = new Dictionary<long, Texture2D>();
    public static Texture2D GetFloorMosaic(int w, int h)
    {
        long key = ((long)w << 32) | (uint)h;
        if (_floorMosaics.TryGetValue(key, out var t) && t != null) return t;

        byte[] rgba = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool light = ((x / FloorCell) + (y / FloorCell)) % 2 == 0;
                int i = (y * w + x) * 4;
                rgba[i] = light ? FloorLightR : FloorDarkR;
                rgba[i + 1] = light ? FloorLightG : FloorDarkG;
                rgba[i + 2] = light ? FloorLightB : FloorDarkB;
                rgba[i + 3] = 255;
            }
        t = DXManager.GDevice.CreateTexture(w, h, rgba);
        _floorMosaics[key] = t;
        return t;
    }
}
