using KFramework.MonoGame;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KFramework.Content.Cli
{

    /// <summary>
    /// 读取并解析 <c>build.config.json</c>（位于 Content 根目录）。
    /// 文件不存在时自动生成一份默认配置；文件损坏时回退为带默认值的配置。
    /// </summary>
    public sealed class BuildConfig
    {
        [JsonPropertyName("rawDir")]
        public string RawDir { get; set; } = "raw";

        /// <summary>打包产物目录（相对 Content 根），缺省 hot_update_res；各示例工程可在 build.config.json 覆盖（示例统一为 hot_update_res，由 csproj 的 BuildGameContent 复制到 wwwroot/hot_update_res，运行时 ContentManager 的 contentRoot 也默认 hot_update_res）。</summary>
        [JsonPropertyName("outDir")]
        public string OutDir { get; set; } = "hot_update_res";

        /// <summary>打包中间产物目录（如 atlas 预览 PNG），相对 Content 根（与 raw 同级），缺省 temp；这些文件是打包过程副产物，不随 outDir 发布。</summary>
        [JsonPropertyName("tempDir")]
        public string TempDir { get; set; } = "temp";

        /// <summary>打包目录（字符串或数组）；空字符串表示 Content/raw 自身为打包目录。</summary>
        [JsonPropertyName("AssetBundleDir")]
        public List<string>? AssetBundleDir { get; set; } = new List<string> { "Bundles" };
        
        /// <summary>是否自动图集打包（默认 true）。</summary>
        [JsonPropertyName("autoAtlas")]
        public bool AutoAtlas { get; set; } = false;

        /// <summary>
        /// AssetBundle 分包模式（默认 Folder）：
        /// <c>Folder</c> = 按文件夹拆分（顶级根目录自身及其每个含资源的子文件夹各自成包）；
        /// <c>Whole</c> = 整包不拆分（根目录含所有子目录整体打成一个包）。
        /// </summary>
        [JsonPropertyName("BundleSplitMode")]
        public BundleSplitMode SplitMode { get; set; } = BundleSplitMode.Whole;

        /// <summary>图集页 / 整图纹理的统一转换目标（见 <see cref="ContentTextureSwitchTarget"/>）：Rgba / Webp（默认）/ Ktx2 / None（不转码，原图处理）。Png 等非统一目标的具体数据格式见 <see cref="ContentTextureDataFormat"/>。</summary>
        [JsonPropertyName("TextureSwitchTarget")]
        public ContentTextureSwitchTarget TextureSwitchTarget { get; set; } = ContentTextureSwitchTarget.Webp;

        /// <summary>basisu 可执行文件路径（<c>Ktx2</c> 编码用）。为空则用 PATH 中的 "basisu"。可相对 Content 根目录（在 <see cref="Load"/> 中按 Content 根解析为绝对路径）。</summary>
        [JsonPropertyName("basisuPath")]
        public string? BasisuPath { get; set; }

        //默认拷贝到 wwwroot 目录，方便浏览器访问
        [JsonPropertyName("copy_to_wwwroot")]
        public bool copy_to_wwwroot { get; set; } = true;



        /// <summary>解析后的打包目录列表（合并 bundlesDir / bundleDirs / AssetBundleDir；
        /// 其中的空字符串元素表示 Content/raw 自身）。</summary>
        [JsonIgnore]
        public List<string> BundleDirsResolved { get; set; } = new();

        /// <summary>
        /// 读取并解析 <c>build.config.json</c>（位于 Content 根目录）。
        /// 文件不存在时自动生成一份默认配置；文件损坏时回退为带默认值的配置。
        /// </summary>
        public static BuildConfig Load(string contentDir)
        {
            string contentRoot = contentDir;
            string configPath = Path.Combine(contentRoot, "build.config.json");

            BuildConfig config;
            if (File.Exists(configPath))
            {
                config = JsonSerializer.Deserialize<BuildConfig>(File.ReadAllText(configPath)) ?? new BuildConfig();
            }
            else
            {
                config = new BuildConfig();
                File.WriteAllText(configPath, GetDefaultJson(config), new UTF8Encoding(false));
            }
            return config;
        }

        private static string GetDefaultJson(BuildConfig mConfig)
        {
            return JsonSerializer.Serialize(mConfig, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
