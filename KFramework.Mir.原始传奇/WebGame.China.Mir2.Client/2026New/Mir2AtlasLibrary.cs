using KFramework.MonoGame;
using System.Text;
using Color = KFramework.MonoGame.Color;
using Rectangle = KFramework.MonoGame.Rectangle;

/// <summary>
/// 原始传奇图集加载 / 解析管理器：在 Mir2OriginResource（取数）之上，把「manifest 索引 + sheet 精灵表」
/// 组装成可用的图集。每张图集 = 整页 Texture2D（sheet.png）+ 若干 KSpriteInfo（每帧：整页贴图 + 源矩形 + 原始尺寸）。
///
/// KSpriteInfo / KSprite 是 KFramework.MonoGame 提供的标准精灵类型，这里直接复用：
///   await Mir2AtlasLibrary.EnsureManifestAsync(ct);
///   var sheet = await Mir2AtlasLibrary.GetSheetAsync(
///       "resource/assets/gameui4/window-sheet/advance-sheet.json", device, ct);
///   // 绘制：可直接取 sheet.Frames["adv_fail"]（KSpriteInfo），也可借 KSprite 句柄
///   Mir2AtlasLibrary.DrawFrame(batch, sheet, "adv_fail", x, y, Color.White);
///
/// 与现有 .Lib 体系（MLibrary/MImage）解耦：这是网页版 Egret 图集，不是 WIL。
/// </summary>
public static class Mir2AtlasLibrary
{
    private static readonly Dictionary<string, Mir2AtlasSheet> _sheets =
        new Dictionary<string, Mir2AtlasSheet>(StringComparer.OrdinalIgnoreCase);

    public static Mir2AtlasManifest Manifest => Mir2OriginResource.Manifest;

    /// <summary>确保 manifest 已加载（CDN 模式下 UI/图集资源需要它做哈希路径解析；本地镜像可跳过）。</summary>
    public static async Task EnsureManifestAsync(CancellationToken ct = default)
    {
        if (Mir2OriginResource.Manifest == null)
            await Mir2OriginResource.LoadManifestAsync(ct);
    }

    /// <summary>按 sheet 的 manifest key 加载一张图集（精灵表 JSON + 整页贴图），结果按 key 缓存。</summary>
    public static async Task<Mir2AtlasSheet> GetSheetAsync(string sheetKey, GraphicsDevice device, CancellationToken ct = default)
    {
        if (_sheets.TryGetValue(sheetKey, out var existing)) return existing;
        await EnsureManifestAsync(ct);

        // ① sheet JSON（帧信息）
        var jsonPath = Mir2OriginResource.ResolveManifestAssetPath(sheetKey);
        var jsonBytes = await Mir2OriginResource.GetBytesAsync(jsonPath, ct: ct);
        if (jsonBytes == null || jsonBytes.Length == 0)
        {
            throw new System.InvalidOperationException(
                $"[Mir2Atlas] 图集 JSON 下载为空：sheetKey={sheetKey}，resolvedPath={jsonPath}。" +
                $"若 resolvedPath 仍是逻辑路径(含 resource/assets/...)且 404，说明 LocalMirrorUsesKeyPath 与 base 不匹配；" +
                $"若 resolvedPath 已是哈希路径(resource/xx/...)仍空，说明该 key 不在 manifest 或资源缺失。");
        }
        var sheet = Mir2AtlasSheet.Parse(Encoding.UTF8.GetString(jsonBytes));

        // ② 同目录整页贴图（png）
        var pngKey = SheetPngKey(sheetKey, sheet.File);
        var pngPath = Mir2OriginResource.ResolveManifestAssetPath(pngKey);
        var texture = await Mir2OriginResource.GetTextureAsync(pngPath, device, ct: ct);

        sheet.Build(texture);
        _sheets[sheetKey] = sheet;
        PrintTool.Log($"[Mir2Atlas] 已加载图集 {sheetKey}：{sheet.Frames.Count} 帧");
        return sheet;
    }

    /// <summary>从已加载图集取某帧的 KSpriteInfo（未加载返回 false）。</summary>
    public static bool TryGetFrame(string sheetKey, string frameName, out KSpriteInfo frame)
    {
        frame = null;
        return _sheets.TryGetValue(sheetKey, out var s) && s.Frames.TryGetValue(frameName, out frame);
    }

    /// <summary>按 KSpriteInfo 绘制一帧。目标尺寸取源矩形尺寸（与框架 SpriteBatchExtensions.Draw 一致），position 为帧左上角。</summary>
    public static void DrawFrame(this SpriteBatch batch, KSpriteInfo sprite, int x, int y, Color color)
    {
        var r = sprite.SourceRectangle;
        batch.Draw(sprite.Texture, new Rectangle(x, y, r.Width, r.Height), r, color);
    }

    /// <summary>按 KSprite 句柄绘制一帧（KSprite 可经隐式转换由 KSpriteInfo 得到，等价于上面的绘制）。</summary>
    public static void DrawFrame(this SpriteBatch batch, KSprite sprite, int x, int y, Color color)
    {
        var r = sprite.Rectangle;
        batch.Draw(sprite.Texture, new Rectangle(x, y, r.Width, r.Height), r, color);
    }

    /// <summary>按「已加载图集 + 帧名」绘制一帧（帧不存在则静默跳过）。</summary>
    public static void DrawFrame(this SpriteBatch batch, Mir2AtlasSheet sheet, string frameName, int x, int y, Color color)
    {
        if (sheet.Frames.TryGetValue(frameName, out var s)) DrawFrame(batch, s, x, y, color);
    }

    private static string SheetPngKey(string sheetKey, string pngFile)
    {
        var idx = sheetKey.LastIndexOf('/');
        var dir = idx >= 0 ? sheetKey.Substring(0, idx) : "";
        return dir.Length == 0 ? pngFile : dir + "/" + pngFile;
    }
}
