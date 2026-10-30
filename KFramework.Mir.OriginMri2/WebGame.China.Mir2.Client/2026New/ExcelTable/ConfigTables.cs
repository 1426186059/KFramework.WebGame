using System.Text;
using System.Text.Json;

namespace Client.MirObjects
{
    /// <summary>
    /// 原始传奇 配置表（0config~5config 分片）的流式加载器。
    /// 游戏运行时实际加载 6 个分片，顶层每个键即一张配置表。整份分片很大（0config 6.7MB / 175 张表），
    /// 故不整体 DOM 化，而是只抽取指定表：FindTable 用 Utf8JsonReader 前向扫描，命中表后一次性 ParseValue，
    /// 其余表全部 Skip，零中间字符串，避免首次进图卡死（整体 JsonDocument.Parse 实测 15s+）。
    /// </summary>
    public static class ConfigTables
    {
        // 数字字段兼容 number / 数字字符串
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };

        static ConfigTables()
        {
            Options.Converters.Add(new FlexIntJsonConverter());
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
                dict[kv.Key] = JsonSerializer.Deserialize<MapTable>(kv.Value, Options);
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
                    if (int.TryParse(p.Name, out var mid)) inner[mid] = JsonSerializer.Deserialize<MinimapTable>(p.Value, Options);
                dict[kv.Key] = inner;
            }
            return dict;
        }

        /// <summary>
        /// 通用「按表名加载」：自动从 0~5 分片里定位 tableName（顶层键），反序列化为 Dictionary&lt;int, T&gt;（key=条目id）。
        /// 与 JS 端 cm 的合并语义一致——若同一张表出现在多个分片，后者整表覆盖前者。找不到则返回空字典（不抛异常）。
        /// 用法：var itemTable = await ConfigTables.LoadTableAsync&lt;ItemTable&gt;("item");
        /// 注：本泛型针对「对象型」表（{id:{...条目...}}）；数组型/标量型表请用 LoadRawTableAsync 自行处理。
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
                    dict[kv.Key] = JsonSerializer.Deserialize<T>(kv.Value, Options)!;
                merged = dict; // 后分片覆盖前分片（整表替换，对齐 JS 的 this[r]=i[r]）
            }
            return merged ?? new Dictionary<int, T>();
        }

        /// <summary>
        /// 在分片 JSON 里前向扫描，定位名为 tableName 的顶层属性，将其值整体读成 JsonElement 返回；
        /// 找不到返回 default(ValueKind=Undefined)。JsonElement 引用底层字节缓冲，调用方需保持其存活（本类已持有）。
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
}
