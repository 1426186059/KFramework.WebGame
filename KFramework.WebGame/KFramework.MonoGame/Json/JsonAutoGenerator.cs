using System.Text.Json;
using System.Text.Json.Serialization;

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
    internal sealed partial class JsonAutoGenerator : JsonSerializerContext
    {
        //这是Json 源产生器 
    }
}
