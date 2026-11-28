using System.Drawing;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirNetwork;
using Server.MirObjects.Monsters;
using System.Numerics;
using S = ServerPackets;

namespace Server.MirObjects
{
    // ============================================================================
    // 原始传奇（fugu_20201203）技能效果处理（服务端）
    // 原始 skill id 到引擎 Spell 的路由 + 无专门实现技能的数据驱动通用伤害结算。
    // 数据来源：5config.skill（Aoe/AreaDis/MaxTarget/ReleaseType/HurtDelay）。
    // 飞行/命中特效由 S.ObjectProjectile 回包驱动客户端本地播放，服务端只算伤害。
    // ============================================================================
    public partial class HumanObject
    {
        /// <summary>
        /// 有专门效果实现的原始技能 id 集合（Spell 常量已对齐原始 id，值即 skillId 本身）。
        /// key/value 均为配置表原始 skill id；未列出的技能（怪物/内功连招等）走 OriginMagicCast 通用伤害。
        /// </summary>
        private static readonly Dictionary<int, Spell> OriginEffectSpellBySkillId = new()
        {
            // 战士 Warrior
            // 被动技能（基本剑术11011/攻杀11021）不在此表：服务端按概率被动结算，不进 Magic()。
            // 刺杀11031/半月11041/烈火11061 等即时释放技能也不在此表：EffectSpellOf 返回 Spell.无，
            // 由 OriginMagicCast 按 5config 的 Aoe 结算（半月3x3群、烈火单体），避免路由到无 case 的 switch。
            {11051, Spell.野蛮冲撞},  // 野蛮冲撞 ht5 突进
            {11071, Spell.开天斩}, // 开天斩 ht12 直线DC
            {11081, Spell.逐日剑法}, // 逐日剑法 ht12 直线+突进
            {11101, Spell.狮子吼},      // 狮子吼 ht13 控场
            {11201, Spell.十步一杀},     // 十步一杀 ht8 突进
            // 法师 Wizard
            {12011, Spell.火球术},      // 火球术 ht1 单体（OriginMagicCast 特例：落点释放）
            {12021, Spell.抗拒火环},     // 抗拒火环 ht7 击退
            {12041, Spell.地狱火},      // 地狱火 ht12 直线
            {12051, Spell.雷电术},   // 雷电术 ht1 单体
            {12061, Spell.大火球}, // 大火球 ht1 单体
            {12071, Spell.瞬间移动},      // 瞬间移动 ht1 传送
            {12081, Spell.爆裂火焰},      // 爆裂火焰 ht1 群(3x3)
            {12091, Spell.火墙},      // 火墙 ht4 地面持续
            {12101, Spell.疾光电影},     // 疾光电影 ht12 直线
            {12111, Spell.地狱雷光},  // 地狱雷光 ht1 群(周围)
            {12121, Spell.魔法盾},   // 魔法盾 ht3 自身增益
            {12131, Spell.冰咆哮},      // 冰咆哮 ht1 群(减速)
            {12141, Spell.灭天火},// 灭天火 ht1 单体高伤
            {12151, Spell.流星火雨},  // 流星火雨 ht4 区域持续
            {12161, Spell.狂雷紫电},    // 狂雷紫电 ht4 区域
            {12181, Spell.分身术},     // 分身术 ht3 召唤镜像
            // 道士 Taoist
            {13011, Spell.治愈术},       // 治愈术 ht3 治疗
            // 精神力战法 13021 是被动准确技能，不进 Magic() 施法 switch（同上，避免 cast=false）。
            {13031, Spell.施毒术},     // 施毒术 ht1 绿毒
            {13041, Spell.灵魂火符},  // 灵魂火符 ht1 单体
            {13051, Spell.召唤骷髅},// 召唤骷髅 ht3 召唤
            {13061, Spell.隐身术},        // 隐身术 ht3 自身
            {13071, Spell.集体隐身术},    // 集体隐身术 ht3 群体
            {13081, Spell.幽灵盾},    // 幽灵盾 ht3 群体魔防
            {13091, Spell.神圣战甲术}, // 神圣战甲术 ht3 群体物防
            {13101, Spell.心灵启示},    // 心灵启示 ht13 洞察
            {13111, Spell.困魔咒_13111},   // 困魔咒 ht13 定身
            {13121, Spell.群体治愈术},   // 群体治愈术 ht3 群治
            {13131, Spell.召唤神兽},  // 召唤神兽 ht3 召唤
            {13141, Spell.噬血术},     // 噬血术 ht1 吸血
            {13151, Spell.无极真气},          // 无极真气 ht3 自身增益
            {13181, Spell.召唤骨龙},// 召唤骨龙 ht3 召唤
            {14011, Spell.护体神盾},// 护体神盾 ht13 被动护盾
            {308000, Spell.气功波},// 气功波 ht7 击退
        };

        /// <summary>有专门效果实现的原始技能 id 原样返回，其余返回 Spell.无(0) 走通用施法。</summary>
        private static Spell EffectSpellOf(MagicInfoRunItem magic)
        {
            return magic != null && magic.Info != null && OriginEffectSpellBySkillId.TryGetValue(magic.Info.OriginId, out Spell s) ? s : Spell.无;
        }

        /// <summary>
        /// 原始传奇技能通用施法：对无专门效果实现的技能，按配置表的 Aoe/职业攻击属性结算伤害。
        /// 战士用 DC、法师用 MC、道士用 SC；Aoe=1 走 3x3 群体，否则单体。伤害走 DelayedType.Damage 通用结算。
        /// 治疗/增益类(TargetType=3，如无极真气)不造成伤害，作用于自身并标记施法成功。
        /// </summary>
        private bool OriginMagicCast(MagicInfoRunItem magic, MapObject target, Point location)
        {
            if (magic == null || magic.Info == null)
            {
                return false;
            }

            // 增益/自身类技能：有目标就指向目标，否则自身，不造成伤害。
            if (magic.Info.TargetType == 3)
            {
                return true;
            }

            Stat minStat, maxStat;
            DefenceType defence;
            switch (Info == null ? MirClass.Warrior : Info.Class)
            {
                case MirClass.Wizard:
                    minStat = Stat.MinMC; maxStat = Stat.MaxMC; defence = DefenceType.MAC; break;
                case MirClass.Taoist:
                    minStat = Stat.MinSC; maxStat = Stat.MaxSC; defence = DefenceType.MAC; break;
                default:
                    // 战士技能用 DC 计算伤害，必须走物理防御 AC；用 MAC 会被魔防减免，
                    // 表现为"半月/烈火打怪几乎不掉血"。
                    minStat = Stat.MinDC; maxStat = Stat.MaxDC; defence = DefenceType.AC; break;
            }

            int damage = magic.GetDamage(GetAttackPower(Stats[minStat], Stats[maxStat]));
            int maxTargets = magic.Info.MaxTarget > 0 ? magic.Info.MaxTarget : (magic.Info.Aoe == 1 ? int.MaxValue : 1);

            if (target != null && target.IsAttackTarget(this))
            {
                // 有目标：按 5config 的 Aoe/AreaDis/MaxTarget 结算。
                // areaDis=0（半月弯刀"身前半圆"）以施法者为中心；areaDis>0 以目标为中心。
                long delay = Functions.MaxDistance(CurrentLocation, target.CurrentLocation) * 50 + 500;
                if (magic.Info.Aoe == 1)
                {
                    Point center = magic.Info.AreaDis > 0 ? target.CurrentLocation : CurrentLocation;
                    int aoeRadius = magic.Info.AreaDis > 0 ? magic.Info.AreaDis : 1;
                    List<MapObject> aoeTargets = new List<MapObject>();
                    for (int y = center.Y - aoeRadius; y <= center.Y + aoeRadius; y++)
                    {
                        if (y < 0 || y >= CurrentMap.Height)
                        {
                            continue;
                        }

                        for (int x = center.X - aoeRadius; x <= center.X + aoeRadius; x++)
                        {
                            if (x < 0 || x >= CurrentMap.Width)
                            {
                                continue;
                            }

                            Cell cell = CurrentMap.GetCell(x, y);
                            if (!cell.Valid || cell.Objects == null)
                            {
                                continue;
                            }

                            foreach (MapObject ob in cell.Objects.ToArray())
                            {
                                if (ob == null || !ob.IsAttackTarget(this))
                                {
                                    continue;
                                }

                                if (!aoeTargets.Contains(ob))
                                {
                                    aoeTargets.Add(ob);
                                }
                            }
                        }
                    }
                    foreach (MapObject ob in aoeTargets.Take(maxTargets))
                    {
                        ActionList.Add(new DelayedAction(DelayedType.Damage, Envir.Time + delay, ob, damage, defence, false));
                    }
                }
                else
                {
                    ActionList.Add(new DelayedAction(DelayedType.Damage, Envir.Time + delay, target, damage, defence, false));
                }

                LevelMagic(magic);
                return true;
            }

            // 无目标：原版 releaseType=4（火球术等）是"朝落点释放"，不是"必须选中目标"。
            // 客户端未选目标时发 location=鼠标位置，这里在落点附近找敌对目标结算；
            // 落点没有目标时仍算施法成功（朝空地打），但不刷熟练度。
            if (magic.Info.ReleaseType != 4 || location.X == 0 || location.Y == 0 || CurrentMap == null)
            {
                return false;
            }

            int radius = magic.Info.AreaDis > 0 ? magic.Info.AreaDis : 1;
            List<MapObject> candidates = new List<MapObject>();
            for (int y = location.Y - radius; y <= location.Y + radius; y++)
            {
                if (y < 0 || y >= CurrentMap.Height)
                {
                    continue;
                }

                for (int x = location.X - radius; x <= location.X + radius; x++)
                {
                    if (x < 0 || x >= CurrentMap.Width)
                    {
                        continue;
                    }

                    Cell cell = CurrentMap.GetCell(x, y);
                    if (!cell.Valid || cell.Objects == null)
                    {
                        continue;
                    }

                    foreach (MapObject ob in cell.Objects.ToArray())
                    {
                        if (ob == null || ob == this || !ob.IsAttackTarget(this))
                        {
                            continue;
                        }

                        if (!candidates.Contains(ob))
                        {
                            candidates.Add(ob);
                        }
                    }
                }
            }

            if (candidates.Count == 0)
            {
                return true;
            }

            candidates = candidates.OrderBy(o => Functions.MaxDistance(o.CurrentLocation, location)).ToList();

            long hitDelay = magic.Info.HurtDelay > 0 ? magic.Info.HurtDelay : 500;
            foreach (MapObject ob in candidates.Take(maxTargets))
            {
                ActionList.Add(new DelayedAction(DelayedType.Damage, Envir.Time + hitDelay, ob, damage, defence, false));
            }

            LevelMagic(magic);
            return true;
        }
    }
}
