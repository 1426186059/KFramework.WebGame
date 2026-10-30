using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

public static partial class ExcelTableMgr
{
    // ── 已加载的表（InitializeAsync 后填充）──
    public static Dictionary<int, MapTable> Maps { get; private set; } = new();
    public static bool IsReady { get; private set; }

    /// <summary>
    /// 加载 0config 分片里的 "map" 表（key=地图id），反序列化为 Dictionary&lt;int, MapTable&gt;。
    /// 全程走源生成 + JsonTool，AOT 安全。失败仅记录日志，不抛出——配置表不是进游戏的硬前置。
    /// </summary>
    public static async Task InitializeAsync(CancellationToken ct = default)
    {
        if (IsReady) return;
        try
        {
            byte[] json = await Mir2OriginResource.GetConfigShardBytesAsync(0, ct).ConfigureAwait(false);
            var table = FindTable(json, "map");
            var map = new Dictionary<int, MapTable>();
            if (table.ValueKind != JsonValueKind.Undefined)
            {
                // 源生成上下文 + FlexInt 容错（数字/数字字符串兼容），通过 JsonTool 反序列化
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
            }
            Maps = map;
            IsReady = true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ExcelTableMgr] 配置表加载失败：{ex}");
        }
    }
}
