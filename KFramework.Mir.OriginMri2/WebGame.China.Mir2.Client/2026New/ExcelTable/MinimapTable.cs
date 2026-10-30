// 配置表 "minimap" —— 由 Mir2ReverseWeb 提取（已手动校正）
// 结构：minimap[地图id][标记id] = 一条小地图标记记录。
// 原提取器误把“标记id”当作字段名，生成了 959 个无意义字段；此处改为标记记录的真实字段。
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;


/// <summary>配置表 "minimap" 的单条记录（小地图标记点）</summary>
public class MinimapTable
{
    // 标记id
    [JsonPropertyName("id")]
    public int id { get; set; } = default;

    // 所属地图id
    [JsonPropertyName("mapid")]
    public int mapid { get; set; } = default;

    // 小地图分片/图号
    [JsonPropertyName("mid")]
    public int mid { get; set; } = default;

    // 标记X坐标
    [JsonPropertyName("x")]
    public int x { get; set; } = default;

    // 标记Y坐标
    [JsonPropertyName("y")]
    public int y { get; set; } = default;

    // 标记类型
    [JsonPropertyName("type")]
    public int type { get; set; } = default;

    // 标记名称（含html颜色）
    [JsonPropertyName("name")]
    public string name { get; set; } = "";
}
