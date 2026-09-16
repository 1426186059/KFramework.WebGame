using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KFramework.MonoGame;

/// <summary>
/// <c>build.config.json</c> 的强类型模型（位于 Content 根目录，与 Pipeline 的 ContentBuilder 配套）。
/// 对应字段：
/// <list type="bullet">
///   <item><c>bundlesDir</c> / <c>bundleDirs</c> / <c>AssetBundleDir</c>：打包目录，可填单个字符串或字符串数组；
///         值为空字符串 <c>""</c> 表示「打包根目录（Content/raw）自身」作为打包目录，其下每个含资源的子文件夹各自成包；
///         三者均缺省时回退为 <c>Bundles</c>。</item>
///   <item><c>outDir</c>：打包产物目录（相对 Content 根），默认 <c>content</c>。</item>
///   <item><c>autoAtlas</c>：是否自动图集打包，默认 <c>true</c>（true 时独立 .png 经图集打包器装箱，false 时原样整图入包）。</item>
///   <item><c>deploy</c>：发布方式 <c>www</c> / <c>serve</c> / <c>none</c>，默认 <c>www</c>。</item>
///   <item><c>wwwDir</c>：本地静态服务器根目录（相对 Content 根），默认 <c>www</c>。</item>
///   <item><c>port</c>：deploy=serve 时的端口，默认 <c>8080</c>。</item>
/// </list>
/// </summary>
public sealed class BuildConfig
{
    /// <summary>打包目录（字符串或数组）；空字符串表示 Content/raw 自身为打包目录。</summary>
    [JsonPropertyName("AssetBundleDir")]
    [JsonConverter(typeof(StringOrStringArrayConverter))]
    public List<string>? AssetBundleDir { get; set; }

    /// <summary>打包产物目录（相对 Content 根），缺省 content。</summary>
    [JsonPropertyName("outDir")]
    public string OutDir { get; set; } = "content";

    /// <summary>是否自动图集打包（默认 true）。</summary>
    [JsonPropertyName("autoAtlas")]
    public bool AutoAtlas { get; set; } = true;

    /// <summary>发布方式：www / serve / none，缺省 www。</summary>
    [JsonPropertyName("deploy")]
    public string Deploy { get; set; } = "www";

    /// <summary>本地静态服务器根目录（相对 Content 根），缺省 www。</summary>
    [JsonPropertyName("wwwDir")]
    public string WwwDir { get; set; } = "www";

    /// <summary>deploy=serve 时的端口，缺省 8080。</summary>
    [JsonPropertyName("port")]
    public int Port { get; set; } = 8080;

    /// <summary>解析后的打包目录列表（合并 bundlesDir / bundleDirs / AssetBundleDir；
    /// 其中的空字符串元素表示 Content/raw 自身）。</summary>
    [JsonIgnore]
    public List<string> BundleDirsResolved { get; set; } = new();

    /// <summary>
    /// 读取并解析 <c>build.config.json</c>（位于 Content 根目录）。
    /// 文件不存在时自动生成一份默认配置；文件损坏时回退为带默认值的配置。
    /// </summary>
    public static BuildConfig Load(string rawDirectory)
    {
        string contentRoot = Path.GetDirectoryName(Path.GetFullPath(rawDirectory)) ?? rawDirectory;
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
                File.WriteAllText(configPath, DefaultJson, new UTF8Encoding(false));
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
        config.WwwDir = config.WwwDir.Replace('\\', '/').Trim('/');
        config.Deploy = config.Deploy.ToLowerInvariant();
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

    private const string DefaultJson =
        "{\"bundlesDir\":\"Bundles\",\"outDir\":\"content\",\"deploy\":\"www\",\"wwwDir\":\"www\",\"port\":8080}";
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
