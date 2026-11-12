using System;
using System.Collections.Generic;
using System.Text.Json;

// =====================================================================================
// 原始传奇资源版本表 resourceVersion.json 的解析。
//
// 根对象即扁平字典：资源名（如 "model/1827"、"assets/gameui4/..."）→ 版本号字符串。
// 用于给 CDN 资源 URL 拼 ?v= 版本段（命中 CDN 缓存、防旧资源）。
//
// 关键：清单导出异常会混入非字符串条目（如 "version": -1，数字值），用强类型
// Dictionary<string,string> 源生成反序列化会整段抛异常返回 null。因此这里用
// JsonDocument 按需解析——查哪个 key 就解析哪个，遇到非字符串值直接跳过，
// 既不会整段失败，也无需把整份大清单（60 万字节）预先反序列化成字典。
// JsonDocument 是读取器 API，在 WASM 裁剪/AOT 下安全（不走反射式 JsonSerializer）。
// =====================================================================================

/// <summary>resourceVersion.json 按需视图（根即 name → version 字典）。</summary>
public sealed class ResourceVersionJson
{
    private readonly JsonDocument _doc;
    // 惰性构建：第一次 TryGet 时才遍历根对象，只收字符串值的条目（跳过 version:-1 这类）。
    private Dictionary<string, string> _dict;

    private ResourceVersionJson(JsonDocument doc) { _doc = doc; }

    public static ResourceVersionJson Parse(byte[] json)
    {
        if (json == null || json.Length == 0) return null;
        try
        {
            return new ResourceVersionJson(JsonDocument.Parse(System.Text.Encoding.UTF8.GetString(json)));
        }
        catch (JsonException) { return null; }
    }

    private Dictionary<string, string> EnsureDict()
    {
        if (_dict != null) return _dict;
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (_doc != null && _doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in _doc.RootElement.EnumerateObject())
            {
                // 遇到哪个解析哪个：只收字符串值的条目，非字符串（如 "version": -1）跳过。
                if (prop.Value.ValueKind == JsonValueKind.String)
                    d[prop.Name] = prop.Value.GetString();
            }
        }
        _dict = d;
        return d;
    }

    public int Count => EnsureDict().Count;

    /// <summary>查资源名对应的版本号；查不到或非字符串值返回 false。</summary>
    public bool TryGet(string key, out string version)
    {
        if (_doc == null) { version = null; return false; }
        return EnsureDict().TryGetValue(key, out version);
    }
}
