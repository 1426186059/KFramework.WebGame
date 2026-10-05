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

            config.BundleDirsResolved = config.AssetBundleDir ?? new List<string>();
            var bundleDirsFull = new List<string>();
            foreach (var d in config.BundleDirsResolved)
            {
                // 空字符串表示「Content/raw 自身」作为打包目录；其余相对 raw 目录解析（与 ContentBuilder 中 Path.Combine(rawDirectory, dir) 一致）
                bundleDirsFull.Add(string.IsNullOrEmpty(d) ? rawDirFull : Path.GetFullPath(Path.Combine(rawDirFull, d)));
            }

            string? basisuPathFull = string.IsNullOrWhiteSpace(config.Ktx2ExePath)
                ? null
                : Path.GetFullPath(config.Ktx2ExePath);

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

            // KTX2 目标要求 basisu 可执行文件真实存在：路径未配置或文件不存在都应立即报错，
            // 而不是在打包时静默回退（那样会悄悄丢失 KTX2 压缩，且难以察觉）。
            if (TextureSwitchTarget == ContentTextureSwitchTarget.Ktx2 &&
                (string.IsNullOrWhiteSpace(BasisuPathFull) || !File.Exists(BasisuPathFull)))
            {
                throw new InvalidOperationException(
                    $"TextureSwitchTarget 已设为 Ktx2，但 basisu 可执行文件不存在或路径未配置" +
                    $"（Ktx2ExePath={config.Ktx2ExePath ?? "<未设置>"}，解析后={BasisuPathFull ?? "<空>"}）。" +
                    "请安装 Basis Universal 命令行工具，并在 build.config.json 中将 \"Ktx2ExePath\" 设置为该可执行文件的有效路径。");
            }
        }
    }
}
