using KFramework.MonoGame;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

public static partial class ExcelTableMgr
{
    // ── 预加载的 config 分片（shard → 原始 JSON，下载后解析为只读 DOM 缓存）──
    private static readonly Dictionary<int, JsonElement> _shards = new();

    // ── 已加载的表（InitializeAsync 后填充）──
    public static Dictionary<int, MapTable> Maps { get; private set; } = new();
    public static Dictionary<long, ModelInfoTable> ModelInfos { get; private set; } = new();
    public static Dictionary<int, List<int>> ModelActions { get; private set; } = new();
    public static bool IsReady { get; private set; }

    /// <summary>
    /// 1) 预加载所需 config 分片（0config=map，2config=modelInfo 等），缓存为 JsonElement；
    /// 2) 再从分片里解析出特定表（map、modelInfo）。分片只下载一次。
    /// 全程走源生成 + JsonTool，AOT 安全。失败仅记录日志，不抛出——配置表不是进游戏的硬前置。
    /// </summary>
    public static async Task InitializeAsync(CancellationToken ct = default)
    {
        if (IsReady) return;
        try
        {
            PrintTool.Log("[ExcelTableMgr] Start initializing");
            // 1) 预加载 config 分片
            await PreloadShardsAsync(new[] { 0, 2 }, ct).ConfigureAwait(false);

            PrintTool.Log("[ExcelTableMgr] 配置表预加载完成");
            // 2) 从分片加载特定表
            LoadMaps();
            LoadModelInfo();

            PrintTool.Log("[ExcelTableMgr] IsReady True");
            IsReady = true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ExcelTableMgr] 配置表加载失败：{ex}");
        }
    }

    private static async Task PreloadShardsAsync(int[] shards, CancellationToken ct)
    {
        foreach (var shard in shards)
        {
            if (_shards.ContainsKey(shard)) continue;
            PrintTool.Log("[ExcelTableMgr] PreloadShardsAsync 0000");
            byte[] bytes = await Mir2OriginResource.GetConfigShardBytesAsync(shard, ct).ConfigureAwait(false);
            PrintTool.Log("[ExcelTableMgr] PreloadShardsAsync 11111");
            if (bytes == null || bytes.Length == 0) continue;
            _shards[shard] = JsonElement.Parse(bytes);
        }
    }

    // 在已缓存的分片里取名为 tableName 的顶层表
    private static bool GetTable(int shard, string tableName, out JsonElement table)
    {
        if (_shards.TryGetValue(shard, out var doc) && doc.TryGetProperty(tableName, out table))
            return true;
        table = default;
        return false;
    }

    private static void LoadMaps()
    {
        if (!GetTable(0, "map", out var table)) return;
        var map = new Dictionary<int, MapTable>();
        var options = new JsonSerializerOptions { TypeInfoResolver = new ConfigTypeInfoResolver() };
        var mapInfo = (JsonTypeInfo<MapTable>)options.GetTypeInfo(typeof(MapTable))!;
        foreach (var prop in table.EnumerateObject())
        {
            if (int.TryParse(prop.Name, out var id))
            {
                var record = JsonTool.FromJson<MapTable>(prop.Value.GetRawText(), mapInfo);
                if (record != null) map[id] = record;
            }
        }
        Maps = map;
    }

    private static void LoadModelInfo()
    {
        if (!GetTable(2, "modelInfo", out var table)) return;
        var infos = new Dictionary<long, ModelInfoTable>();
        var actions = new Dictionary<int, List<int>>();
        var options = new JsonSerializerOptions { TypeInfoResolver = new ConfigTypeInfoResolver() };
        var info = (JsonTypeInfo<ModelInfoTable>)options.GetTypeInfo(typeof(ModelInfoTable))!;
        foreach (var modelProp in table.EnumerateObject())
        {
            if (!int.TryParse(modelProp.Name, out var model)) continue;
            var al = new List<int>();
            actions[model] = al;
            foreach (var actionProp in modelProp.Value.EnumerateObject())
            {
                if (!int.TryParse(actionProp.Name, out var action)) continue;
                var record = JsonTool.FromJson<ModelInfoTable>(actionProp.Value.GetRawText(), info);
                if (record != null)
                {
                    infos[(long)model * 1000 + action] = record;
                    al.Add(action);
                }
            }
        }
        ModelInfos = infos;
        ModelActions = actions;
    }
}
