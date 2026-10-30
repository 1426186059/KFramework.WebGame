
// 浏览器 WASM 构建会裁剪反射元数据，导致基于反射的 System.Text.Json 序列化抛
// JsonSerializerIsReflectionDisabled。改用源生成的 JsonSerializerContext，
// 在编译期生成 (反)序列化代码，不依赖运行时反射。
using System.Text.Json.Serialization;

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(TextMap))]
// 配置表（ExcelTableMgr 加载的地图相关表）——AOT/裁剪构建下反序列化必须走源生成，否则抛 JsonSerializerIsReflectionDisabled
[JsonSerializable(typeof(MapTable))]
[JsonSerializable(typeof(MinimapTable))]
[JsonSerializable(typeof(GuajimapTable))]
[JsonSerializable(typeof(MapnpcTable))]
[JsonSerializable(typeof(MapPlayTable))]
[JsonSerializable(typeof(StarmapTable))]
[JsonSerializable(typeof(SheepmapTable))]
[JsonSerializable(typeof(XitongMapTable))]
internal sealed partial class AppJsonContext : JsonSerializerContext
{
}
