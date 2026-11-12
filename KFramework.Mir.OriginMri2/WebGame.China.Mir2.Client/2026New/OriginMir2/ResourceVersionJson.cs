using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

// =====================================================================================
// 原始传奇资源版本表 resourceVersion.json 的 DTO。
//
// 根对象即字典：资源名（如 "model/123"、"assets/gameui4/..."）→ 版本号字符串。
// 用于给 CDN 资源 URL 拼 ?v= 版本段（命中 CDN 缓存、防旧资源）。
//
// 风格参考 EgretAtlasJson / Mir2AtlasManifest：纯 POCO + 源生成反序列化，
// 替代原先 LoadVersionsAsync 里的 JsonDocument 手解析。
// =====================================================================================

/// <summary>resourceVersion.json 强类型视图（根即 name → version 字典）。</summary>
public sealed class ResourceVersionJson
{
    /// <summary>资源名 → 版本号；用 OrdinalIgnoreCase 比较，与原手解析行为一致。</summary>
    public Dictionary<string, string> Entries { get; set; }

    public static ResourceVersionJson Parse(byte[] json)
    {
        if (json == null || json.Length == 0) return null;
        return Deserialize(System.Text.Encoding.UTF8.GetString(json));
    }

    public static ResourceVersionJson Deserialize(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            // 用 JsonDocument 手解析（AOT/裁剪安全，不依赖反射式 JsonSerializer，
            // 也不依赖源生成对 Dictionary<string,string> 的注册——后者在本 WASM 构建里会返回 null）。
            using var doc = JsonDocument.Parse(json);
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                    dict[prop.Name] = prop.Value.GetString();
            }
            return new ResourceVersionJson { Entries = dict };
        }
        catch (JsonException) { return null; }
    }

    public bool TryGet(string key, out string version)
    {
        if (Entries == null) { version = null; return false; }
        return Entries.TryGetValue(key, out version);
    }
}
