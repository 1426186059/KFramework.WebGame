// 原始页游玩家分层模型解算：给定某装备槽的【页游物品 id】，查 cm.item[col17] 得外观模型号。
// 对应页游 Logic.getEquipId / getModleBySex：col17 形如 "男/女"，女取后半、男取前半；无 "/" 视为通用单号。
using System;

namespace Client.MirObjects
{
    internal static class Mir2OriginModelResolver
    {
        public static int GetModel(int pgItemId, bool female, int fallback)
        {
            if (pgItemId <= 0) return fallback;
            if (!Mir2OriginItemModelMap.Body.TryGetValue(pgItemId, out var s) || string.IsNullOrEmpty(s)) return fallback;
            int slash = s.IndexOf('/');
            string pick = slash < 0 ? s : (female ? s.Substring(slash + 1) : s.Substring(0, slash));
            return int.TryParse(pick, out var m) ? m : fallback;
        }

        // 页游 cm.title[id].model（称号/足迹/法阵/羽翼...，type 区分）：同样 "男/女" 或单号。
        public static int GetTitleModel(int pgTitleId, bool female, int fallback)
        {
            if (pgTitleId <= 0) return fallback;
            if (!Mir2OriginTitleModelMap.Model.TryGetValue(pgTitleId, out var s) || string.IsNullOrEmpty(s)) return fallback;
            int slash = s.IndexOf('/');
            string pick = slash < 0 ? s : (female ? s.Substring(slash + 1) : s.Substring(0, slash));
            return int.TryParse(pick, out var m) ? m : fallback;
        }
    }
}
