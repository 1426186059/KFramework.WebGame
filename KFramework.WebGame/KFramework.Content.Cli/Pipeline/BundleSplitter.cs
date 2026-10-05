namespace KFramework.Content.Cli
{

    public enum BundleSplitMode
    {
        Whole,

        Folder,
    }

    public static class BundleSplitter
    {
        public static IEnumerable<(string BundleName, string FullDir, string[] Files)> Enumerate(
            string bundlesRoot, string rawDirectory, BundleSplitMode mode)
        {
            if (mode == BundleSplitMode.Whole)
            {
                string[] files = EnumerateAll(bundlesRoot, rawDirectory).ToArray();
                if (files.Length > 0)
                    yield return (RootBundleName(bundlesRoot, rawDirectory), RelativeDir(rawDirectory, bundlesRoot), files);
                yield break;
            }

            // Folder 模式：根目录的直接资源 + 其下每个含资源的子文件夹
            string rootName = RootBundleName(bundlesRoot, rawDirectory);
            string[] rootFiles = DirectFiles(bundlesRoot, rawDirectory).ToArray();
            if (rootFiles.Length > 0)
                yield return (rootName, RelativeDir(rawDirectory, bundlesRoot), rootFiles);

            foreach (string folder in Directory.EnumerateDirectories(bundlesRoot, "*", SearchOption.AllDirectories)
                         .OrderBy(f => f, StringComparer.Ordinal))
            {
                string[] files = DirectFiles(folder, rawDirectory).ToArray();
                if (files.Length == 0) continue;
                yield return (
                    AssetName.Normalize(Path.GetRelativePath(rawDirectory, folder).Replace('\\', '/')),
                    RelativeDir(rawDirectory, folder),
                    files);
            }
        }

        private static string RelativeDir(string rawDirectory, string dir)
        {
            string rel = Path.GetRelativePath(rawDirectory, dir).Replace('\\', '/');
            return rel == "." ? "" : rel;
        }

        private static string RootBundleName(string bundlesRoot, string rawDirectory)
        {
            string rel = Path.GetRelativePath(rawDirectory, bundlesRoot).Replace('\\', '/');
            return AssetName.Normalize(
                string.IsNullOrEmpty(rel)
                    ? Path.GetFileName(rawDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                    : rel);
        }

        private static IEnumerable<string> EnumerateAll(string dir, string rawDirectory)
        {
            return Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                .Where(f => !BundleBaker.IsIgnored(Path.GetRelativePath(rawDirectory, f).Replace('\\', '/')))
                .OrderBy(f => f, StringComparer.Ordinal);
        }

        private static IEnumerable<string> DirectFiles(string dir, string rawDirectory)
        {
            return Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly)
                .Where(f => !BundleBaker.IsIgnored(Path.GetRelativePath(rawDirectory, f).Replace('\\', '/')))
                .OrderBy(f => f, StringComparer.Ordinal);
        }
    }

}
