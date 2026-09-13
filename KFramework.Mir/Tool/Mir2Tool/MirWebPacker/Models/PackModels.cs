namespace MirWebPacker.Models;

/// <summary>
/// .web.lib 包内的单个资源条目。
/// </summary>
/// <param name="Path">包内相对路径，如 textures/floor/t1.webp</param>
/// <param name="Type">MIME 类型，前端据此构造 Blob</param>
/// <param name="Bytes">字节长度</param>
/// <param name="Crc">Zip CRC32（十六进制小写），用于完整性校验</param>
/// <param name="Hash">内容哈希，可选；去重/缓存命中用，格式如 sha1:xxxx</param>
public sealed record WebLibEntry(
    string Path,
    string Type,
    long Bytes,
    string Crc,
    string? Hash = null);

/// <summary>
/// .web.lib 包级清单，加载器只需先读它即可获知全部资源。
/// </summary>
/// <param name="Format">固定 "web.lib"</param>
/// <param name="Version">清单结构版本</param>
/// <param name="Kind">资源种类，如 map / library / shared</param>
/// <param name="Name">逻辑名称，如 3-1</param>
/// <param name="Entries">资源索引</param>
public sealed record WebLibManifest(
    string Format,
    int Version,
    string Kind,
    string Name,
    IReadOnlyList<WebLibEntry> Entries);

// ============ 服务侧：目录浏览（与 MapExtract 同源） ============

public sealed class FsEntry
{
    public string Name { get; set; } = "";
    public string FullPath { get; set; } = "";
}

public sealed class FsListDto
{
    public string Path { get; set; } = "";
    public string Parent { get; set; } = "";
    public List<FsEntry> Directories { get; set; } = new();
    public List<FsEntry> Files { get; set; } = new();
    public List<FsEntry> Drives { get; set; } = new();
}

// ============ 服务侧：打包请求 / 结果 ============

/// <summary>
/// 一次打包请求：把 root 下每个子目录分别打成 .web.lib（多级目录分别打包）。
/// 若 root 下没有子目录，则把 root 本身打成一个包。
/// </summary>
public sealed class PackRootRequest
{
    /// <summary>资源根目录（绝对路径，工具所在机器上的目录）</summary>
    public string? Root { get; set; }

    /// <summary>产物输出目录；缺省为 {root}/_packages</summary>
    public string? OutputDir { get; set; }

    /// <summary>资源种类，写入每个包与总清单，如 map / library / shared</summary>
    public string? Kind { get; set; } = "map";

    /// <summary>WebP 无损编码（默认 true）：像素 100% 还原</summary>
    public bool Lossless { get; set; } = true;

    /// <summary>WebP 质量/压缩力度 1~100</summary>
    public int Quality { get; set; } = 90;
}

/// <summary>单个资源包（带内容哈希，文件名已包含短哈希用于精准热更）。</summary>
public sealed record PackageInfo(
    string Name,
    string File,
    string Url,
    long Size,
    string Hash,
    int Entries,
    string? Detail);

/// <summary>打包接口的返回：列出所有生成的包 + 总清单内容。</summary>
public sealed record PackRootResult(
    bool Ok,
    string Message,
    string Root,
    string OutputDir,
    int PackageCount,
    long TotalBytes,
    IReadOnlyList<PackageInfo> Packages,
    string ManifestFile,
    string ManifestJson);

// ============ 总清单（部署到 CDN，游戏启动时拉取） ============

/// <summary>
/// 资源包集合的总清单（version.manifest）。
/// 游戏启动时先拉取它，再按每个包的 Hash/文件名判断是否需要热更。
/// </summary>
/// <param name="Format">固定 "web.lib.bundle"</param>
/// <param name="Version">清单结构版本</param>
/// <param name="Kind">资源种类，如 map / library / shared</param>
/// <param name="Root">打包时的源根目录</param>
/// <param name="GeneratedAt">生成时间（UTC ISO8601）</param>
/// <param name="Packages">所有资源包索引</param>
public sealed record BundleManifest(
    string Format,
    int Version,
    string Kind,
    string Root,
    string GeneratedAt,
    IReadOnlyList<BundlePackage> Packages);

/// <summary>
/// 总清单里的一个资源包条目。File 已包含内容短哈希（如 3-1.a1b2c3d4.web.lib）。
/// 游戏端缓存上次下载的 File/Hash，二者未变即跳过下载，实现精准热更。
/// </summary>
/// <param name="Name">逻辑名，如 3-1</param>
/// <param name="File">包文件名（含短哈希），如 3-1.a1b2c3d4.web.lib</param>
/// <param name="Size">字节长度</param>
/// <param name="Hash">完整内容哈希，如 sha1:xxxx</param>
/// <param name="Entries">包内资源条目数</param>
public sealed record BundlePackage(
    string Name,
    string File,
    long Size,
    string Hash,
    int Entries);
