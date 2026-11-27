using Server.MirDatabase;

namespace Server.MirObjects
{
    public class WizardHero : HeroObject
    {
        public WizardHero(CharacterInfo info, PlayerObject owner) : base(info, owner) { }
        public int SurroundedCount;
        public int TargetSurroundedCount;
        protected override bool InAttackRange()
        {
            if (Target.CurrentMap != CurrentMap) return false;

            if (HasRangedSpell && CanCast)
                return TargetDistance <= ViewRange;

            return Target.CurrentLocation != CurrentLocation && Functions.InRange(CurrentLocation, Target.CurrentLocation, 1);
        }
        protected override void ProcessFriend()
        {
            if (!CanCast) return;

            if (Target != null)
            {
                MagicInfoRunItem magic = GetMagic(Spell.魔法盾);
                if (CanUseMagic(magic) && !HasBuff(BuffType.MagicShield))
                {
                    BeginMagic(magic.Spell, Direction, ObjectID, CurrentLocation);
                    return;
                }

                magic = GetMagic(Spell.魔力增幅);
                if (CanUseMagic(magic) && !HasBuff(BuffType.MagicBooster))
                {
                    BeginMagic(magic.Spell, Direction, ObjectID, CurrentLocation);
                    return;
                }
            }
        }

        protected override void ProcessAttack()
        {
            if (!CanCast || Target == null || Target.Dead) return;
            TargetDistance = Functions.MaxDistance(CurrentLocation, Target.CurrentLocation);
            SurroundedCount = FindAllTargets(2, CurrentLocation).Count;
            TargetSurroundedCount = FindAllTargets(1, Target.CurrentLocation).Count;
            if (!HasRangedSpell) return;

            Direction = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);
            MagicInfoRunItem magic;

            if (InAttackRange())
            {
                if (TargetDistance == 1)
                {
                    if (Target.Level < Level)
                    {
                        magic = GetMagic(Spell.抗拒火环);
                        if (CanUseMagic(magic))
                        {
                            BeginMagic(magic.Spell, Direction, Target.ObjectID, Target.CurrentLocation);
                            return;
                        }
                    }
                }

                if (TargetDistance < 3 && SurroundedCount > 1)
                {
                    magic = GetMagic(Spell.狂雷紫电);
                    if (CanUseMagic(magic))
                    {
                        BeginMagic(magic.Spell, Direction, Target.ObjectID, Target.CurrentLocation);
                        return;
                    }

                    magic = GetMagic(Spell.地狱雷光);
                    if (CanUseMagic(magic))
                    {
                        BeginMagic(magic.Spell, Direction, Target.ObjectID, Target.CurrentLocation);
                        return;
                    }
                }

                if (TargetSurroundedCount > 1)
                {
                    magic = GetMagic(Spell.冰咆哮);
                    if (CanUseMagic(magic))
                    {
                        BeginMagic(magic.Spell, Direction, Target.ObjectID, Target.CurrentLocation);
                        return;
                    }

                    magic = GetMagic(Spell.爆裂火焰);
                    if (CanUseMagic(magic))
                    {
                        BeginMagic(magic.Spell, Direction, Target.ObjectID, Target.CurrentLocation);
                        return;
                    }
                }

                if (Target.Undead == true && Target.Level < Level)
                {
                    magic = GetMagic(Spell.驱魔);
                    if (CanUseMagic(magic))
                    {
                        BeginMagic(magic.Spell, Direction, Target.ObjectID, Target.CurrentLocation);
                        return;
                    }
                }

                magic = GetMagic(Spell.灭天火);
                if (CanUseMagic(magic))
                {
                    BeginMagic(magic.Spell, Direction, Target.ObjectID, Target.CurrentLocation);
                    return;
                }

                magic = GetMagic(Spell.噬血术);
                if (CanUseMagic(magic))
                {
                    BeginMagic(magic.Spell, Direction, Target.ObjectID, Target.CurrentLocation);
                    return;
                }

                magic = GetMagic(Spell.寒冰掌);
                if (CanUseMagic(magic))
                {
                    BeginMagic(magic.Spell, Direction, Target.ObjectID, Target.CurrentLocation);
                    return;
                }

                magic = GetMagic(Spell.雷电术);
                if (CanUseMagic(magic))
                {
                    BeginMagic(magic.Spell, Direction, Target.ObjectID, Target.CurrentLocation);
                    return;
                }

                magic = GetMagic(Spell.大火球);
                if (CanUseMagic(magic))
                {
                    BeginMagic(magic.Spell, Direction, Target.ObjectID, Target.CurrentLocation);
                    return;
                }

                // 原始传奇火球术 spell=12011（原 31 仅作旧引擎残留，服务端已改数据驱动）。
                magic = GetMagic((Spell)12011);
                if (CanUseMagic(magic))
                {
                    BeginMagic(magic.Spell, Direction, Target.ObjectID, Target.CurrentLocation);
                    return;
                }

                magic = GetMagic(Spell.无);
                {
                    return;
                }
            }
            
            magic = GetMagic(Spell.无);
            {
                return;
            }
        }

        protected override void ProcessTarget()
        {
            if (Target != null && Owner.PMode == PetMode.FocusMasterTarget && !Target.IsAttackTarget(Owner))
            {
                Target = null;
                return;
            }

            if (CanMove && ((!CanCast || NextMagicSpell == Spell.无) && Owner.Info.HeroBehaviour == HeroBehaviour.CounterAttack))
            {
                MoveTo(Owner.Back);
                return;
            }

            if (Target == null || !CanAttack) return;

            if (CanCast && NextMagicSpell != Spell.无)
            {
                Magic(NextMagicSpell, NextMagicDirection, NextMagicTargetID, NextMagicLocation);
                NextMagicSpell = Spell.无;
            }            

            if (CanAttack && (!HasRangedSpell && InAttackRange() || NextMagicSpell == Spell.无 && Owner.Info.HeroBehaviour == HeroBehaviour.Attack && TargetDistance == 1))
            {
                Attack();

                if (Target != null && Target.Dead)
                {
                    FindTarget();
                }

                return;
            }

            if (CanMove && (!HasRangedSpell || NextMagicSpell == Spell.无 && Owner.Info.HeroBehaviour == HeroBehaviour.Attack && TargetDistance > 1))
                MoveTo(Target.CurrentLocation);
        }

        private bool HasRangedSpell => Info.Magics.Select(x => x.Spell).Intersect(Globals.RangedSpells).Any();
    }
}
