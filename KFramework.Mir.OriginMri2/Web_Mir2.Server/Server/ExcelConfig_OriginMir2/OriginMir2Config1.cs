// 原始传奇配置分片 1config 的根类（对应 1config10.25341.1.json）。
// 1config 只有 item 一张表（17166 项的稀疏数字列数组，列号无语义名），
// 语义映射已在 Tools/Mir2ItemConverter + 服务器 ItemInfoTable 完成；这里显式声明整表，
// 加载进来方便与导出的 ItemInfo.json 对比排查（item 条目保留原始 JsonElement，不建固定 DTO）。
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Server.ExcelConfig
{
    public class OriginMir2Config1 : OriginConfigRoot
    {
        // 物品表（17166 项，key=物品id，值=稀疏数字列数组 [col0..col56]）。
        // 关键列：2=名称 5=类别 17=外观模型 19=图标 22=价格 40=属性串 42=描述 52=职业。
        [JsonPropertyName("item")] public Dictionary<string, JsonElement> item { get; set; } = new();
    }
}
