
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

    /// <summary>
    /// 加载全部地图相关配置表。每个分片（0~5）只下载/解析一次，单次前向扫描抽出全部需要的表，
    /// 避免之前「每张表各扫 6 个分片」导致 6.7MB 大文件被重复拉取几十次、CDN/缓存拥塞（单次曾达 60+ 秒）。
    /// 失败仅记录日志，不抛出——配置表不是进游戏的硬前置，缺了也不应卡死首屏。
    /// </summary>
    public static async Task InitializeAsync(CancellationToken ct = default)
    {
        if (IsReady) return;
        try
        {
            // 6 个分片并行拉取（URL 各不相同，不会像之前那样几十个并发抢同一批文件）
            var tasks = new Task<byte[]>[6];
            for (int s = 0; s < 6; s++)
                tasks[s] = Mir2OriginResource.GetConfigShardBytesAsync(s, ct);
            var shards = await Task.WhenAll(tasks).ConfigureAwait(false);

            var map = new Dictionary<int, MapTable>();
            var minimap = new Dictionary<int, Dictionary<int, MinimapTable>>();
            var guaji = new Dictionary<int, GuajimapTable>();
            var npc = new Dictionary<int, MapnpcTable>();
            var play = new Dictionary<int, MapPlayTable>();
            var star = new Dictionary<int, StarmapTable>();
            var sheep = new Dictionary<int, Dictionary<int, SheepmapTable>>();
            var xitong = new Dictionary<int, Dictionary<int, XitongMapTable>>();

            // 每个分片只扫描一遍，抽出全部 8 张表（后分片对同 id 覆盖前分片，对齐 JS 端合并语义）
            foreach (var json in shards)
            {
                ExtractTableInto(json, "map", map);
                ExtractNestedInto(json, "minimap", minimap);
                ExtractTableInto(json, "guajimap", guaji);
                ExtractTableInto(json, "mapnpc", npc);
                ExtractTableInto(json, "mapPlay", play);
                ExtractTableInto(json, "starmap", star);
                ExtractNestedInto(json, "sheepmap", sheep);
                ExtractNestedInto(json, "xitongMap", xitong);
            }

            Maps = map;
            Minimaps = minimap;
            GuajiMaps = guaji;
            MapNpcs = npc;
            MapPlays = play;
            StarMaps = star;
            SheepMaps = sheep;
            XitongMaps = xitong;
            IsReady = true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ExcelTableMgr] 配置表加载失败（不影响进游戏）：{ex}");
        }
    }
}
