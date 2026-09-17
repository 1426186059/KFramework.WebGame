using System.Text.Json;
using System.Text.Json.Serialization;

namespace KFramework.MonoGame;

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

    /// <summary>序列化为 JSON 字符串（version.manifest 的内容）。</summary>
    public string Serialize() => JsonSerializer.Serialize(this, JsonAutoGenerator.Default.AssetBundleManifest);

    /// <summary>从流解析总清单（version.manifest）。</summary>
    public static AssetBundleManifest Parse(Stream stream)
        => JsonSerializer.Deserialize(stream, JsonAutoGenerator.Default.AssetBundleManifest)
           ?? throw new InvalidDataException("version.manifest 解析失败");

    /// <summary>从 JSON 字符串解析总清单（version.manifest）。</summary>
    public static AssetBundleManifest Parse(string json)
        => JsonSerializer.Deserialize(json, JsonAutoGenerator.Default.AssetBundleManifest)
           ?? throw new InvalidDataException("version.manifest 解析失败");

    /// <summary>按逻辑名（不区分大小写）查找包条目；找不到返回 null。</summary>
    public static BundlePackage? FindPackage(IReadOnlyList<BundlePackage> packages, string name, bool strict = true)
    {
        if (strict)
        {
            foreach (var p in packages)
            {
                if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) return p;
            }
        }
        else
        {
            foreach (var p in packages)
            {
                if (p.Name.Contains(name, StringComparison.OrdinalIgnoreCase)) return p;
            }
        }
        return null;
    }

    // ==================== Unity 风格 API ====================

    /// <summary>所有包的逻辑名（对应 Unity GetAllAssetBundles）。</summary>
    public string[] GetAllAssetBundles()
        => Packages.Select(p => p.Name).ToArray();

    /// <summary>取某包的完整内容哈希（对应 Unity GetAssetBundleHash）。用于精确热更比对。</summary>
    public string? GetAssetBundleHash(string bundleName)
        => FindPackage(Packages, bundleName)?.Hash;

    /// <summary>取某包直接依赖的其它包逻辑名（对应 Unity GetDirectDependencies）。</summary>
    public string[] GetDirectDependencies(string bundleName)
    {
        var p = FindPackage(Packages, bundleName);
        return p?.Dependencies?.ToArray() ?? Array.Empty<string>();
    }

    /// <summary>取某包全部（传递）依赖的其它包逻辑名（对应 Unity GetAllDependencies）。</summary>
    public string[] GetAllDependencies(string bundleName)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Visit(string name)
        {
            var p = FindPackage(Packages, name);
            if (p?.Dependencies == null) return;
            foreach (var d in p.Dependencies)
                if (seen.Add(d)) { result.Add(d); Visit(d); }
        }
        Visit(bundleName);
        return result.ToArray();
    }
}

/// <summary>
/// 总清单里的一个资源包条目。File 已含内容短哈希（如 myres_atlas_characters.a1b2c3d4.web.lib），
/// 对齐 Unity <c>AssetBundleManifest</c> 中“包名 + 哈希”的索引概念。
/// </summary>
/// <param name="Name">逻辑名（全相对路径，含 '/'，如 myres/group/atlas）</param>
/// <param name="File">包文件名（含短哈希、扁平化）：把逻辑名的 '/' 换成 '_'，如 myres_group_atlas.a1b2c3d4.web.lib</param>
/// <param name="Size">字节长度</param>
/// <param name="Hash">完整内容哈希（小写十六进制，无算法前缀）；算法见总清单 Hash 字段</param>
/// <param name="Entries">包内资源条目数</param>
/// <param name="Dependencies">依赖的其它包逻辑名（对应 Unity GetDirectDependencies / GetAllDependencies）</param>
public sealed record BundlePackage(
    string Name,
    string File,
    long Size,
    string Hash,
    int Entries,
    IReadOnlyList<string> Dependencies = null!);
