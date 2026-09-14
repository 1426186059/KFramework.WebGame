using System.Text.Json;
using System.Text.Json.Serialization;

namespace KFramework.MonoGame
{
    public static class JsonTool
    {
        public static T FromJson<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json);
        }

        public static string ToJson(object t)
        {
            return JsonSerializer.Serialize(t);
        }
    }

    // 定义你的数据模型
    public class GameSaveData
    {
        public int Level { get; set; }
        public string PlayerName { get; set; }
    }

    // 定义 AOT 兼容的序列化上下文
    [JsonSerializable(typeof(GameSaveData))]
    public partial class AppJsonContext : JsonSerializerContext { }

}
