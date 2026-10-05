using KFramework.MonoGame;

namespace KFramework.Content.Cli
{
    public static class BuildConfigResult
    {
        public static string ContentRoot { get; set; } = "";

        public static string RawDirFull { get; set; } = "";

        public static string OutDirFull { get; set; } = "";

        public static string TempDirFull { get; set; } = "";

        public static List<string> BundleDirsFull { get; set; } = new List<string>();

        public static bool AutoAtlas { get; set; }

        public static BundleSplitMode SplitMode { get; set; }

        public static ContentTextureSwitchTarget TextureSwitchTarget { get; set; }

        public static string? BasisuPathFull { get; set; }

        public static bool copy_to_wwwroot { get; set; }

        public static void Parse(BuildConfig config, string contentRoot)
        {
            string root = Path.GetFullPath(contentRoot);

            string rawDirFull  = Path.GetFullPath(Path.Combine(root, config.RawDir));
            string outDirFull  = Path.GetFullPath(Path.Combine(root, config.OutDir));
            string tempDirFull = Path.GetFullPath(Path.Combine(root, config.TempDir));

            var bundleDirsFull = new List<string>();
            foreach (var d in config.BundleDirsResolved)
            {
                // 空字符串表示「Content/raw 自身」作为打包目录；其余相对 raw 目录解析（与 ContentBuilder 中 Path.Combine(rawDirectory, dir) 一致）
                bundleDirsFull.Add(string.IsNullOrEmpty(d) ? rawDirFull : Path.GetFullPath(Path.Combine(rawDirFull, d)));
            }

            string? basisuPathFull = string.IsNullOrWhiteSpace(config.BasisuPath)
                ? null
                : Path.GetFullPath(config.BasisuPath);

            ContentRoot          = root;
            RawDirFull           = rawDirFull;
            OutDirFull           = outDirFull;
            TempDirFull          = tempDirFull;
            BundleDirsFull       = bundleDirsFull;
            AutoAtlas            = config.AutoAtlas;
            SplitMode            = config.SplitMode;
            TextureSwitchTarget = config.TextureSwitchTarget;
            BasisuPathFull       = basisuPathFull;
            copy_to_wwwroot      = config.copy_to_wwwroot;
        }
    }
}
