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

    /// <summary>
    /// 取某地图的背景音乐 fileId（5config.soundnormal[type=3][地图id].file）。
    /// 原始传奇 JS：AudioManager.playNormal(3, gd.map.curMapId) → soundnormal["3"][地图id].file，
    /// 再由 ResUrl.url(file,15) 得到 audio/{file}.mp3。无配置返回 false。
    /// </summary>
    public static bool TryGetMapBgm(int mapIndex, out int fileId)
    {
        fileId = 0;
        if (_config5?.soundnormal == null) return false;
        if (!_config5.soundnormal.TryGetValue("3", out var byIndex) || byIndex == null) return false;
        if (!byIndex.TryGetValue(mapIndex.ToString(), out var rec) || rec == null || rec.file <= 0) return false;
        fileId = rec.file;
        return true;
    }

    /// <summary>取某技能特效的定义（5config.effect：model/action/dir/intval/blendmode）。查不到返回 false。</summary>
    public static bool TryGetEffect(int effectId, [NotNullWhen(true)] out EffectTable? effect)
    {
        effect = null;
        if (_config5?.effect == null) return false;
        return _config5.effect.TryGetValue(effectId, out effect) && effect != null;
    }

    /// <summary>取战斗飘血配置（5config.damage），原始 BloodMovie.showBloodEffect 的数据源。</summary>
    public static bool TryGetDamage(int Piaozi_DamageId, [NotNullWhen(true)] out DamageTable? damage)
    {
        damage = null;
        if (_config5?.damage == null) return false;
        return _config5.damage.TryGetValue(Piaozi_DamageId.ToString(), out damage) && damage != null;
    }

    /// <summary>
    /// 按原始传奇口径取技能特效时序：
    ///   JS initEffects(): o = cm.skills_condition[skillid][level]; s = cm.skillEffects[o.effLevelID];
    /// 先按 2config.skills_condition[技能id][等级] 取 effLevelID，再用它查 5config.skillEffects。
    /// 技能等级 0 没有条目，统一按 1 级取。查不到返回 false。
    /// </summary>
    public static bool TryGetSkillEffects(int skillId, int level, [NotNullWhen(true)] out List<SkillEffectTable>? list)
    {
        list = null;
        if (_config2?.skills_condition == null || _config5?.skillEffects == null) return false;

        int effectLevel = Math.Max(1, level);
        if (!_config2.skills_condition.TryGetValue(skillId, out var byLevel) || byLevel == null) return false;
        if (!byLevel.TryGetValue(effectLevel, out var cond) || cond == null || cond.effLevelID <= 0) return false;

        return _config5.skillEffects.TryGetValue(cond.effLevelID.ToString(), out list) && list != null && list.Count > 0;
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
