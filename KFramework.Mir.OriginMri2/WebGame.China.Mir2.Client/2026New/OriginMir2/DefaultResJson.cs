using System.Collections.Generic;
using System.Text.Json.Serialization;

// =====================================================================================
// 白鹭资源清单 default.res.json 的 DTO（UI 共享图集的别名 / 分组配置）。
//
// 结构：
// {
//   "resources": [ { "name": "...", "url": "...", "type": "...", "subkeys": "a,b,c" }, ... ],
//   "groups":     [ { "name": "...", "keys": "x,y,z" }, ... ]
// }
//
// 风格参考 EgretAtlasJson：纯 POCO + 源生成反序列化，不含解析后的别名/分组映射逻辑
// （那些运行时字典仍在 Mir2AtlasLibrary 里维护）。
// =====================================================================================

/// <summary>default.res.json 根对象。</summary>
public sealed class DefaultResJson
{
    [JsonPropertyName("resources")] public List<DefaultResItemJson> Resources { get; set; } = new();
    [JsonPropertyName("groups")] public List<DefaultResGroupJson> Groups { get; set; } = new();

    public static DefaultResJson Deserialize(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return KFramework.MonoGame.JsonTool.FromJson(json, AppJsonContext.Default.DefaultResJson); }
        catch (System.Text.Json.JsonException) { return null; }
    }
}

/// <summary>resources 数组中的单项。</summary>
public sealed class DefaultResItemJson
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("url")] public string Url { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("subkeys")] public string Subkeys { get; set; } = "";
}

/// <summary>groups 数组中的单项。</summary>
public sealed class DefaultResGroupJson
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("keys")] public string Keys { get; set; } = "";
}
