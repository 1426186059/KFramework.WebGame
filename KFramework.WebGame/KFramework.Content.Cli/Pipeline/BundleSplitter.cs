using KFramework.MonoGame;

namespace KFramework.Content.Build;

/// <summary>
/// AssetBundle 分包模式：给定一个顶级根目录，如何把其下资源拆成多个 Bundle。
/// </summary>
public enum BundleSplitMode
{
    /// <summary>整包不拆分：根目录（含所有子目录）整体打成一个 Bundle。</summary>
    Whole,

    /// <summary>按文件夹拆分：根目录自身及其每个含资源的子文件夹各自成一个 Bundle（即原有的“按目录分别打包”）。</summary>
    Folder,
}

/// <summary>
/// 把一个「打包根目录」按指定 <see cref="BundleSplitMode"/> 枚举成若干 Bundle（包名 + 直接归属的文件列表）。
/// 与 <see cref="BundleBaker"/> 解耦：本类只负责“怎么切”，单个 Bundle 的烘焙仍交给 <see cref="BundleBaker.BuildBundle"/>。
/// </summary>
public static class BundleSplitter
{
    /// <summary>
    /// 枚举 <paramref name="bundlesRoot"/> 下的所有 Bundle。
    /// <paramref name="rawDirectory"/> 仅用于把文件路径换算成相对路径（资源名/包名都基于此计算）。
    /// </summary>
    public static IEnumerable<(string BundleName, string[] Files)> Enumerate(
        string bundlesRoot, string rawDirectory, BundleSplitMode mode)
    {
        if (mode == BundleSplitMode.Whole)
        {
            string[] files = EnumerateAll(bundlesRoot, rawDirectory).ToArray();
            if (files.Length > 0)
                yield return (RootBundleName(bundlesRoot, rawDirectory), files);
            yield break;
        }

        // Folder 模式：根目录的直接资源 + 其下每个含资源的子文件夹
        string rootName = RootBundleName(bundlesRoot, rawDirectory);
        string[] rootFiles = DirectFiles(bundlesRoot, rawDirectory).ToArray();
        if (rootFiles.Length > 0)
            yield return (rootName, rootFiles);

        foreach (string folder in Directory.EnumerateDirectories(bundlesRoot, "*", SearchOption.AllDirectories)
                     .OrderBy(f => f, StringComparer.Ordinal))
        {
            string[] files = DirectFiles(folder, rawDirectory).ToArray();
            if (files.Length == 0) continue;
            yield return (
                PakFormat.NormalizeName(Path.GetRelativePath(rawDirectory, folder).Replace('\\', '/')),
                files);
        }
    }

    /// <summary>根目录对应的包名：相对 raw 根目录的路径；空（raw 自身为根）时用 raw 最后一级文件夹名。</summary>
    private static string RootBundleName(string bundlesRoot, string rawDirectory)
    {
        string rel = Path.GetRelativePath(rawDirectory, bundlesRoot).Replace('\\', '/');
        return PakFormat.NormalizeName(
            string.IsNullOrEmpty(rel)
                ? Path.GetFileName(rawDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                : rel);
    }

    /// <summary>递归收集目录下全部资源文件（跳过忽略项）。</summary>
    private static IEnumerable<string> EnumerateAll(string dir, string rawDirectory)
        => Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
            .Where(f => !BundleBaker.IsIgnored(Path.GetRelativePath(rawDirectory, f).Replace('\\', '/')))
            .OrderBy(f => f, StringComparer.Ordinal);

    /// <summary>只收集目录的「直接」资源文件（跳过忽略项与子目录）。</summary>
    private static IEnumerable<string> DirectFiles(string dir, string rawDirectory)
        => Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly)
            .Where(f => !BundleBaker.IsIgnored(Path.GetRelativePath(rawDirectory, f).Replace('\\', '/')))
            .OrderBy(f => f, StringComparer.Ordinal);
}
