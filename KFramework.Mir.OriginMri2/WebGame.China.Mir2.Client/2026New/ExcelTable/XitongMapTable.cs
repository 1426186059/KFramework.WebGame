// 配置表 "xitongMap" —— 由 Mir2ReverseWeb 提取（已手动校正）
// 结构：xitongMap[系统分类id][门槛id] = 一条系统/传送记录。
// 原提取器误把“门槛id”当作字段名，生成了 54 个无意义字段；此处改为记录的真实字段。
// 注：deliver（传送点id）仅 type>=2 的记录存在，type=1 时反序列化后为 0。
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;


/// <summary>配置表 "xitongMap" 的单条记录（系统/传送点）</summary>
public class XitongMapTable
{
    // 等级/门槛
    [JsonPropertyName("level")]
    public int level { get; set; } = default;

    // 类型（1=养成系统，2+=转生/秘境地图）
    [JsonPropertyName("type")]
    public int type { get; set; } = default;

    // 名称
    [JsonPropertyName("name")]
    public string name { get; set; } = "";

    // 传送点id（仅 type>=2 有）
    [JsonPropertyName("deliver")]
    public int deliver { get; set; } = default;

    // 序号
    [JsonPropertyName("index")]
    public int index { get; set; } = default;

    // 进入条件（代号，可用 | 分隔）
    [JsonPropertyName("condition")]
    public string condition { get; set; } = "";
}
