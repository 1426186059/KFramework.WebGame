
public static partial class ExcelTableMgr
{
    // ── 已加载的表（InitializeAsync 后填充）──
    public static Dictionary<int, MapTable> Maps { get; private set; } = new();
    public static Dictionary<int, Dictionary<int, MinimapTable>> Minimaps { get; private set; } = new();
    public static Dictionary<int, GuajimapTable> GuajiMaps { get; private set; } = new();
    public static Dictionary<int, MapnpcTable> MapNpcs { get; private set; } = new();
    public static Dictionary<int, MapPlayTable> MapPlays { get; private set; } = new();
    public static Dictionary<int, StarmapTable> StarMaps { get; private set; } = new();
    public static Dictionary<int, Dictionary<int, SheepmapTable>> SheepMaps { get; private set; } = new();
    public static Dictionary<int, Dictionary<int, XitongMapTable>> XitongMaps { get; private set; } = new();

    public static bool IsReady { get; private set; }

    /// <summary>并行加载全部地图相关配置表（按表名自动跨 6 个分片定位，重复分片字节由 HTTP 层缓存复用）。</summary>
    public static async Task InitializeAsync(CancellationToken ct = default)
    {
        if (IsReady) return;
        var tMap = LoadMapConfigAsync(ct);
        var tMinimap = LoadMinimapConfigAsync(ct);
        var tGuaji = LoadTableAsync<GuajimapTable>("guajimap", ct);
        var tNpc = LoadTableAsync<MapnpcTable>("mapnpc", ct);
        var tPlay = LoadTableAsync<MapPlayTable>("mapPlay", ct);
        var tStar = LoadTableAsync<StarmapTable>("starmap", ct);
        var tSheep = LoadNestedTableAsync<SheepmapTable>("sheepmap", ct);
        var tXitong = LoadNestedTableAsync<XitongMapTable>("xitongMap", ct);

        await Task.WhenAll(tMap, tMinimap, tGuaji, tNpc, tPlay, tStar, tSheep, tXitong).ConfigureAwait(false);

        Maps = tMap.Result;
        Minimaps = tMinimap.Result;
        GuajiMaps = tGuaji.Result;
        MapNpcs = tNpc.Result;
        MapPlays = tPlay.Result;
        StarMaps = tStar.Result;
        SheepMaps = tSheep.Result;
        XitongMaps = tXitong.Result;
        IsReady = true;
    }
}
