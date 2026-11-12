// ExcelTableMgr 的“模型×动作”查询扩展。
// 把原来 (long)model * 1000 + action 这种把二维（模型/动作）硬塞进一维 long 键的 hack，
// 改为直接按 model + action 两个参数从 2config.modelInfo（model→action→记录）查询。
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

public static partial class ExcelTableMgr
{
    /// <summary>取某模型某动作的模型信息（standx/standy/intval/totaldir）。查不到返回 false。</summary>
    public static bool TryGetModelInfo(int model, int action, [NotNullWhen(true)] out ModelInfoTable? info)
    {
        info = null;
        if (_config2?.modelInfo == null) return false;
        if (!_config2.modelInfo.TryGetValue(model, out var byAction) || byAction == null) return false;
        return byAction.TryGetValue(action, out info) && info != null;
    }

    /// <summary>取某模型登记的动作号列表（用于预加载/遍历方向）。查不到或为空返回 false。</summary>
    public static bool TryGetModelActions(int model, [NotNullWhen(true)] out List<int>? actions)
    {
        actions = null;
        if (_config2?.modelInfo == null) return false;
        if (!_config2.modelInfo.TryGetValue(model, out var byAction) || byAction == null) return false;
        var list = new List<int>();
        foreach (var kv in byAction)
            if (kv.Value != null) list.Add(kv.Key);
        if (list.Count == 0) return false;
        actions = list;
        return true;
    }
}
