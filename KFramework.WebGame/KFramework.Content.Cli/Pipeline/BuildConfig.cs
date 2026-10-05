using KFramework.MonoGame;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KFramework.Content.Cli
{

    public sealed class BuildConfig
    {
        [JsonPropertyName("rawDir")]
        public string RawDir { get; set; } = "raw";

        [JsonPropertyName("outDir")]
        public string OutDir { get; set; } = "hot_update_res";

        [JsonPropertyName("tempDir")]
        public string TempDir { get; set; } = "temp";

        [JsonPropertyName("AssetBundleDir")]
        public List<string>? AssetBundleDir { get; set; } = new List<string> { "Bundles" };
        
        [JsonPropertyName("autoAtlas")]
        public bool AutoAtlas { get; set; } = false;

        [JsonPropertyName("BundleSplitMode")]
        public BundleSplitMode SplitMode { get; set; } = BundleSplitMode.Whole;

        [JsonPropertyName("TextureSwitchTarget")]
        public ContentTextureSwitchTarget TextureSwitchTarget { get; set; } = ContentTextureSwitchTarget.Webp;

        [JsonPropertyName("Ktx2ExePath")]
        public string? Ktx2ExePath { get; set; }

        //默认拷贝到 wwwroot 目录，方便浏览器访问
        [JsonPropertyName("copy_to_wwwroot")]
        public bool copy_to_wwwroot { get; set; } = true;



        [JsonIgnore]
        public List<string> BundleDirsResolved { get; set; } = new();

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
