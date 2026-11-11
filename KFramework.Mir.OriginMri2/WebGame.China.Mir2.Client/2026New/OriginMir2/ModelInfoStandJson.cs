using KFramework.MonoGame;
using System.Text.Json;

// =====================================================================================
// 原始传奇模型枢轴表 modelInfo_stand.json 的数据类：只负责反序列化，不参与运行时绘制。
//
// 结构（根对象即字典）：
//   { "modelId": { "actionId": [standx, standy, intval?, totaldir?] } }
// 例如 "15221": { "1": [311.25, 300, 50, 1] }。
//
// 注意 standx/standy 可能是小数（如 311.25，导出时的惯性数据），因此数组元素用 double[]，
// 读取时统一四舍五入，避免旧代码 GetInt32 遇小数抛异常、拖垮整张表（曾导致 NPC 脚底抖动）。
// 风格参考 EgretAtlasJson：纯数据、源生成反序列化、不掺运行时逻辑。
// =====================================================================================

/// <summary>原始传奇模型枢轴表 modelInfo_stand.json 的强类型视图。</summary>
public sealed class ModelInfoStandJson
{
    /// <summary>model → (action → [standx, standy, intval?, totaldir?])；数组用 double[] 容小数。</summary>
    public Dictionary<int, Dictionary<int, double[]>> Table { get; set; }

    public static ModelInfoStandJson Deserialize(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            // AOT：传入源生成上下文产出的 JsonTypeInfo，不走反射式 JsonSerializer。
            var table = JsonTool.FromJson(
                json, AppJsonContext.Default.ModelInfoStandTable);
            return table == null ? null : new ModelInfoStandJson { Table = table };
        }
        catch (JsonException) { return null; }
    }

    /// <summary>取 (model, action) 的枢轴与元信息；小数已四舍五入。</summary>
    public bool TryGet(int model, int action,
        out int standx, out int standy, out int intval, out int totaldir)
    {
        standx = standy = intval = totaldir = 0;
        if (Table == null ||
            !Table.TryGetValue(model, out var acts) ||
            !acts.TryGetValue(action, out var v) ||
            v == null || v.Length < 2)
            return false;

        standx = (int)Math.Round(v[0]);
        standy = (int)Math.Round(v[1]);
        if (v.Length >= 3) intval = (int)Math.Round(v[2]);
        if (v.Length >= 4) totaldir = (int)Math.Round(v[3]);
        return true;
    }
}
