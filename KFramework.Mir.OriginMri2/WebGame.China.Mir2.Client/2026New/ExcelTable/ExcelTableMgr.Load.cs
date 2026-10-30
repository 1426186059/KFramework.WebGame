// ExcelTableMgr 的加载实现（partial）：对应 JS 端 cm 的分片抽取。
// 不整体 DOM 化，仅用 Utf8JsonReader 前向扫描抽取指定表，避免整份分片 Parse 卡死（实测 15s+）。
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Client.MirObjects;   // Mir2OriginResource

public static partial class ExcelTableMgr
{
    // 数字字段兼容 number / 数字字符串
    private static readonly JsonSerializerOptions _options = new JsonSerializerOptions
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    static ExcelTableMgr()
    {
        _options.Converters.Add(new FlexIntJsonConverter());
    }

    /// <summary>
    /// 抽取某分片里某张表（顶层键），返回 {id: 原始 JSON 元素}（id 为字符串键解析成的 int）。
    /// 找不到该表时返回空字典（不会抛异常）。
    /// </summary>
    public static async Task<Dictionary<int, JsonElement>> LoadRawTableAsync(int shard, string tableName, CancellationToken ct = default)
    {
        byte[] json = await Mir2OriginResource.GetConfigShardBytesAsync(shard, ct).ConfigureAwait(false);
        var result = new Dictionary<int, JsonElement>();
        var table = FindTable(json, tableName);
        if (table.ValueKind == JsonValueKind.Undefined) return result;
        foreach (var prop in table.EnumerateObject())
            if (int.TryParse(prop.Name, out var id)) result[id] = prop.Value;
        return result;
    }

    /// <summary>加载 0config 的 map 表，反序列化为 MapTable 字典（key=地图id）。</summary>
    public static async Task<Dictionary<int, MapTable>> LoadMapConfigAsync(CancellationToken ct = default)
    {
        var raw = await LoadRawTableAsync(0, "map", ct).ConfigureAwait(false);
        var dict = new Dictionary<int, MapTable>(raw.Count);
        foreach (var kv in raw)
            dict[kv.Key] = JsonSerializer.Deserialize<MapTable>(kv.Value, _options);
        return dict;
    }

    /// <summary>加载 4config 的 minimap 表，反序列化为 {地图id: {标记id: MinimapTable}}。</summary>
    public static async Task<Dictionary<int, Dictionary<int, MinimapTable>>> LoadMinimapConfigAsync(CancellationToken ct = default)
    {
        var raw = await LoadRawTableAsync(4, "minimap", ct).ConfigureAwait(false);
        var dict = new Dictionary<int, Dictionary<int, MinimapTable>>();
        foreach (var kv in raw)
        {
            var inner = new Dictionary<int, MinimapTable>();
            foreach (var p in kv.Value.EnumerateObject())
                if (int.TryParse(p.Name, out var mid)) inner[mid] = JsonSerializer.Deserialize<MinimapTable>(p.Value, _options);
            dict[kv.Key] = inner;
        }
        return dict;
    }

    /// <summary>
    /// 通用「对象型」表按表名加载：自动从 0~5 分片定位 tableName（顶层键），反序列化为 Dictionary&lt;int, T&gt;（key=条目id）。
    /// 与 JS 端 cm 的合并语义一致——若同一张表出现在多个分片，后者整表覆盖前者。找不到返回空字典。
    /// </summary>
    public static async Task<Dictionary<int, T>> LoadTableAsync<T>(string tableName, CancellationToken ct = default) where T : class
    {
        Dictionary<int, T>? merged = null;
        for (int shard = 0; shard < 6; shard++)
        {
            var raw = await LoadRawTableAsync(shard, tableName, ct).ConfigureAwait(false);
            if (raw.Count == 0) continue;
            var dict = new Dictionary<int, T>(raw.Count);
            foreach (var kv in raw)
                dict[kv.Key] = JsonSerializer.Deserialize<T>(kv.Value, _options)!;
            merged = dict; // 后分片覆盖前分片（整表替换，对齐 JS 的 this[r]=i[r]）
        }
        return merged ?? new Dictionary<int, T>();
    }

    /// <summary>
    /// 通用「嵌套表」按表名加载：自动从 0~5 分片定位 tableName（顶层键），其值为 {外层id: {内层id: 记录}}，
    /// 反序列化为 Dictionary&lt;int, Dictionary&lt;int, T&gt;&gt;。找不到返回空字典。
    /// 用于 minimap / sheepmap / xitongMap 这类「两层对象」表。
    /// </summary>
    public static async Task<Dictionary<int, Dictionary<int, T>>> LoadNestedTableAsync<T>(string tableName, CancellationToken ct = default) where T : class
    {
        Dictionary<int, Dictionary<int, T>>? merged = null;
        for (int shard = 0; shard < 6; shard++)
        {
            var raw = await LoadRawTableAsync(shard, tableName, ct).ConfigureAwait(false);
            if (raw.Count == 0) continue;
            var dict = new Dictionary<int, Dictionary<int, T>>(raw.Count);
            foreach (var kv in raw)
            {
                var inner = new Dictionary<int, T>();
                foreach (var p in kv.Value.EnumerateObject())
                    if (int.TryParse(p.Name, out var k)) inner[k] = JsonSerializer.Deserialize<T>(p.Value, _options)!;
                dict[kv.Key] = inner;
            }
            merged = dict; // 后分片覆盖前分片
        }
        return merged ?? new Dictionary<int, Dictionary<int, T>>();
    }

    /// <summary>
    /// 在分片 JSON 里前向扫描，定位名为 tableName 的顶层属性，将其值整体读成 JsonElement 返回；
    /// 找不到返回 default(ValueKind=Undefined)。
    /// </summary>
    private static JsonElement FindTable(byte[] json, string tableName)
    {
        var opts = new JsonReaderOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        var reader = new Utf8JsonReader(json, opts);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) return default;
        var nameUtf8 = Encoding.UTF8.GetBytes(tableName);
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) return default;
            if (reader.TokenType != JsonTokenType.PropertyName) continue;
            if (reader.ValueTextEquals(nameUtf8))
            {
                reader.Read(); // 定位到表值（对象）
                return JsonElement.ParseValue(ref reader);
            }
            reader.Skip();
        }
        return default;
    }
}
