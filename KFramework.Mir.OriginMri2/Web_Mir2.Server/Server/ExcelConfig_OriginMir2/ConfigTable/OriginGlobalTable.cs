using System.Text.Json.Serialization;

namespace Server.ExcelConfig
{
    /// <summary>0config.global 单条全局配置（任务类型名 global[9201] 用）。</summary>
    public class OriginGlobalTable
    {
        [JsonPropertyName("id")] public int id { get; set; } // 全局配置 id（9201=任务类型前缀）
        // 9201 的值形如 "1#[主线]&amp;201#[转生]&amp;..."（type#[前缀]&amp;...）
        [JsonPropertyName("value")] public string value { get; set; } = "";
    }
}
