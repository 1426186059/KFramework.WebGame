// 配置表 "sheepmap" —— 由 Mir2ReverseWeb 提取（已手动校正）
// 结构：sheepmap[布局id][点位id] = 一条网格点位记录（羊群/走格子玩法坐标）。
// 原提取器误把“点位id”当作字段名，生成了 180 个无意义字段；此处改为点位记录的真实字段。
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;


/// <summary>配置表 "sheepmap" 的单条记录（网格点位）</summary>
public class SheepmapTable
{
    // 点位id
    [JsonPropertyName("id")]
    public int id { get; set; } = default;

    // 布局/关卡
    [JsonPropertyName("level")]
    public int level { get; set; } = default;

    // 层
    [JsonPropertyName("z")]
    public int z { get; set; } = default;

    // X坐标（可含小数）
    [JsonPropertyName("x")]
    public double x { get; set; } = default;

    // Y坐标（可含小数）
    [JsonPropertyName("y")]
    public double y { get; set; } = default;
}
