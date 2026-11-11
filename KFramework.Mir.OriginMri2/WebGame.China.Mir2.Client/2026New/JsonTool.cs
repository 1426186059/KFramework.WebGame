// 浏览器 WASM（AOT/裁剪）构建没有反射元数据，基于反射的 System.Text.Json 会抛
// JsonSerializerIsReflectionDisabled。因此所有 (反)序列化都走【源生成】的 AppJsonContext，
// 并通过 JsonTool 这个静态工具类统一入口（传入 JsonTypeInfo<T>，由编译期生成代码完成，不碰反射）。
using System.Text.Json.Serialization;

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(TextMap))]
[JsonSerializable(typeof(MapTable))]
internal sealed partial class AppJsonContext : JsonSerializerContext
{
}
