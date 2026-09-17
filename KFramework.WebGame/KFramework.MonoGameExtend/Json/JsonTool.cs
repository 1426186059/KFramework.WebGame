using KTexturePacker.Parser;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KFramework.MonoGameExtend
{
    [JsonSerializable(typeof(AtlasData))] // 增加 KTexturePacker 序列化
    [JsonSerializable(typeof(AtlasPage))]
    [JsonSerializable(typeof(AtlasRegion))]
    [JsonSerializable(typeof(List<AtlasRegion>))]
    [JsonSerializable(typeof(List<AtlasPage>))]
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
}
