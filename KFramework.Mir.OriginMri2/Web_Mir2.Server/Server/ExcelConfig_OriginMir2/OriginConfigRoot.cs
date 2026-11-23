// 原始传奇 0config~5config 六个分片根类的公共基类。
// 作用：分片里已建好 DTO 的表走强类型属性；其余表通过 [JsonExtensionData] 原样收进 ExtraTables（表 → JsonElement），
// 不丢数据。以后要给某张表建 DTO 时，只需在对应根类加一个强类型属性，该表即自动从 ExtraTables 转为强类型。
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Server.ExcelConfig
{
public class OriginConfigRoot
{
    /// <summary>本分片中未在根类显式声明的所有顶层表（表名 → 原始 JsonElement）。</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement> ExtraTables { get; set; } = new();
}
}
