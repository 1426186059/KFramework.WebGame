using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Server.MirEnvir
{
    /// <summary>
    /// 配置表 JSON 反序列化专用 TypeInfoResolver：
    /// 1) 只保留字段与“有 setter”的属性，忽略只读计算属性（如 QuestInfo.FinishNPC 会访问
    ///    Envir.NPCs.Single() 在空匹配时抛异常、ItemInfo.FriendlyName 等）；
    /// 2) 移除指针 / byref / LinkedListNode&lt;&gt; 等不可序列化成员。
    /// 与导出工具 MirDBToJson 的 IgnoreResolver 保持一致，确保往返对称。
    /// </summary>
    internal sealed class JsonConfigResolver : DefaultJsonTypeInfoResolver
    {
        public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            JsonTypeInfo ti = base.GetTypeInfo(type, options);
            if (ti == null) return ti;
            foreach (var p in ti.Properties.ToList())
            {
                if (p.AttributeProvider is PropertyInfo pi && !pi.CanWrite)
                {
                    ti.Properties.Remove(p);
                    continue;
                }
                Type pt = p.PropertyType;
                if (pt != null && (pt.IsPointer || pt.IsByRef ||
                    (pt.IsGenericType && pt.GetGenericTypeDefinition() == typeof(LinkedListNode<>))))
                {
                    ti.Properties.Remove(p);
                }
            }
            return ti;
        }
    }
}
