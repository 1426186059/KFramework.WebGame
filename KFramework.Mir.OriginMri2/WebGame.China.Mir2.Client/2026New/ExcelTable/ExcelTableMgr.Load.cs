// ExcelTableMgr 的加载辅助（partial）：分片内定位 "map" 表 + AOT 安全的 FlexInt 解析器。
using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Client.MirObjects;   // Mir2OriginResource / FlexIntJsonConverter

public static partial class ExcelTableMgr
{
    // 源生成上下文 + FlexInt 容错：给所有 int 属性挂上 FlexIntJsonConverter（数字/数字字符串兼容）。
    private sealed class ConfigTypeInfoResolver : IJsonTypeInfoResolver
    {
        private readonly IJsonTypeInfoResolver _inner = AppJsonContext.Default;
        public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            var ti = _inner.GetTypeInfo(type, options);
            if (ti != null)
                foreach (var p in ti.Properties)
                    if (p.PropertyType == typeof(int))
                        p.CustomConverter = new FlexIntJsonConverter();
            return ti;
        }
    }

    // 在分片 JSON 里前向扫描，定位名为 tableName 的顶层属性，返回其 JsonElement；找不到返回 Undefined。
    private static JsonElement FindTable(byte[] json, string tableName)
    {
        var opts = new JsonReaderOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        var reader = new Utf8JsonReader(json, opts);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) return default;
        var nameUtf8 = Encoding.UTF8.GetBytes(tableName);
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) return default;
            if (reader.TokenType != JsonTokenType.PropertyName) continue;
            if (reader.ValueTextEquals(nameUtf8))
            {
                reader.Read();
                return JsonElement.ParseValue(ref reader);
            }
            reader.Skip();
        }
        return default;
    }
}
