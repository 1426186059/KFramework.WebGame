namespace WebLib;

/// <summary>
/// 总清单里的一个资源包条目。File 已含内容短哈希（如 3-1.a1b2c3d4.web.lib），
/// 对齐 Unity <c>AssetBundleManifest</c> 中“包名 + 哈希”的索引概念。
/// </summary>
/// <param name="Name">逻辑名，如 3-1</param>
/// <param name="File">包文件名（含短哈希），如 3-1.a1b2c3d4.web.lib</param>
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
