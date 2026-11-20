using KFramework.MonoGame;
using System.Collections.Concurrent;

public static class Mir2OriginResource
{
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

    // —— 文本（string）取数：GetTextAsync 直接委托底层 LoadTextAsync（Cache 做 HTTP 级去重/驻留），
    // 不另设 string 字典缓存——图集/配置/地图等消费方各自已缓存解析结果（_sheets/_config/_entries/...），
    // 首次加载后不会重复取，故源 json 字符串无需在此层冗余驻留。

    // —— 解码后 Texture2D 的内存缓存（key = 规范化相对路径/URL）——
    // 这是原始传奇 RES.host（root+name → 解码 Texture/SpriteSheet）在我们工程里的对应层：所有远程图
    // （模型图集 / UI 图集 / 人物立绘 / 物品图标 / 地图瓦片）统一经此取解码纹理，首次下载+解码后常驻复用，
    // 不再每次 GetTextureAsync 都重新 decode + CreateTexture（之前每帧/每次取都重建，是显存与 CPU 双重浪费）。
    // 用「引用计数 + 时间/容量 LRU」管理释放：纹理仅在 refCount==0（上层图集已淘汰）且超过 TrimAge 后
    // 才 Dispose——这样上层 Mir2AtlasSheet 持有的同一份 Texture2D 不会被本层单方面释放而悬空。
    private sealed class TexEntry
    {
        public Task<Texture2D> Task;
        public long LastAccess;
        public int RefCount;
    }
    private static readonly ConcurrentDictionary<string, TexEntry> _texCache =
        new(StringComparer.OrdinalIgnoreCase);

    private const int TexCacheMaxEntries = 1500;     // 容量预算（条目数），超即触发淘汰
    private const long TexTrimAgeMs = 30_000;       // 无引用且超过该时长(ms)才释放（对齐原版 SKIN_TIME_OUT）
    private const long TexTrimIntervalMs = 1500;    // 淘汰轮询间隔（对齐原版 CacheManager.update 1500ms）
    private static long _lastTexTrimTick;

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
    private static string Norm(string p)
    {
        return BrowserResource.NormalizePath(p);
    }

    // ===================== 通用取文本 =====================
    /// <summary>按相对路径取文本（UTF-8 解码）。直接委托底层 LoadTextAsync（Cache 做 HTTP 级去重/驻留），
    /// 不另设 string 字典缓存——图集/配置/地图等消费方各自已缓存解析结果，首次加载后不会重复取。</summary>
    public static Task<string> GetTextAsync(string relativePath, int priority = 0, CancellationToken ct = default)
    {
        string key = Norm(relativePath);
        return _imageContent.LoadTextAsync(key, Cache, priority, ct);
    }
    /// <summary>便捷重载：直接以 ct 作为第二参数。</summary>
    public static Task<string> GetTextAsync(string relativePath, CancellationToken ct)
    {
        return GetTextAsync(relativePath, 0, ct);
    }

    // ===================== 原始传奇音效 =====================

    /// <summary>
    /// 原始传奇音效相对路径。JS ResUrl.url(id+"",15) → AudioIcon("audio/")+id+mp3Ext(".mp3")，
    /// 即 assets/resource/audio/{soundId}.mp3。技能音效 id 来自 5config.skillEffects[].sound。
    /// </summary>
    public static string OriginAudioPath(int soundId)
    {
        return $"assets/resource/audio/{soundId}.mp3?ver={ResourceVersion}";
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, byte[]> _originAudioCache = new();

    /// <summary>取原始传奇音效字节（内存缓存：同一 id 只下载一次）。缺失返回 null。</summary>
    public static async Task<byte[]> GetOriginAudioBytesAsync(int soundId)
    {
        if (_originAudioCache.TryGetValue(soundId, out var cached)) return cached;
        try
        {
            byte[] bytes = await _dataContent.LoadBytesAsync(OriginAudioPath(soundId), Cache, priority: 0).ConfigureAwait(false);
            if (bytes == null || bytes.Length == 0) return null;
            _originAudioCache[soundId] = bytes;
            return bytes;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>按相对路径取纹理（浏览器原生解码 jpg/png → RGBA Texture2D）。
    /// 走 _texCache：同一 URL 只在首次下载+解码，之后 GetOrAdd 复用同一份，所有调用方共享；
    /// 引用计数 +1（上层不再需要时调 ReleaseTexture）；超预算且引用归零的纹理由 TrimTextureCache 释放。</summary>
    public static Task<Texture2D> GetTextureAsync(string relativePath, int priority = 0, CancellationToken ct = default, bool readable = false)
    {
        // readable=true 会保留 CPU 像素副本（供图集 alpha 命中检测），内存成本更高且 GetData 可用；
        // 用后缀区分缓存键，避免与同一 URL 的普通（不可读）纹理串到同一份。
        string path = Norm(relativePath);
        string key = readable ? path + "#R" : path;
        var entry = _texCache.GetOrAdd(key, k => new TexEntry
        {
            // 缓存的 Task 绝不能绑定某一次调用的 ct：技能栏多个 MirImageControl 共享同一 URL 的缓存项，
            // 任一控件 RefreshUI / 换绑时 Cancel 自己的 _remoteCts，若 ct 传进这里，这个被取消的 Task
            // 就被永久缓存在 _texCache，之后所有取该图标的地方 await 都抛 OperationCanceledException，
            // 图标再也出不来（日志里 icon/skills/*.png 的 TaskCanceledException 正是此因）。
            // 下载本身不被单个控件取消；ct 只用于“本次等待是否提前返回”，见下方 AwaitWithCancellation。
            Task = _imageContent.LoadTexture2DAsync(path, Cache, priority, readable: readable),
            LastAccess = Environment.TickCount64,
            RefCount = 0,
        });
        Interlocked.Increment(ref entry.RefCount);
        entry.LastAccess = Environment.TickCount64;
        TrimTextureCache();

        if (ct.CanBeCanceled)
            return AwaitWithCancellation(entry.Task, ct);
        return entry.Task;
    }

    private static async Task<Texture2D> AwaitWithCancellation(Task<Texture2D> task, CancellationToken ct)
    {
        return await task.WaitAsync(ct).ConfigureAwait(false);
    }
    /// <summary>便捷重载：直接以 ct 作为第三参数（避免把 ct 误传给 priority）。</summary>
    public static Task<Texture2D> GetTextureAsync(string relativePath, CancellationToken ct)
    {
        return GetTextureAsync(relativePath, 0, ct);
    }
    /// <summary>同步取出已就绪的纹理（不触发下载）。供绘制路径判断「图下来了没」（物品图标等）。
    /// 只读取 _texCache 的完成态；是否该（重新）拉取由上层（IsMissing 冷却）决定。</summary>
    public static bool TryGetTexture(string relativePath, out Texture2D tex)
    {
        tex = null;
        if (_texCache.TryGetValue(Norm(relativePath), out var entry) && entry != null
            && entry.Task != null && entry.Task.IsCompletedSuccessfully)
        {
            tex = entry.Task.Result;
            return tex != null;
        }
        return false;
    }

    /// <summary>上层不再需要某纹理时调用（图集从 _sheets 淘汰、物品纹理同样走本层 _texCache；场景切换释放）。refCount 归 0 后，
    /// 该纹理在 TrimTextureCache 超时被 Dispose，真正释放显存。</summary>
    public static void ReleaseTexture(string relativePath)
    {
        string key = Norm(relativePath);
        if (_texCache.TryGetValue(key, out var entry))
            Interlocked.Decrement(ref entry.RefCount);
    }

    /// <summary>显式释放一批纹理（按 key 匹配，对齐原版 RES.destroyRes 按组/场景卸载）。调用方需确保这些纹理已无引用。</summary>
    public static void ReleaseTextures(Func<string, bool> match)
    {
        foreach (var kv in _texCache.Where(e => match(e.Key)).ToList())
        {
            if (_texCache.TryRemove(kv.Key, out var entry) && entry.Task != null && entry.Task.IsCompletedSuccessfully)
                entry.Task.Result?.Dispose();
        }
    }

    /// <summary>纹理缓存淘汰（对齐原版 Egret 的 texture.dispose + 定时定量清理）：超容量预算且距上次扫描
    /// ≥ TexTrimIntervalMs 时，把「refCount==0（上层图集已淘汰）且超过 TexTrimAgeMs 未再使用」的纹理 Dispose 并移除。
    /// 只释放无人引用的纹理，上层 Mir2AtlasSheet 正在持有的不会被选中，绘制不会崩。</summary>
    private static void TrimTextureCache()
    {
        long now = Environment.TickCount64;
        if (now - _lastTexTrimTick < TexTrimIntervalMs) return;
        if (_texCache.Count <= TexCacheMaxEntries) { _lastTexTrimTick = now; return; }
        _lastTexTrimTick = now;

        long cutoff = now - TexTrimAgeMs;
        int toEvict = _texCache.Count - TexCacheMaxEntries + 32;
        foreach (var kv in _texCache
            .Where(e => e.Value.RefCount <= 0 && e.Value.LastAccess < cutoff)
            .OrderBy(e => e.Value.LastAccess)
            .Take(toEvict))
        {
            if (_texCache.Count <= TexCacheMaxEntries) break;
            if (!_texCache.TryRemove(kv.Key, out var entry)) continue;
            if (entry.Task != null && entry.Task.IsCompletedSuccessfully) entry.Task.Result?.Dispose();
        }
    }

    /// <summary>逻辑地图数据等：按相对路径取字节流（数据服务器根，非图片 CDN）。</summary>
    public static Task<byte[]> GetDataBytesAsync(string relativePath, int priority = 10, CancellationToken ct = default)
    {
        return _dataContent.LoadBytesAsync(Norm(relativePath), Cache, priority, ct);
    }
    /// <summary>便捷重载：直接以 ct 作为第二参数。</summary>
    public static Task<byte[]> GetDataBytesAsync(string relativePath, CancellationToken ct)
    {
        return GetDataBytesAsync(relativePath, 10, ct);
    }

    // ===================== ResUrl 映射（与线上 main.min.js 对齐）=====================
    // 地图资源优先级最高（与 BrowserResource.PriorityMap=10 一致）：进图/传送后第一时间出地面。

    /// <summary>真实地图瓦片：assets/resource/zipmap/{img}/{index}.jpg（512×256，index 从 1 开始、无零补零）。</summary>
    public static string MapTilePath(int img, int index)
    {
        return $"assets/resource/zipmap/{img}/{index}.jpg?ver={ResourceVersion}";
    }
    public static Task<Texture2D> GetMapTileTextureAsync(int img, int index, CancellationToken ct = default)
    {
        return GetTextureAsync(MapTilePath(img, index), 10, ct);
    }

    /// <summary>小地图：assets/resource/minimap/{img}.jpg（优先独立整图；图集帧回退由上层处理）。</summary>
    public static string MinimapPath(int img)
    {
        return $"assets/resource/minimap/{img}.jpg?ver={ResourceVersion}";
    }
    public static Task<Texture2D> GetMinimapTextureAsync(int img, CancellationToken ct = default)
    {
        return GetTextureAsync(MinimapPath(img), 10, ct);
    }

    /// <summary>大地图（世界地图整图）：assets/resource/bigmap/{img}.jpg。路径按站点资源规律推断，若实际不符请调整此处。</summary>
    public static string BigMapPath(int img)
    {
        return $"assets/resource/bigmap/{img}.jpg?ver={ResourceVersion}";
    }
    public static Task<Texture2D> GetBigMapTextureAsync(int img, CancellationToken ct = default)
    {
        return GetTextureAsync(BigMapPath(img), 10, ct);
    }

    /// <summary>配置分片：{i}config{GameVersion}.json（地图表 map 在 0config 分片）。</summary>
    public static string ConfigShardPath(int shard)
    {
        return $"{shard}config{GameVersion}.json?v={GameVersion}";
    }
    // 配置分片文本进程内复用：0config 等超大分片（6.7MB / 175 表）会被 ExcelTableMgr 与
    // Mir2MapSource 等多个模块各自请求。若每次都重新下载 + UTF-8 解码，进 GameScene 时叠加会长时间
    // 阻塞主线程（WASM 单线程，Task.Run 不另起 OS 线程），导致心跳（5s）超时断开。故在此层对同一
    // shard 只下载/解码一次，各调用方共享同一份 string。
    // 注意：图集/UI 帧 JSON 不在此缓存——其解析结果由 Mir2AtlasLibrary._sheets 自身缓存，源文本无复用需求。
    private static readonly ConcurrentDictionary<int, Task<string>> _configShardTextCache =
        new ConcurrentDictionary<int, Task<string>>();
    public static Task<string> GetConfigShardTextAsync(int shard, CancellationToken ct = default)
    {
        return _configShardTextCache.GetOrAdd(shard, s => GetTextAsync(ConfigShardPath(s), ct: ct));
    }

    /// <summary>逻辑地图数据：{data}.bin / .dat / .csv（数据服务器根，非图片 CDN）。</summary>
    // ===================== UI / 图集资源（manifest 索引：key → {v,s}）=====================
    // CDN：实际文件 = resource/{v前2}/{v}_{s}.{ext}
    // 本地镜像（KeyPath 模式）：直接用 key 原路径
    /// <summary>资源总索引 manifest（图集 + UI + 配置 总索引）；由 LoadManifestAsync 填充。</summary>
    public static Mir2AtlasManifest Manifest { get; private set; }

    /// <summary>拉取资源清单 manifest（图集 + UI + 配置 总索引），供 UI/图集资源按 key 解析。解析委托 Mir2AtlasManifest。</summary>
    public static async Task LoadManifestAsync(CancellationToken ct = default)
    {
        var json = await GetTextAsync($"allmanifest{GameVersion}.json?v=20260922111844&qufu_version=229", ct: ct);
        Manifest = Mir2AtlasManifest.Deserialize(json);
        //PrintTool.Log($"[Mir2Origin] manifest 已加载：{Manifest.Entries.Count} 条");
    }

    /// <summary>把 manifest key 解析成实际相对路径：本地镜像(Keypath)→key 原路径；CDN→resource/{v前2}/{v}_{s}.{ext}。</summary>
    public static string ResolveManifestAssetPath(string key)
    {
        return Manifest?.ResolvePath(key, LocalMirrorUsesKeyPath) ?? key;
    }

    public static Task<Texture2D> GetManifestAssetTextureAsync(string key, int priority = Mir2OriginObjectLibrary.PriorityUI, CancellationToken ct = default)
    {
        return GetTextureAsync(ResolveManifestAssetPath(key), priority, ct: ct);
    }
}
