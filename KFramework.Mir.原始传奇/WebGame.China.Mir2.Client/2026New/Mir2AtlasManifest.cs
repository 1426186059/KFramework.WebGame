using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;

/// <summary>
/// 原始传奇（移动版）资源总索引 manifest 解析：allmanifest{GameVersion}.json。
/// 顶层是「逻辑路径 key → { v: 文件哈希, s: 体积(字节) }」的映射；图集 / UI / 配置都登记在这里。
/// 本类只负责解析 + 把 key 解析成实际下载相对路径；取数仍走 Mir2OriginResource（base URL 可切 CDN/本地镜像）。
/// 与线上 main.min.js 的 manifest 处理一致：CDN 真文件名 = resource/{v前2}/{v}_{s}.{ext}。
/// </summary>
public sealed class Mir2AtlasManifest
{
    public Dictionary<string, Entry> Entries { get; } = new(StringComparer.OrdinalIgnoreCase);

    public sealed class Entry
    {
        public string Hash = "";   // 文件哈希（CDN 真实文件名前缀）
        public long Size;          // 体积（字节），同时用于拼 CDN 文件名 {hash}_{size}.{ext}
        public string Ext = "";    // 扩展名（含点，如 .png），由 key 推导
    }

    /// <summary>从 allmanifest 字节解析（UTF-8 JSON 对象，每项是 { v, s }）。</summary>
    public static Mir2AtlasManifest Parse(byte[] json)
    {
        var m = new Mir2AtlasManifest();
        var node = JsonNode.Parse(System.Text.Encoding.UTF8.GetString(json));
        if (node is JsonObject obj)
        {
            foreach (var kv in obj)
            {
                if (kv.Value is JsonObject o &&
                    o["v"] is JsonValue vv && vv.TryGetValue<string>(out var hash) &&
                    o["s"] is JsonValue sv && sv.TryGetValue<long>(out var size))
                {
                    m.Entries[kv.Key] = new Entry
                    {
                        Hash = hash,
                        Size = size,
                        Ext = Path.GetExtension(kv.Key)
                    };
                }
            }
        }
        return m;
    }

    public bool TryGet(string key, out Entry e) => Entries.TryGetValue(key, out e);

    /// <summary>把 key 解析成实际下载相对路径。
    /// • 本地镜像（KeyPath 模式）：直接用 key 原路径（镜像按 manifest key 落盘）。
    /// • CDN：resource/{hash前2}/{hash}_{size}.{ext}（哈希布局，与线上一致）。
    /// key 不在清单里时回退到 key 本身（容错）。</summary>
    public string ResolvePath(string key, bool localMirrorUsesKeyPath)
    {
        if (!localMirrorUsesKeyPath && Entries.TryGetValue(key, out var e) && e.Hash.Length >= 2)
            return $"resource/{e.Hash.Substring(0, 2)}/{e.Hash}_{e.Size}{e.Ext}";
        return key;
    }
}
