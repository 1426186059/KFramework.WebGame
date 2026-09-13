using System.IO;
using System.Linq;
using System.Text.Json;

namespace WebLib;

/// <summary>
/// 所有 .web.lib 的总清单（对齐 Unity <c>AssetBundleManifest</c>），落盘为 version.manifest（JSON）。
/// 记录每个包的 逻辑名 / 文件名(含短哈希) / 大小 / 完整哈希 / 依赖，并提供 Unity 风格的查询方法。
/// </summary>
public sealed class AssetBundleManifest
{
    /// <summary>清单格式标识，固定 "web.lib.bundle"</summary>
    public string Format { get; }

    /// <summary>清单结构版本</summary>
    public int Version { get; }

    /// <summary>内容哈希算法名（小写，无前缀），如 md5</summary>
    public string Hash { get; }

    /// <summary>生成时间（ISO8601 UTC）</summary>
    public string CreatedAt { get; }

    /// <summary>全部包条目</summary>
    public IReadOnlyList<BundlePackage> Packages { get; }

    public AssetBundleManifest(
        string format,
        int version,
        string hash,
        string createdAt,
        IReadOnlyList<BundlePackage> packages)
    {
        Format = format;
        Version = version;
        Hash = hash;
        CreatedAt = createdAt;
        Packages = packages;
    }

    private static readonly JsonSerializerOptions s_opts = new() { WriteIndented = true };

    /// <summary>序列化为 JSON 字符串（version.manifest 的内容）。</summary>
    public string Serialize() => JsonSerializer.Serialize(this, s_opts);

    /// <summary>从流解析总清单（version.manifest）。</summary>
    public static AssetBundleManifest Parse(Stream stream) =>
        JsonSerializer.Deserialize<AssetBundleManifest>(stream)
            ?? throw new InvalidDataException("version.manifest 解析失败");

    /// <summary>从 JSON 字符串解析总清单（version.manifest）。</summary>
    public static AssetBundleManifest Parse(string json) =>
        JsonSerializer.Deserialize<AssetBundleManifest>(json)
            ?? throw new InvalidDataException("version.manifest 解析失败");

    // ==================== Unity 风格 API ====================

    /// <summary>所有包的逻辑名（对应 Unity GetAllAssetBundles）。</summary>
    public string[] GetAllAssetBundles() =>
        Packages.Select(p => p.Name).ToArray();

    /// <summary>取某包的完整内容哈希（对应 Unity GetAssetBundleHash）。</summary>
    public string? GetAssetBundleHash(string bundleName) =>
        Packages.FirstOrDefault(p => string.Equals(p.Name, bundleName, StringComparison.OrdinalIgnoreCase))?.Hash;

    /// <summary>取某包直接依赖的其它包逻辑名（对应 Unity GetDirectDependencies）。</summary>
    public string[] GetDirectDependencies(string bundleName)
    {
        var p = Packages.FirstOrDefault(x => string.Equals(x.Name, bundleName, StringComparison.OrdinalIgnoreCase));
        return p?.Dependencies?.ToArray() ?? Array.Empty<string>();
    }

    /// <summary>取某包全部（传递）依赖的其它包逻辑名（对应 Unity GetAllDependencies）。</summary>
    public string[] GetAllDependencies(string bundleName)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Visit(string name)
        {
            var p = Packages.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
            if (p?.Dependencies == null) return;
            foreach (var d in p.Dependencies)
                if (seen.Add(d)) { result.Add(d); Visit(d); }
        }
        Visit(bundleName);
        return result.ToArray();
    }
}
