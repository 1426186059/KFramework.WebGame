using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace KFramework.MonoGame
{
    [JsonSerializable(typeof(AssetBundleManifest))]
    [JsonSerializable(typeof(BundlePackage))]
    [JsonSerializable(typeof(AssetBundleContent))]
    [JsonSerializable(typeof(AssetBundleEntry))]
    [JsonSerializable(typeof(List<BundlePackage>))]
    [JsonSerializable(typeof(List<AssetBundleEntry>))]
    [JsonSerializable(typeof(List<string>))]

    [JsonSourceGenerationOptions(
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true)]
    internal sealed partial class AppJsonContext : JsonSerializerContext
    {
        //这是Json 源产生器 
    }

    //专门为 AOT 设计的 Json 工具类
    public static class JsonTool
    {
        public static string ToJson<T>(T value, JsonTypeInfo<T> A)
        {
            return JsonSerializer.Serialize(value, A);
        }

        public static T? FromJson<T>(string json, JsonTypeInfo<T> A)
        {
            return JsonSerializer.Deserialize(json, A);
        }
    }
}
