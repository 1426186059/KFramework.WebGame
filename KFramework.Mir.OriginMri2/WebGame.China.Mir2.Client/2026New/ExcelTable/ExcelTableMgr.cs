using KFramework.MonoGame;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

public static partial class ExcelTableMgr
{
    // ── 预加载的 config 分片 ──
    // 0config / 2config：直接整文件反序列化为 DTO，避免逐条二次解析
    private static OriginMir2Config0Table _config0;
    private static OriginMir2Config2Table _config2;
    // 其他分片：兜底缓存为 JsonElement（供 GetTable 定位特定表）
    private static readonly Dictionary<int, JsonElement> _shards = new();

    // ── 已加载的表（InitializeAsync 后填充）──
    public static Dictionary<int, MapTable> Maps { get; private set; } = new();
    public static Dictionary<long, ModelInfoTable> ModelInfos { get; private set; } = new();
    public static Dictionary<int, List<int>> ModelActions { get; private set; } = new();
    public static bool IsReady { get; private set; }

    /// <summary>
    /// 1) 预加载所需 config 分片（0config=map，2config=modelInfo 等），整文件反序列化为 DTO；
    /// 2) 再从 DTO 里取出特定表（map、modelInfo）。分片只下载一次。
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
            // 2) 从 DTO 加载特定表
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
            if (shard == 0)
            {
                if (_config0 != null) continue;
                PrintTool.Log("[ExcelTableMgr] PreloadShardsAsync 0000");
                byte[] bytes = await Mir2OriginResource.GetConfigShardBytesAsync(shard, ct).ConfigureAwait(false);
                PrintTool.Log("[ExcelTableMgr] PreloadShardsAsync 11111");
                if (bytes == null || bytes.Length == 0) continue;
                var options = new JsonSerializerOptions { TypeInfoResolver = new ConfigTypeInfoResolver() };
                var info = (JsonTypeInfo<OriginMir2Config0Table>)options.GetTypeInfo(typeof(OriginMir2Config0Table))!;
                _config0 = JsonTool.FromJson<OriginMir2Config0Table>(System.Text.Encoding.UTF8.GetString(bytes), info);
            }
            else if (shard == 2)
            {
                if (_config2 != null) continue;
                PrintTool.Log("[ExcelTableMgr] PreloadShardsAsync 0000");
                byte[] bytes = await Mir2OriginResource.GetConfigShardBytesAsync(shard, ct).ConfigureAwait(false);
                PrintTool.Log("[ExcelTableMgr] PreloadShardsAsync 11111");
                if (bytes == null || bytes.Length == 0) continue;
                var options = new JsonSerializerOptions { TypeInfoResolver = new ConfigTypeInfoResolver() };
                var info = (JsonTypeInfo<OriginMir2Config2Table>)options.GetTypeInfo(typeof(OriginMir2Config2Table))!;
                _config2 = JsonTool.FromJson<OriginMir2Config2Table>(System.Text.Encoding.UTF8.GetString(bytes), info);
            }
            else
            {
                if (_shards.ContainsKey(shard)) continue;
                byte[] bytes = await Mir2OriginResource.GetConfigShardBytesAsync(shard, ct).ConfigureAwait(false);
                if (bytes == null || bytes.Length == 0) continue;
                _shards[shard] = JsonElement.Parse(bytes);
            }
        }
    }

    // 在已缓存的分片里取名为 tableName 的顶层表（用于未显式建 DTO 的分片）
    private static bool GetTable(int shard, string tableName, out JsonElement table)
    {
        if (_shards.TryGetValue(shard, out var doc) && doc.TryGetProperty(tableName, out table))
            return true;
        table = default;
        return false;
    }

    private static void LoadMaps()
    {
        if (_config0?.map == null) return;
        var map = new Dictionary<int, MapTable>();
        foreach (var kv in _config0.map)
        {
            if (kv.Value != null) map[kv.Key] = kv.Value;
        }
        Maps = map;
    }

    private static void LoadModelInfo()
    {
        if (_config2?.modelInfo == null) return;
        var infos = new Dictionary<long, ModelInfoTable>();
        var actions = new Dictionary<int, List<int>>();
        foreach (var mkv in _config2.modelInfo)
        {
            int model = mkv.Key;
            var al = new List<int>();
            actions[model] = al;
            foreach (var akv in mkv.Value)
            {
                int action = akv.Key;
                if (akv.Value != null)
                {
                    infos[(long)model * 1000 + action] = akv.Value;
                    al.Add(action);
                }
            }
        }
        ModelInfos = infos;
        ModelActions = actions;
    }
}
