using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

// =====================================================================================
// 原始传奇（移动版）资源总索引 manifest（allmanifest{GameVersion}.json）的 DTO。
//
// 根对象即字典：逻辑路径 key → { v: 文件哈希, s: 体积(字节) }。图集 / UI / 配置都登记在这里。
// 与线上 main.min.js 的 manifest 处理一致：CDN 真文件名 = resource/{v前2}/{v}_{s}.{ext}。
//
// 风格参考 EgretAtlasJson：纯 POCO + 源生成反序列化，不掺运行时逻辑。
// 用源生成（AppJsonContext）而非反射式 JsonSerializer（项目禁用反射序列化）。
// =====================================================================================

/// <summary>manifest 中单条记录：{ v: 文件哈希, s: 体积(字节) }。</summary>
public sealed class ManifestEntryJson
{
    [JsonPropertyName("v")] public string Hash { get; set; } = "";   // 文件哈希（CDN 真实文件名前缀）
    [JsonPropertyName("s")] public long Size { get; set; }          // 体积（字节），用于拼 CDN 文件名 {hash}_{size}.{ext}
}

/// <summary>原始传奇资源总索引 manifest 的强类型视图。</summary>
public sealed class Mir2AtlasManifest
{
    /// <summary>逻辑路径 key → 该资源在 manifest 中的记录。</summary>
    public Dictionary<string, ManifestEntryJson> Entries { get; set; }

    /// <summary>从 allmanifest 字节解析（兼容旧调用点，传 byte[]）。</summary>
    public static Mir2AtlasManifest Parse(byte[] json)
    {
        if (json == null || json.Length == 0) return null;
        return Deserialize(System.Text.Encoding.UTF8.GetString(json));
    }

    public static Mir2AtlasManifest Deserialize(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            // 根即字典：string -> ManifestEntryJson；走源生成上下文，AOT 友好。
            var entries = KFramework.MonoGame.JsonTool.FromJson(
                json, AppJsonContext.Default.ManifestEntryDictionary);
            return new Mir2AtlasManifest { Entries = entries };
        }
        catch (JsonException) { return null; }
    }

    public bool TryGet(string key, out ManifestEntryJson e)
    {
        if (Entries == null) { e = null; return false; }
        return Entries.TryGetValue(key, out e);
    }

    /// <summary>把 key 解析成实际下载相对路径。
    /// • 本地镜像（KeyPath 模式）：直接用 key 原路径（镜像按 manifest key 落盘）。
    /// • CDN：resource/{hash前2}/{hash}_{size}.{ext}（哈希布局，与线上一致）。
    /// key 不在清单里时回退到 key 本身（容错）。</summary>
    public string ResolvePath(string key, bool localMirrorUsesKeyPath)
    {
        if (!localMirrorUsesKeyPath && Entries != null &&
            Entries.TryGetValue(key, out var e) && e.Hash.Length >= 2)
            return $"resource/{e.Hash.Substring(0, 2)}/{e.Hash}_{e.Size}{Path.GetExtension(key)}";
        return key;
    }
}
