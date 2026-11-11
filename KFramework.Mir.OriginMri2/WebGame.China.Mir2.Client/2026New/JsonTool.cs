// 浏览器 WASM（AOT/裁剪）构建没有反射元数据，基于反射的 System.Text.Json 会抛
// JsonSerializerIsReflectionDisabled。因此所有 (反)序列化都走【源生成】的 AppJsonContext，
// 并通过 KFramework.MonoGame.JsonTool 这个静态工具类统一入口（传入 JsonTypeInfo<T>，
// 由编译期生成代码完成，不碰反射）。
using System.Text.Json;
using System.Text.Json.Serialization;

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Default,
    WriteIndented = false,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(TextMap))]
[JsonSerializable(typeof(MapTable))]

// 图集 JSON 核心数据：普通图集 / MovieClip 图集及其嵌套类型与 frames 的两种集合形态。
[JsonSerializable(typeof(EgretAtlasJson))]
[JsonSerializable(typeof(EgretMovieClipJson))]
[JsonSerializable(typeof(EgretRectJson))]
[JsonSerializable(typeof(EgretMovieClipTimelineJson))]
[JsonSerializable(typeof(EgretMovieClipFrameJson))]
[JsonSerializable(typeof(EgretMovieClipLabelJson))]
[JsonSerializable(typeof(EgretMovieClipEventJson))]
[JsonSerializable(typeof(Dictionary<string, EgretRectJson>))]
[JsonSerializable(typeof(List<EgretRectJson>))]
// 原始传奇枢轴表 modelInfo_stand.json：{ model: { action: [standx,standy,intval?,totaldir?] } }，小数用 double[] 容错。
[JsonSerializable(typeof(Dictionary<int, Dictionary<int, double[]>>), TypeInfoPropertyName = "ModelInfoStandTable")]
// 资源总索引 manifest（allmanifest{ver}.json）：根即字典 { key: { v, s } }。
[JsonSerializable(typeof(Dictionary<string, ManifestEntryJson>), TypeInfoPropertyName = "ManifestEntryDictionary")]
// 白鹭资源清单 default.res.json：{ resources:[...], groups:[...] }。
[JsonSerializable(typeof(DefaultResJson))]
// 资源版本表 resourceVersion.json：根即字典 { name: version }。
[JsonSerializable(typeof(Dictionary<string, string>), TypeInfoPropertyName = "ResourceVersionDictionary")]
internal sealed partial class AppJsonContext : JsonSerializerContext
{
}
