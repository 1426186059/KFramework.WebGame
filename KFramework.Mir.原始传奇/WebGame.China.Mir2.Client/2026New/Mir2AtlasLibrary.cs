using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KFramework.MonoGame;
// MirEngine 全局 using 也导出了 Color / Rectangle；这里用别名消歧，明确使用 KFramework.MonoGame 版本。
using Color = KFramework.MonoGame.Color;
using Rectangle = KFramework.MonoGame.Rectangle;

/// <summary>
/// 原始传奇图集加载 / 解析管理器：在 Mir2OriginResource（取数）之上，把「manifest 索引 + sheet 精灵表」
/// 组装成可用的图集。每张图集 = 整页 Texture2D（sheet.png）+ 帧矩形（sheet.json）。
///
/// 用法：
///   await Mir2AtlasLibrary.EnsureManifestAsync(ct);                       // 加载总索引（CDN 模式需要）
///   var sheet = await Mir2AtlasLibrary.GetSheetAsync(                      // 加载一张图集（按 key）
///       "resource/assets/gameui4/window-sheet/advance-sheet.json", device, ct);
///   // 绘制：整页贴图 + 帧 source rect（可选裁剪偏移）
///   Mir2AtlasLibrary.DrawFrame(batch, sheet, "adv_fail", x, y, Color.White);
///
/// 与现有 .Lib 体系（MLibrary/MImage）解耦：这是网页版 Egret 图集，不是 WIL。
/// </summary>
public static class Mir2AtlasLibrary
{
    private static readonly Dictionary<string, LoadedSheet> _sheets =
        new Dictionary<string, LoadedSheet>(StringComparer.OrdinalIgnoreCase);

    public static Mir2AtlasManifest Manifest => Mir2OriginResource.Manifest;

    /// <summary>确保 manifest 已加载（CDN 模式下 UI/图集资源需要它做哈希路径解析；本地镜像可跳过）。</summary>
    public static async Task EnsureManifestAsync(CancellationToken ct = default)
    {
        if (Mir2OriginResource.Manifest == null)
            await Mir2OriginResource.LoadManifestAsync(ct);
    }

    /// <summary>按 sheet 的 manifest key 加载一张图集（精灵表 JSON + 整页贴图），结果按 key 缓存。</summary>
    public static async Task<LoadedSheet> GetSheetAsync(string sheetKey, GraphicsDevice device, CancellationToken ct = default)
    {
        if (_sheets.TryGetValue(sheetKey, out var existing)) return existing;
        await EnsureManifestAsync(ct);

        // ① sheet JSON（帧信息）
        var jsonPath = Mir2OriginResource.ResolveManifestAssetPath(sheetKey);
        var jsonBytes = await Mir2OriginResource.GetBytesAsync(jsonPath, ct: ct);
        var sheet = Mir2AtlasSheet.Parse(Encoding.UTF8.GetString(jsonBytes));

        // ② 同目录整页贴图（png）
        var pngKey = SheetPngKey(sheetKey, sheet.File);
        var pngPath = Mir2OriginResource.ResolveManifestAssetPath(pngKey);
        var texture = await Mir2OriginResource.GetTextureAsync(pngPath, device, ct: ct);

        var loaded = new LoadedSheet { Key = sheetKey, Sheet = sheet, Texture = texture };
        _sheets[sheetKey] = loaded;
        PrintTool.Log($"[Mir2Atlas] 已加载图集 {sheetKey}：{sheet.Frames.Count} 帧");
        return loaded;
    }

    /// <summary>从已加载图集取某帧（未加载返回 false）。</summary>
    public static bool TryGetFrame(string sheetKey, string frameName, out Mir2AtlasFrame frame)
    {
        frame = null;
        return _sheets.TryGetValue(sheetKey, out var s) && s.Sheet.Frames.TryGetValue(frameName, out frame);
    }

    /// <summary>按「整页贴图 + 帧」绘制一帧：整页贴图 + source rect + 裁剪偏移。pos 为源尺寸盒左上角。</summary>
    public static void DrawFrame(this SpriteBatch batch, Texture2D sheet, Mir2AtlasFrame frame, int x, int y, Color color)
    {
        int dx = x + frame.OffX;
        int dy = y + frame.OffY;
        batch.Draw(sheet, new Rectangle(dx, dy, frame.W, frame.H), frame.SourceRect, color);
    }

    /// <summary>按「已加载图集 + 帧名」绘制一帧（帧不存在则静默跳过）。</summary>
    public static void DrawFrame(this SpriteBatch batch, LoadedSheet sheet, string frameName, int x, int y, Color color)
    {
        if (sheet.Sheet.Frames.TryGetValue(frameName, out var f))
            DrawFrame(batch, sheet.Texture, f, x, y, color);
    }

    private static string SheetPngKey(string sheetKey, string pngFile)
    {
        var idx = sheetKey.LastIndexOf('/');
        var dir = idx >= 0 ? sheetKey.Substring(0, idx) : "";
        return dir.Length == 0 ? pngFile : dir + "/" + pngFile;
    }
}

/// <summary>已加载的一张图集：整页贴图 + 帧信息。</summary>
public sealed class LoadedSheet
{
    public string Key;
    public Mir2AtlasSheet Sheet;
    public Texture2D Texture;
}
