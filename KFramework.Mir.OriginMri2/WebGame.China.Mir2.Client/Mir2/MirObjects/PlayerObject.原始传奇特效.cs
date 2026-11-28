// ============================================================================
// 原始传奇（fugu_20201203）技能特效处理（客户端）
// 数据来自 5config.skillEffects：skills_condition[skill][level].effLevelID → skillEffects。
// 全部特效/音效客户端本地按配置时序播放，服务端施法包只带伤害数据。
// type=0 起手特效、type=1 命中特效、type=2 飞行物（OriginMissile）。
// ============================================================================
using Client.MirSounds;
using WebGame.Mir2.MonoGame.Client;

namespace Client.MirObjects
{
    public partial class PlayerObject : MapObject
    {
        private static bool TryGetOriginSteps(Spell spell, int level, out List<SkillEffectTable> steps)
        {
            steps = null;
            // 原始传奇口径：skills_condition[skill][level].effLevelID → skillEffects[effLevelID]；
            // 等级 0 在 ExcelTableMgr 内部按 1 级取。
            return ExcelTableMgr.TryGetSkillEffects((int)spell, level, out steps) && steps != null && steps.Count > 0;
        }

        /// <summary>起手特效(type=0)：施法瞬间在施法者身上播放。原始传奇 type=0 是角色起手动作，
        /// pos 字段对它无意义（雷电术 type=0 的 pos=1 不是让特效挂到怪物身上）。在施法动作起始帧调用。</summary>
        private bool PlayOriginCastEffects(Spell spell, int level)
        {
            if (!TryGetOriginSteps(spell, level, out var steps))
            {
                return false;
            }

            // JS: SkillBase.release() 里 initEffects() 在技能状态进入时立即执行，delaytime 从这一刻起算。
            _originCastSpell = spell;
            _originCastTime = CMain.Time;

            bool any = false;
            // 战士刀光必须跟随施法朝向；法师/道士特效按 effect 配置固定 dir（原始传奇就是这样）。
            bool followDir = Class == MirClass.Warrior;
            int dir = followDir ? (int)Direction : -1;
            foreach (var s in steps)
            {
                if (s.type != 0 || s.effect <= 0)
                {
                    continue;
                }

                long start = CMain.Time + Math.Max(0, s.delaytime);
                // 原始传奇 JS：addEffect(effect, null, t.x, t.y, ..., u.x, u.y) —— 起手特效画在施法者坐标，
                // 只加 skillEffects 自身的 x/y 像素偏移（配置里通常是 0），不做额外抬升（yOffset=0）。
                int xOffset = s.x, yOffset = -s.y;  // OriginEffect 的 yOffset 语义是“向上”，配置 y 是“向下”
                Effects.Add(new OriginEffect(s.effect, this, starttime: start,
                                             xOffset: xOffset, yOffset: yOffset,
                                             direction: dir, front: followDir ? s.front : 0));
                if (s.sound > 0)
                {
                    SoundManager.PlayOriginSound(s.sound);
                }

                any = true;
            }
            return any || steps.Any(x => x.type == 1 || x.type == 2);
        }

        /// <summary>
        /// 命中/飞行特效(type=1/2)：在施法释放帧(Cast)调用，全部客户端本地播放——与原始传奇 JS 一致：
        /// 所有技能都由本地 skillEffects 时序驱动特效，服务端施法包只带伤害数据。
        /// 有 type=2 时本地建 OriginMissile 子弹（起点抬 SKILL_HitTestY），飞抵后挂 type=1 命中；
        /// 无子弹技能（雷电术）则 type=1 命中按“相对施法起始”的 delaytime 挂到目标（已过时刻则立即播放）。
        /// 灵魂火符走旧 switch 里精心调校的 OriginMissile(653/752→654/754)，这里不重复。
        /// </summary>
        private bool PlayOriginHitEffects(Spell spell, int level, MapObject target, Point targetPoint)
        {
            if (!TryGetOriginSteps(spell, level, out var steps))
            {
                return false;
            }

            // 灵魂火符走旧 switch 里精心调校的 OriginMissile(653/752→654/754)，不在此重复。
            if (spell == Spell.灵魂火符)
            {
                return false;
            }

            var fly = steps.FirstOrDefault(s => s.type == 2 && s.effect > 0);
            var hits = steps.Where(s => s.type == 1 && s.effect > 0).ToList();

            if (fly == null)
            {
                // 原始传奇 JS：无子弹技能 playTargetEffects(0) → addEffect(..., targetX, targetY-0, ..., a.delaytime)。
                // 命中特效画在目标坐标 + 配置 x/y 偏移，不额外抬升（雷电术落雷在目标脚底）。
                // delaytime 从 PlayOriginCastEffects 记录的施法起始时刻起算；这里已经晚于它就直接播放，
                // 不再从动画结束帧重新加 400ms（之前因此施放到命中多等了一截）。
                long baseTime = _originCastSpell == spell && _originCastTime > 0 ? _originCastTime : CMain.Time;
                bool any = false;
                bool followDir = Class == MirClass.Warrior;
                int dir = followDir ? (int)Direction : -1;
                foreach (var s in hits)
                {
                    long start = Math.Max(CMain.Time, baseTime + Math.Max(0, s.delaytime));
                    MapObject owner = s.pos == 0 ? this : (target ?? this);
                    owner.Effects.Add(new OriginEffect(s.effect, owner, starttime: start,
                                                       xOffset: s.x, yOffset: -s.y,
                                                       direction: dir, front: followDir ? s.front : 0));
                    if (s.sound > 0)
                    {
                        SoundManager.PlayOriginSound(s.sound);
                    }

                    any = true;
                }
                return any || steps.Any(x => x.type == 0);
            }

            // 严格照灵魂火符(SoulFireBall)已验证正常的模式：
            //   · 子弹仅用自己 model 的 stand，不做覆盖（火球子弹 485 / 命中 537 本来不同，灵魂火符 485/530 也不同却正常）
            //   · 终点用目标 CurrentLocation（格子坐标），由 OriginMissile 内部按起点抬 60、终点脚底处理
            //   · 命中特效 new OriginEffect(effect, owner)，yOffset=0（灵魂火符就是这样），不额外抬 60
            if (fly.sound > 0)
            {
                SoundManager.PlayOriginSound(fly.sound);
            }

            Point dest = target != null ? target.CurrentLocation : targetPoint;
            OriginMissile missile = new OriginMissile(fly.effect, this, target, dest,
                                                      flightDuration: Math.Max(1, fly.delaytime));
            bool followDirHit = Class == MirClass.Warrior;
            int hitDir = followDirHit ? (int)Direction : -1;
            foreach (var s in hits)
            {
                int hitEffect = s.effect, hitSound = s.sound;
                int hitFront = followDirHit ? s.front : 0;
                MapObject hitOwner = target ?? this;
                missile.Complete += (o, e) =>
                {
                    if (hitOwner.CurrentAction == MirAction.Dead)
                    {
                        return;
                    }

                    hitOwner.Effects.Add(new OriginEffect(hitEffect, hitOwner,
                                                          direction: hitDir, front: hitFront));
                    if (hitSound > 0)
                    {
                        SoundManager.PlaySound(hitSound);
                    }
                };
            }
            Effects.Add(missile);
            return true;
        }
    }
}
