using KTexturePacker.Parser;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

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

    //专门为 AOT 设计的 Json 工具类
    public static class JsonTool
    {
        public static string ToJson<T>(T value)
        {
            JsonTypeInfo<T> A = (JsonTypeInfo<T>)AppJsonContext.Default.GetTypeInfo(typeof(T));
            return JsonSerializer.Serialize(value, A);
        }

        public static T? FromJson<T>(string json)
        {
            JsonTypeInfo<T> A = (JsonTypeInfo<T>)AppJsonContext.Default.GetTypeInfo(typeof(T));
            return JsonSerializer.Deserialize(json, A);
        }

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
