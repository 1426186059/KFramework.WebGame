using System.Text.Json;

namespace Server.MirEnvir
{
    public partial class Envir
    {
        /// <summary>
        /// 原始传奇技能提示静态索引：从 0config/2config/5config 一次性派生，
        /// 只存运行时只读数据，不写回任何配置表。
        /// </summary>
        public readonly SkillTipIndex SkillTips = new SkillTipIndex();

        public class SkillTipIndex
        {
            private readonly Dictionary<int, Dictionary<int, string>> _attributes = new Dictionary<int, Dictionary<int, string>>();

            public int Count
            {
                get { return _attributes.Count; }
            }

            public string GetAttributes(int originId, int level)
            {
                if (!_attributes.TryGetValue(originId, out Dictionary<int, string> levels))
                {
                    return string.Empty;
                }

                if (!levels.TryGetValue(level + 1, out string text))
                {
                    return string.Empty;
                }

                return text;
            }

            public void Build(OriginMir2Config0 cfg0, OriginMir2Config2 cfg2, OriginMir2Config5 cfg5)
            {
                _attributes.Clear();
                if (cfg0 == null || cfg2 == null || cfg5 == null)
                {
                    return;
                }

                bool hasBuffs = cfg5.ExtraTables.TryGetValue("buff", out JsonElement buffs);
                foreach (var skill in cfg2.skills_condition)
                {
                    if (!int.TryParse(skill.Key, out int skillId) || skill.Value == null)
                    {
                        continue;
                    }

                    var levels = new Dictionary<int, string>();
                    foreach (var condition in skill.Value)
                    {
                        if (!int.TryParse(condition.Key, out int level) || condition.Value == null)
                        {
                            continue;
                        }

                        string text = BuildAttributes(condition.Value, cfg0, hasBuffs ? buffs : default);
                        if (!string.IsNullOrEmpty(text))
                        {
                            levels[level] = text;
                        }
                    }

                    _attributes[skillId] = levels;
                }
            }

            private static string BuildAttributes(OriginSkillConditionTable cond, OriginMir2Config0 cfg0, JsonElement buffs)
            {
                var parts = new List<string>();
                foreach (string token in (cond.buffers ?? string.Empty).Split('#'))
                {
                    if (!int.TryParse(token, out _))
                    {
                        continue;
                    }

                    string txt = GetBuffText(buffs, token);
                    if (string.IsNullOrEmpty(txt))
                    {
                        continue;
                    }

                    string attrName = GetBuffAttrName(txt, cfg0);
                    string line = string.IsNullOrEmpty(attrName) ? txt : attrName + "：" + txt;
                    if (!parts.Contains(line))
                    {
                        parts.Add(line);
                    }
                }

                if (cond.extraHurt > 0)
                {
                    parts.Add(AttrName(cfg0, 15, "增加攻击伤害") + " +" + cond.extraHurt);
                }

                if (cond.rate > 0 && cond.extraHurt > 0)
                {
                    parts.Add("伤害倍率 " + (cond.rate / 100f).ToString("0.##") + "%");
                }
                else if (cond.rate > 0 && parts.Count == 0)
                {
                    parts.Add("触发概率 " + (cond.rate / 100f).ToString("0.##") + "%");
                }

                if (cond.monHurtAdd > 0)
                {
                    parts.Add(AttrName(cfg0, 30, "对怪增伤") + " +" + (cond.monHurtAdd / 100f).ToString("0.##") + "%");
                }

                if (cond.monHurtAddFix > 0)
                {
                    parts.Add(AttrName(cfg0, 27, "对怪伤害") + " +" + cond.monHurtAddFix);
                }

                if (cond.hurt > 0)
                {
                    parts.Add(AttrName(cfg0, 58, "对人伤害") + " +" + cond.hurt);
                }

                return string.Join("；", parts);
            }

            private static string GetBuffText(JsonElement buffs, string id)
            {
                if (buffs.ValueKind != JsonValueKind.Object || !buffs.TryGetProperty(id, out JsonElement buff))
                {
                    return string.Empty;
                }

                if (buff.TryGetProperty("txt", out JsonElement txt) && txt.ValueKind == JsonValueKind.String)
                {
                    return (txt.GetString() ?? string.Empty).Replace("<br>", " ").Replace("<br/>", " ").Trim();
                }

                if (buff.TryGetProperty("txt1", out JsonElement txt1) && txt1.ValueKind == JsonValueKind.String)
                {
                    return (txt1.GetString() ?? string.Empty).Replace("<br>", " ").Replace("<br/>", " ").Trim();
                }

                return string.Empty;
            }

            private static string AttrName(OriginMir2Config0 cfg0, int id, string fallback)
            {
                if (cfg0.attribute_score.TryGetValue(id.ToString(), out OriginAttributeScoreTable attr)
                    && !string.IsNullOrEmpty(attr.description))
                {
                    return attr.description.Replace("\u3000", string.Empty).Trim();
                }

                return fallback;
            }

            private static string GetBuffAttrName(string txt, OriginMir2Config0 cfg0)
            {
                if (txt.Contains("抵挡") || txt.Contains("伤害降低"))
                {
                    return AttrName(cfg0, 16, "免伤");
                }

                if (txt.Contains("魔法防御"))
                {
                    return AttrName(cfg0, 21, "魔防上限");
                }

                if (txt.Contains("物理防御"))
                {
                    return AttrName(cfg0, 7, "防御上限");
                }

                if (txt.Contains("恢复生命"))
                {
                    return AttrName(cfg0, 3, "生命恢复");
                }

                if (txt.Contains("伤害提高") || txt.Contains("伤害加深"))
                {
                    return AttrName(cfg0, 192, "易伤");
                }

                if (txt.Contains("每秒造成"))
                {
                    return AttrName(cfg0, 55, "增加技能伤害");
                }

                if (txt.Contains("无法移动"))
                {
                    return "定身";
                }

                if (txt.Contains("不能进行任何操作"))
                {
                    return "眩晕";
                }

                return string.Empty;
            }
        }
    }
}
