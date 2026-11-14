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

            // 手工覆盖表优先，其次才是 1config.item[col17] 自动生成的映射。
            string s;
            if (!Mir2OriginModelOverrides.Models.TryGetValue(pgItemId, out s) &&
                !Mir2OriginItemModelMap.Body.TryGetValue(pgItemId, out s)) return fallback;
            if (string.IsNullOrEmpty(s)) return fallback;

            int slash = s.IndexOf('/');
            string pick = slash < 0 ? s : (female ? s.Substring(slash + 1) : s.Substring(0, slash));
            return int.TryParse(pick, out var m) ? m : fallback;
        }

        /// <summary>按性别取模型号；若该性别的站立帧元数据始终拉不到，回落到另一性别的号。
        /// 帧元数据是异步加载的，未就绪时直接返回主性别号，等就绪后由 PosVersion 触发重算。</summary>
        public static int GetAvailableModel(int pgItemId, bool female, int fallback)
        {
            int primary = GetModel(pgItemId, female, fallback);
            if (primary > 0 && Mir2OriginUIModel.HasModelPos(primary)) return primary;

            int other = GetModel(pgItemId, !female, 0);
            if (other > 0 && Mir2OriginUIModel.HasModelPos(other)) return other;

            return primary > 0 ? primary : (other > 0 ? other : fallback);
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
