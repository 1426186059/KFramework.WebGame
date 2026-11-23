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
        /// 加载原始传奇任务权威表（仿 LoadOriginStores 的整文件 DTO 写法）：
        /// 4config._task → OriginTasks；5config.goals → OriginGoals；0config.global[9201] → OriginTaskTypeNames。
        /// 任务追踪面板（QuestProgressInfo.UpdateTasks）直接读这些原表生成文字，忠实原版 dataChanged。
        /// </summary>
        private void LoadOriginQuestTables(string baseDir)
        {
            OriginTasks.Clear();
            OriginGoals.Clear();
            OriginTaskTypeNames.Clear();
            OriginTextStyles.Clear();

            try
            {
                string f4 = Directory.GetFiles(baseDir, "4config*.json").FirstOrDefault();
                if (f4 != null)
                {
                    var cfg4 = JsonTool.FromJson<OriginMir2Config4>(File.ReadAllText(f4), AppJsonContext.Default.OriginMir2Config4);
                    if (cfg4?._task != null)
                        foreach (var kv in cfg4._task)
                            if (int.TryParse(kv.Key, out int id) && kv.Value != null)
                                OriginTasks[id] = kv.Value;
                }

                string f5 = Directory.GetFiles(baseDir, "5config*.json").FirstOrDefault();
                if (f5 != null)
                {
                    var cfg5 = JsonTool.FromJson<OriginMir2Config5>(File.ReadAllText(f5), AppJsonContext.Default.OriginMir2Config5);
                    if (cfg5?.goals != null)
                        foreach (var kv in cfg5.goals)
                            if (int.TryParse(kv.Key, out int id) && kv.Value != null)
                                OriginGoals[id] = kv.Value;
                    if (cfg5?.textstyle != null)
                        foreach (var kv in cfg5.textstyle)
                            if (int.TryParse(kv.Key, out int id) && kv.Value != null)
                                OriginTextStyles[id] = kv.Value;
                }

                string f0 = Directory.GetFiles(baseDir, "0config*.json").FirstOrDefault();
                if (f0 != null)
                {
                    var cfg0 = JsonTool.FromJson<OriginMir2Config0>(File.ReadAllText(f0), AppJsonContext.Default.OriginMir2Config0);
                    if (cfg0?.global != null && cfg0.global.TryGetValue("9201", out var g) && g != null)
                    {
                        // value 形如 "1#[主线]&201#[转生]&..."：按 & 拆项，再按 # 拆 type#前缀。
                        foreach (var item in g.value.Split('&', StringSplitOptions.RemoveEmptyEntries))
                        {
                            var parts = item.Split('#');
                            if (parts.Length >= 2 && int.TryParse(parts[0], out int type))
                                OriginTaskTypeNames[type] = parts[1];
                        }
                    }
                }

                MessageQueue.Enqueue($"[{InstanceTag}] 原始传奇任务权威表: {OriginTasks.Count} 任务 / {OriginGoals.Count} 目标 / {OriginTaskTypeNames.Count} 类型前缀 / {OriginTextStyles.Count} 文字样式（4config._task + 5config.goals + 0config.global + 5config.textstyle）");
            }
            catch (Exception ex)
            {
                MessageQueue.Enqueue("加载原始传奇任务权威表失败: " + ex.Message);
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
