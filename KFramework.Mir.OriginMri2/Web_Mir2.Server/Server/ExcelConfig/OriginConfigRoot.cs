// 原始传奇 0config~5config 六个分片根类的公共基类。
// 作用：分片里已建好 DTO 的表走强类型属性；其余大量未用的表通过 [JsonExtensionData]
// 原样收进 ExtraTables（表 → JsonElement），不丢数据、不影响加载，以后要用某张表时
// 只要在对应根类里加一个具体 DTO 属性，该表就自动从 ExtraTables 转为强类型，加载代码不用改。
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Server.MirDatabase
{
    public class OriginConfigRoot
    {
        /// <summary>本分片中未在根类显式声明的所有顶层表（表名 → 原始 JsonElement）。</summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement> ExtraTables { get; set; } = new();
    }
}
