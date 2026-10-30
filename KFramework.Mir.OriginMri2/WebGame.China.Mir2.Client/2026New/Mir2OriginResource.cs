using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KFramework.MonoGame;

/// <summary>
/// 原始传奇（移动版）线上资源加载器。
/// 复刻游戏运行时 ResUrl 的「相对路径 → 资源」映射，统一经 KFramework.MonoGame.ContentManager
/// 走 HTTP 异步加载（Cache Storage 缓存 + 优先级调度 + 浏览器原生图片解码），
/// 与现有 .Lib 加载（BrowserResource）共用同一套基建，只是 base URL 不同。
///
/// 两种取数来源（仅 base URL 不同，资源相对路径布局完全一致，切换是配置级的）：
///   • 线上 CDN（默认）：ImageBaseUrl = https://cdn.yscq-wy.yscq.com/fugu_20201203
///   • 本地镜像（Mir2Res/）：把镜像根作为 ImageBaseUrl 自托管，离线 / 不被 CDN 限流。
/// 逻辑地图数据（.bin/.dat）在另一台数据服务器，故 DataBaseUrl 单独可配。
///
/// 直接用 CDN 还是本地资源？
///   建议开发期默认 CDN（零托管、与 ResUrl 完全一致、立刻能跑）；
///   上线 / 怕 CDN 失联或被防盗链时，调用 Configure(镜像绝对URL) 切到自托管的 Mir2Res 镜像即可（一行）。
///   注意：UI/图集资源在 CDN 上是哈希布局 resource/{v前2}/{v}_{s}.{ext}，而 Tools 导出的 Mir2Res 镜像
///   按 manifest key 原路径落盘（resource/assets/...），故 LocalMirrorUsesKeyPath 默认 true 以匹配镜像。
/// </summary>
public static class Mir2OriginResource
{
    // —— 线上 CDN 常量（逆向自 main10.25341.1.min.js / 0config）——
    public const string CdnBase = "https://cdn.yscq-wy.yscq.com/fugu_20201203";
    public const string ResourceVersion = "1.0.2";   // 瓦片 ?ver=
    public const string GameVersion = "10.25341.1";   // 配置分片 / 地图包版本号

    // —— 可配置根地址（默认线上 CDN；切本地镜像改这里）——
    public static string ImageBaseUrl { get; private set; } = CdnBase;
    public static string DataBaseUrl { get; private set; } = CdnBase;

    /// <summary>本地镜像是否按「manifest key 原路径」落盘（Tools 导出的 Mir2Res 即如此）。
    /// true：UI/图集资源按 key 原路径取；false：按 CDN 哈希布局 resource/{v前2}/{v}_{s}.{ext} 取。</summary>
    public static bool LocalMirrorUsesKeyPath { get; set; } = true;

    // 原始传奇资源用独立 ContentManager（base 在构造时固定），不污染 .Lib 的 BrowserResource.Content。
    private static ContentManager _imageContent = new ContentManager("", CdnBase);
    private static ContentManager _dataContent = new ContentManager("", CdnBase);
    private static readonly Caching Cache = Caching.Open("Mir2Origin.Cache");

    /// <summary>切换取数来源（CDN ↔ 本地镜像）。base 必须是绝对 URL（如 https://host/Mir2Res 或 http://127.0.0.1:5080）。</summary>
    public static void Configure(string imageBaseUrl, string dataBaseUrl = null)
    {
        ImageBaseUrl = (imageBaseUrl?.TrimEnd('/') ?? CdnBase);
        DataBaseUrl = (dataBaseUrl?.TrimEnd('/') ?? ImageBaseUrl);
        _imageContent?.Dispose();
        _dataContent?.Dispose();
        _imageContent = new ContentManager("", ImageBaseUrl);
        _dataContent = new ContentManager("", DataBaseUrl);
    }

    // 规范化：相对路径统一成 HTTP 友好形式（复用 BrowserResource 的语义）。
    private static string Norm(string p) => BrowserResource.NormalizePath(p);

    // ===================== 通用取数 =====================
    /// <summary>按相对路径取任意字节流（图片 CDN 根）。</summary>
    public static Task<byte[]> GetBytesAsync(string relativePath, int priority = 0, CancellationToken ct = default)
        => _imageContent.LoadBytesAsync(Norm(relativePath), Cache, priority, ct);
    /// <summary>便捷重载：直接以 ct 作为第二参数（避免把 ct 误传给 priority）。</summary>
    public static Task<byte[]> GetBytesAsync(string relativePath, CancellationToken ct)
        => GetBytesAsync(relativePath, 0, ct);

    /// <summary>按相对路径取纹理（浏览器原生解码 jpg/png → RGBA Texture2D）。</summary>
    public static Task<Texture2D> GetTextureAsync(string relativePath, GraphicsDevice device, int priority = 0, CancellationToken ct = default)
        => _imageContent.LoadTexture2DAsync(Norm(relativePath), device, Cache, priority, ct);
    /// <summary>便捷重载：直接以 ct 作为第三参数（避免把 ct 误传给 priority）。</summary>
    public static Task<Texture2D> GetTextureAsync(string relativePath, GraphicsDevice device, CancellationToken ct)
        => GetTextureAsync(relativePath, device, 0, ct);

    /// <summary>逻辑地图数据等：按相对路径取字节流（数据服务器根，非图片 CDN）。</summary>
    public static Task<byte[]> GetDataBytesAsync(string relativePath, int priority = 10, CancellationToken ct = default)
        => _dataContent.LoadBytesAsync(Norm(relativePath), Cache, priority, ct);
    /// <summary>便捷重载：直接以 ct 作为第二参数。</summary>
    public static Task<byte[]> GetDataBytesAsync(string relativePath, CancellationToken ct)
        => GetDataBytesAsync(relativePath, 10, ct);

    // ===================== ResUrl 映射（与线上 main.min.js 对齐）=====================
    // 地图资源优先级最高（与 BrowserResource.PriorityMap=10 一致）：进图/传送后第一时间出地面。

    /// <summary>真实地图瓦片：assets/resource/zipmap/{img}/{index}.jpg（512×256，index 从 1 开始、无零补零）。</summary>
    public static string MapTilePath(int img, int index) => $"assets/resource/zipmap/{img}/{index}.jpg?ver={ResourceVersion}";
    public static Task<byte[]> GetMapTileBytesAsync(int img, int index, CancellationToken ct = default) => GetBytesAsync(MapTilePath(img, index), 10, ct);
    public static Task<Texture2D> GetMapTileTextureAsync(int img, int index, GraphicsDevice device, CancellationToken ct = default) => GetTextureAsync(MapTilePath(img, index), device, 10, ct);

    /// <summary>小地图：assets/resource/minimap/{img}.jpg（优先独立整图；图集帧回退由上层处理）。</summary>
    public static string MinimapPath(int img) => $"assets/resource/minimap/{img}.jpg?ver={ResourceVersion}";
    public static Task<byte[]> GetMinimapBytesAsync(int img, CancellationToken ct = default) => GetBytesAsync(MinimapPath(img), 10, ct);
    public static Task<Texture2D> GetMinimapTextureAsync(int img, GraphicsDevice device, CancellationToken ct = default) => GetTextureAsync(MinimapPath(img), device, 10, ct);

    /// <summary>大地图（世界地图整图）：assets/resource/bigmap/{img}.jpg。路径按站点资源规律推断，若实际不符请调整此处。</summary>
    public static string BigMapPath(int img) => $"assets/resource/bigmap/{img}.jpg?ver={ResourceVersion}";
    public static Task<byte[]> GetBigMapBytesAsync(int img, CancellationToken ct = default) => GetBytesAsync(BigMapPath(img), 10, ct);
    public static Task<Texture2D> GetBigMapTextureAsync(int img, GraphicsDevice device, CancellationToken ct = default) => GetTextureAsync(BigMapPath(img), device, 10, ct);

    /// <summary>配置分片：{i}config{GameVersion}.json（地图表 map 在 0config 分片）。</summary>
    public static string ConfigShardPath(int shard) => $"{shard}config{GameVersion}.json?v={GameVersion}";
    public static Task<byte[]> GetConfigShardBytesAsync(int shard, CancellationToken ct = default) => GetBytesAsync(ConfigShardPath(shard), ct: ct);
    public static Task<string> GetConfigShardTextAsync(int shard, CancellationToken ct = default) => _imageContent.LoadTextAsync(ConfigShardPath(shard), Cache, cancellationToken: ct);

    /// <summary>逻辑地图数据：{data}.bin / .dat / .csv（数据服务器根，非图片 CDN）。</summary>
    public static Task<byte[]> GetMapDataBytesAsync(int data, string ext, CancellationToken ct = default)
        => GetDataBytesAsync($"{data}.{ext.TrimStart('.')}", ct: ct);

    // ===================== UI / 图集资源（manifest 索引：key → {v,s}）=====================
    // CDN：实际文件 = resource/{v前2}/{v}_{s}.{ext}
    // 本地镜像（KeyPath 模式）：直接用 key 原路径
    /// <summary>资源总索引 manifest（图集 + UI + 配置 总索引）；由 LoadManifestAsync 填充。</summary>
    public static Mir2AtlasManifest Manifest { get; private set; }

    /// <summary>拉取资源清单 manifest（图集 + UI + 配置 总索引），供 UI/图集资源按 key 解析。解析委托 Mir2AtlasManifest。</summary>
    public static async Task LoadManifestAsync(CancellationToken ct = default)
    {
        var json = await GetBytesAsync($"allmanifest{GameVersion}.json?v=20260922111844&qufu_version=229", ct: ct);
        Manifest = Mir2AtlasManifest.Parse(json);
        PrintTool.Log($"[Mir2Origin] manifest 已加载：{Manifest.Entries.Count} 条");
    }

    /// <summary>把 manifest key 解析成实际相对路径：本地镜像(Keypath)→key 原路径；CDN→resource/{v前2}/{v}_{s}.{ext}。</summary>
    public static string ResolveManifestAssetPath(string key)
        => Manifest?.ResolvePath(key, LocalMirrorUsesKeyPath) ?? key;

    public static Task<byte[]> GetManifestAssetBytesAsync(string key, CancellationToken ct = default) => GetBytesAsync(ResolveManifestAssetPath(key), ct: ct);
    public static Task<Texture2D> GetManifestAssetTextureAsync(string key, GraphicsDevice device, CancellationToken ct = default) => GetTextureAsync(ResolveManifestAssetPath(key), device, ct: ct);
}
