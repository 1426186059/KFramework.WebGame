using System.Text.Json;
using System.Text.Json.Serialization;

namespace Client.MirObjects
{
    /// <summary>
    /// 数字/字符串 容错转换器：JSON 里写成 number 或数字字符串都按 int 解析。
    /// 原始传奇不同版本/分片里，部分数字字段（img、width 等）可能以 "103" 字符串形式下发，
    /// 用本转换器即可在不改变属性类型（仍是 int）的前提下兼容。
    /// </summary>
    internal sealed class FlexIntJsonConverter : JsonConverter<int>
    {
        public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var n)) return n;
            if (reader.TokenType == JsonTokenType.String && int.TryParse(reader.GetString(), out var s)) return s;
            return 0;
        }

        public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
            => writer.WriteNumberValue(value);
    }
}
