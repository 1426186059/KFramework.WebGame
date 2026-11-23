// 原始传奇配置分片 1config 的根类（对应 1config10.25341.1.json）。
// 1config 只有 item 一张表（17166 条，稀疏列对象），用 OriginItemTable 显式声明整表（56 列带注释）。
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Server.ExcelConfig
{
    public class OriginMir2Config1 : OriginConfigRoot
    {
        // 物品表（17166 项，key=物品id，值=稀疏列对象 {"列号": 值}，列 0~63 共 56 列有数据）。
        // 关键列：0=id 2=名称 5=类别 9=品质框 17=外观模型 19=图标 40=属性串 42=描述 52=职业 56=性别。
        [JsonPropertyName("item")] public Dictionary<string, OriginItemTable> item { get; set; } = new();
    }
}
