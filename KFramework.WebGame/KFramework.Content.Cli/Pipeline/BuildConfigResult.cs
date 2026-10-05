using KFramework.MonoGame;

namespace KFramework.Content.Cli
{
    /// <summary>
    /// <see cref="BuildConfig"/> 解析后的精确结果（静态持有）：所有相对路径字段都被解析为
    /// 相对 Content 根目录的完整（绝对）路径，且与 <see cref="BuildConfig"/> 的字段一一对应。
    /// 调用 <see cref="Parse"/> 后，下方各静态字段即为本次解析出的完整结果。
    /// </summary>
    public static class BuildConfigResult
    {
        /// <summary>Content 根目录（build.config.json 所在目录）的完整绝对路径。</summary>
        public static string ContentRoot { get; set; } = "";

        /// <summary>对应 <see cref="BuildConfig.RawDir"/> 的完整路径（绝对）。</summary>
        public static string RawDirFull { get; set; } = "";

        /// <summary>对应 <see cref="BuildConfig.OutDir"/> 的完整路径（绝对）。</summary>
        public static string OutDirFull { get; set; } = "";

        /// <summary>对应 <see cref="BuildConfig.TempDir"/> 的完整路径（绝对）。</summary>
        public static string TempDirFull { get; set; } = "";

        /// <summary>
        /// 对应 <see cref="BuildConfig.AssetBundleDir"/> / <see cref="BuildConfig.BundleDirsResolved"/> 的完整路径列表；
        /// 其中空字符串元素已被替换为 Content/raw 自身（即 <see cref="RawDirFull"/>）。
        /// </summary>
        public static List<string> BundleDirsFull { get; set; } = new List<string>();

        /// <summary>对应 <see cref="BuildConfig.AutoAtlas"/>。</summary>
        public static bool AutoAtlas { get; set; }

        /// <summary>对应 <see cref="BuildConfig.SplitMode"/>。</summary>
        public static BundleSplitMode SplitMode { get; set; }

        /// <summary>对应 <see cref="BuildConfig.TextureSwitchTarget"/>。</summary>
        public static ContentTextureSwitchTarget TextureSwitchTarget { get; set; }

        /// <summary>对应 <see cref="BuildConfig.BasisuPath"/> 的完整路径（绝对）；未配置时为 null。</summary>
        public static string? BasisuPathFull { get; set; }

        /// <summary>对应 <see cref="BuildConfig.copy_to_wwwroot"/>。</summary>
        public static bool copy_to_wwwroot { get; set; }

        /// <summary>
        /// 将已加载的 <see cref="BuildConfig"/> 解析为精确结果（所有相对路径解析为完整绝对路径），
        /// 并填充到本类的各静态字段中。
        /// </summary>
        /// <param name="config">经 <see cref="BuildConfig.Load"/> 加载的配置（其 BundleDirsResolved / BasisuPath 已被初步解析）。</param>
        /// <param name="contentRoot">Content 根目录（build.config.json 所在目录）。</param>
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
