
// 浏览器 WASM 构建会裁剪反射元数据，导致基于反射的 System.Text.Json 序列化抛
// JsonSerializerIsReflectionDisabled。改用源生成的 JsonSerializerContext，
// 在编译期生成 (反)序列化代码，不依赖运行时反射。
using System.Text.Json.Serialization;

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(TextMap))]
internal sealed partial class AppJsonContext : JsonSerializerContext
{

}

