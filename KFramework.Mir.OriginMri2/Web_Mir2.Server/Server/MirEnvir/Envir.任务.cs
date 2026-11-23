// partial 文件：Envir 任务域方法（原始任务表加载 / 技能学习 / 任务查询 / 跨天清理）。
using Server.Library.MirDatabase;
using Server.MirDatabase;
using Server.MirObjects;
using Shared.Helpers;
using System.IO;
using System.Text.Json;

namespace Server.MirEnvir
{
    public partial class Envir
    {
        /// <summary>
        /// 原始传奇技能移植：玩家可见技能（IsShow=1）按职业（Cls：1战/2法/3道）在学习等级自动学会。
        /// Condition = 10000 + 学习等级（如 10007 = 7 级）。普通攻击(Condition=10001,Spell=10101)不纳入，
        /// 普攻走引擎 Spell.None=0，不需要作为技能学习。
        /// </summary>
        public List<MagicInfoTable> GetLearnableSkills(MirClass playerClass)
        {
            int cls = playerClass switch
            {
                MirClass.Warrior => 1,
                MirClass.Wizard => 2,
                MirClass.Taoist => 3,
                _ => -1,
            };
            var result = new List<MagicInfoTable>();
            if (cls < 0) return result;

            for (int i = 0; i < MagicInfoList.Count; i++)
            {
                MagicInfoTable info = MagicInfoList[i];
                if (info == null || info.IsShow != 1 || info.Cls != cls) continue;
                // condition 10001 = 普通攻击等 1 级无门槛技能，跳过（普攻不占技能栏）。
                if (info.Condition <= 10001) continue;
                result.Add(info);
            }
            return result;
        }

        /// <summary>技能学习等级：Condition - 10000（如 10007 → 7），钳制到 [1,255]。</summary>
        public static int GetLearnLevel(MagicInfoTable info)
        {
            if (info == null) return 0;
            int level = info.Condition >= 10000 ? info.Condition - 10000 : info.Condition;
            return Math.Max(1, Math.Min(byte.MaxValue, level));
        }
        /// <summary>
        /// 全量加载原始传奇 0config~5config 六个分片，根类统一存 OriginConfigs（所有顶层表都进内存）：
        /// 已建具体 DTO 的表同时建索引（box/task/goals/textstyle/conditions/global），其余表在 ExtraTables 原样保留。
        /// 当前任务追踪文字走构建期转换好的 QuestInfo.json（不读这些表），这里仅做全量加载备用。
        /// </summary>
        private void LoadOriginQuestTables(string baseDir)
        {
            OriginTasks.Clear();
            OriginGoals.Clear();
            OriginTaskTypeNames.Clear();
            OriginTextStyles.Clear();
            OriginBoxes.Clear();
            OriginConfigs.Clear();

            try
            {
                string f0 = Directory.GetFiles(baseDir, "0config*.json").FirstOrDefault();
                if (f0 != null)
                {
                    var cfg0 = JsonTool.FromJson<OriginMir2Config0>(File.ReadAllText(f0), AppJsonContext.Default.OriginMir2Config0);
                    if (cfg0 != null)
                    {
                        OriginConfigs[0] = cfg0;
                        if (cfg0.global != null && cfg0.global.TryGetValue("9201", out var g) && g != null)
                        {
                            foreach (var item in g.value.Split('&', StringSplitOptions.RemoveEmptyEntries))
                            {
                                var parts = item.Split('#');
                                if (parts.Length >= 2 && int.TryParse(parts[0], out int type))
                                    OriginTaskTypeNames[type] = parts[1];
                            }
                        }
                    }
                }

                string f1 = Directory.GetFiles(baseDir, "1config*.json").FirstOrDefault();
                if (f1 != null)
                {
                    var cfg1 = JsonTool.FromJson<OriginMir2Config1>(File.ReadAllText(f1), AppJsonContext.Default.OriginMir2Config1);
                    if (cfg1 != null) OriginConfigs[1] = cfg1;
                }

                string f2 = Directory.GetFiles(baseDir, "2config*.json").FirstOrDefault();
                if (f2 != null)
                {
                    var cfg2 = JsonTool.FromJson<OriginMir2Config2>(File.ReadAllText(f2), AppJsonContext.Default.OriginMir2Config2);
                    if (cfg2 != null)
                    {
                        OriginConfigs[2] = cfg2;
                        if (cfg2.box != null)
                        {
                            foreach (var kv in cfg2.box)
                            {
                                if (!int.TryParse(kv.Key, out int id) || kv.Value == null) continue;
                                kv.Value.ParseRewards();
                                OriginBoxes[id] = kv.Value;
                            }
                        }
                    }
                }

                string f3 = Directory.GetFiles(baseDir, "3config*.json").FirstOrDefault();
                if (f3 != null)
                {
                    var cfg3 = JsonTool.FromJson<OriginMir2Config3>(File.ReadAllText(f3), AppJsonContext.Default.OriginMir2Config3);
                    if (cfg3 != null) OriginConfigs[3] = cfg3;
                }

                string f4 = Directory.GetFiles(baseDir, "4config*.json").FirstOrDefault();
                if (f4 != null)
                {
                    var cfg4 = JsonTool.FromJson<OriginMir2Config4>(File.ReadAllText(f4), AppJsonContext.Default.OriginMir2Config4);
                    if (cfg4 != null)
                    {
                        OriginConfigs[4] = cfg4;
                        if (cfg4._task != null)
                            foreach (var kv in cfg4._task)
                                if (int.TryParse(kv.Key, out int id) && kv.Value != null)
                                    OriginTasks[id] = kv.Value;
                    }
                }

                string f5 = Directory.GetFiles(baseDir, "5config*.json").FirstOrDefault();
                if (f5 != null)
                {
                    var cfg5 = JsonTool.FromJson<OriginMir2Config5>(File.ReadAllText(f5), AppJsonContext.Default.OriginMir2Config5);
                    if (cfg5 != null)
                    {
                        OriginConfigs[5] = cfg5;
                        if (cfg5.goals != null)
                            foreach (var kv in cfg5.goals)
                                if (int.TryParse(kv.Key, out int id) && kv.Value != null)
                                    OriginGoals[id] = kv.Value;
                        if (cfg5.textstyle != null)
                            foreach (var kv in cfg5.textstyle)
                                if (int.TryParse(kv.Key, out int id) && kv.Value != null)
                                    OriginTextStyles[id] = kv.Value;
                    }
                }

                MessageQueue.Enqueue($"[{InstanceTag}] 原始传奇配置表加载: {OriginConfigs.Count}/6 分片（新增表 {OriginConfigs.Sum(kv => kv.Value.ExtraTables.Count)} 张）/ {OriginBoxes.Count} 奖励包 / {OriginTasks.Count} 任务 / {OriginGoals.Count} 目标 / {OriginTaskTypeNames.Count} 类型前缀 / {OriginTextStyles.Count} 文字样式");
            }
            catch (Exception ex)
            {
                MessageQueue.Enqueue("加载原始传奇配置表失败: " + ex.Message);
            }
        }
        public QuestInfoTable GetQuestInfo(int index)
        {
            return QuestInfoList.FirstOrDefault(info => info.Index == index);
        }
        private void ClearDailyQuests(CharacterInfo info)
        {
            foreach (var quest in QuestInfoList)
            {
                // 原始传奇主线为一次性任务，不参与跨天重置；其余类型按日常任务处理。
                if (quest.Type == QuestType.Main || quest.Type == QuestType.None) continue;

                for (var i = 0; i < info.CompletedQuests.Count; i++)
                {
                    if (info.CompletedQuests[i] != quest.Index) continue;

                    info.CompletedQuests.RemoveAt(i);
                }
            }

            info.Player?.GetCompletedQuests();
        }
    }
}
