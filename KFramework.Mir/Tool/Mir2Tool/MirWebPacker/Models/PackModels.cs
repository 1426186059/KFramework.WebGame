namespace MirWebPacker.Models;

// 说明：.web.lib 的打包/解包/总清单/热更逻辑都在共享库 WebLib（Unity 风格的 AssetBundle / AssetBundleManifest /
// BuildPipeline / AssetBundleBuild / AssetBundleManager 等），本工程仅保留 Web 工具侧专用的请求/结果模型。

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
