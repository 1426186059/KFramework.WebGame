using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using KFramework.MonoGame;

namespace KFramework.Content.Build
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
        [JsonConverter(typeof(StringOrStringArrayConverter))]
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
        [JsonConverter(typeof(BundleSplitModeConverter))]
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
                try
                {
                    config = JsonSerializer.Deserialize<BuildConfig>(File.ReadAllText(configPath)) ?? new BuildConfig();
                }
                catch
                {
                    config = new BuildConfig();
                }
            }
            else
            {
                config = new BuildConfig();
                try
                {
                    // 配置文件不存在：自动生成一份默认 build.config.json，方便后续按目录分别打包
                    File.WriteAllText(configPath, GetDefaultJson(), new UTF8Encoding(false));
                }
                catch
                {
                    // 无法写入也不影响本次打包
                }
            }

            var dirs = new List<string>();
            if (config.AssetBundleDir is not null) dirs.AddRange(ResolveDirs(config.AssetBundleDir));
            config.BundleDirsResolved = dirs;

            config.OutDir = config.OutDir.Replace('\\', '/').Trim('/');
            config.TempDir = config.TempDir.Replace('\\', '/').Trim('/');
            // basisu 由 EncodeKtx2 直接 Process.Start，需绝对路径；相对则按 Content 根解析。
            if (!string.IsNullOrWhiteSpace(config.BasisuPath))
                config.BasisuPath = Path.GetFullPath(Path.Combine(contentRoot, config.BasisuPath));
            return config;
        }

        private static IEnumerable<string> ResolveDirs(List<string> list)
        {
            foreach (var raw in list)
            {
                if (raw is null) continue;
                // 空字符串表示「打包根目录（Content/raw）自身」作为打包目录
                if (string.IsNullOrWhiteSpace(raw)) yield return "";
                else yield return raw.Replace('\\', '/').Trim('/');
            }
        }

        private static string GetDefaultJson()
        {
            BuildConfig mConfig = new BuildConfig();
            return JsonSerializer.Serialize(mConfig, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    /// <summary>允许 JSON 字段既可以是单个字符串，也可以是字符串数组，统一反序列化为 <see cref="List{T}"/>（T=string）。</summary>
    public sealed class StringOrStringArrayConverter : JsonConverter<List<string>>
    {
        public override List<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var list = new List<string>();
            if (reader.TokenType == JsonTokenType.String)
            {
                list.Add(reader.GetString() ?? "");
            }
            else if (reader.TokenType == JsonTokenType.StartArray)
            {
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (reader.TokenType == JsonTokenType.String) list.Add(reader.GetString() ?? "");
                }
            }
            return list;
        }

        public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach (var s in value) writer.WriteStringValue(s);
            writer.WriteEndArray();
        }
    }

    /// <summary>把 <see cref="BundleSplitMode"/> 序列化为小写字符串（folder/whole），反序列化时大小写不敏感，兼容旧配置。</summary>
    public sealed class BundleSplitModeConverter : JsonConverter<BundleSplitMode>
    {
        public override BundleSplitMode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string? s = reader.GetString();
            if (s is null) return BundleSplitMode.Folder;
            return Enum.TryParse<BundleSplitMode>(s, ignoreCase: true, out var value) ? value : BundleSplitMode.Folder;
        }

        public override void Write(Utf8JsonWriter writer, BundleSplitMode value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString().ToLowerInvariant());
        }
    }
}
