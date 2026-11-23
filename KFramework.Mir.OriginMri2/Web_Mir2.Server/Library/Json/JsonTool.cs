using Server.MirDatabase;
using Server.MirEnvir;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Shared.Helpers
{
    // 服务端 JSON AOT 源生成上下文。
    // 只注册 AOT 友好的类型（原始传奇配置表 DTO + 语言表）；泛型表 / 私有内嵌类走下方 options 兼容重载。
    [JsonSourceGenerationOptions(
        GenerationMode = JsonSourceGenerationMode.Default,
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        IncludeFields = true)]
    [JsonSerializable(typeof(OriginMir2Config0))]
    [JsonSerializable(typeof(OriginMir2Config1))]
    [JsonSerializable(typeof(OriginMir2Config2))]
    [JsonSerializable(typeof(OriginMir2Config3))]
    [JsonSerializable(typeof(OriginMir2Config4))]
    [JsonSerializable(typeof(OriginMir2Config5))]
    [JsonSerializable(typeof(TextMap))]
    internal sealed partial class AppJsonContext : JsonSerializerContext
    {
        // Json 源产生器
    }

    /// <summary>
    /// 服务端统一 JSON 入口（AOT 优先）：
    /// - FromJson/ToJson + JsonTypeInfo：走 AOT 源生成（AppJsonContext.Default.X），无反射。
    /// - FromJson/ToJson + JsonSerializerOptions：兼容泛型表 / 私有内嵌类（走 JsonConfigResolver，行为与旧代码一致）。
    /// </summary>
    public static class JsonTool
    {
        // ===== AOT 重载 =====
        public static string ToJson<T>(T value, JsonTypeInfo<T> A)
        {
            return JsonSerializer.Serialize(value, A);
        }

        public static T FromJson<T>(string json, JsonTypeInfo<T> A)
        {
            return JsonSerializer.Deserialize(json, A);
        }

        // ===== options 兼容重载（泛型 / 私有内嵌类 / 需自定义 resolver 的配置表） =====
        public static string ToJson<T>(T value, JsonSerializerOptions options)
        {
            return JsonSerializer.Serialize(value, options);
        }

        public static T FromJson<T>(string json, JsonSerializerOptions options)
        {
            return JsonSerializer.Deserialize<T>(json, options);
        }

        /// <summary>配置表反序列化共享 options（过滤只读计算属性 / 指针等，与旧 JsonConfigOptions 一致）。</summary>
        public static readonly JsonSerializerOptions ConfigOptions = new JsonSerializerOptions
        {
            TypeInfoResolver = new JsonConfigResolver(),
        };
    }
}
