using System.Text.Json;
using System.Text.Json.Serialization;

namespace KFramework.Content.Pipeline;

/// <summary>清单中的一个资源条目。</summary>
public sealed class ManifestAsset
{
    /// <summary>资源名，例如 "sprites/player"。</summary>
    public string Name { get; set; } = "";

    /// <summary>类型：texture / json / text。</summary>
    public string Type { get; set; } = "";

    /// <summary>所属图集页索引，非纹理资源为 -1。</summary>
    public int Page { get; set; } = -1;

    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>原始未压缩字节数。</summary>
    public long Size { get; set; }
}

/// <summary>清单中的一个 pak 文件。</summary>
public sealed class ManifestPak
{
    public string File { get; set; } = "";
    public long Size { get; set; }
    public uint Checksum { get; set; }
}

/// <summary>
/// 发布内容的清单。运行时先读它，再按名字定位到 pak 中的具体位置，
/// 因此新增/修改资源只需要更新 manifest 与对应的 pak。
/// </summary>
public sealed class ContentManifest
{
    public int Version { get; set; } = 1;

    public string GeneratedAt { get; set; } = "";

    public string Root { get; set; } = "";

    public List<ManifestPak> Paks { get; set; } = new();

    public List<ManifestAsset> Assets { get; set; } = new();

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    public static ContentManifest FromJson(string json)
        => JsonSerializer.Deserialize<ContentManifest>(json, Options)
           ?? throw new InvalidDataException("无法解析 manifest.json。");

    public ManifestAsset? Find(string name)
    {
        string normalized = Pak.PakFormat.NormalizeName(name);
        foreach (ManifestAsset asset in Assets)
            if (asset.Name == normalized) return asset;
        return null;
    }
}
