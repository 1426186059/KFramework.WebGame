using Server.MirDatabase;
using Server.MirEnvir;

namespace Server.MirObjects
{
    public class WarriorHero : HeroObject
    {
        public WarriorHero(CharacterInfo info, PlayerObject owner) : base(info, owner) { }
        protected override bool InAttackRange()
        {
            if (Target.CurrentMap != CurrentMap) return false;

            if (Info.Thrusting)
            {
                int x = Math.Abs(Target.CurrentLocation.X - CurrentLocation.X);
                int y = Math.Abs(Target.CurrentLocation.Y - CurrentLocation.Y);

                if (x > 2 || y > 2) return false;

                return (x <= 1 && y <= 1) || (x == y || x % 2 == y % 2);
            }

            return Target.CurrentLocation != CurrentLocation && Functions.InRange(CurrentLocation, Target.CurrentLocation, 1);
        }
        protected override void ProcessFriend()
        {
            if (!CanCast) return;

            if (Target != null)
            {
                MagicInfoRunItem magic = GetMagic(Spell.无极真气);
                if (CanUseMagic(magic) && !HasBuff(BuffType.Rage))
                {
                    BeginMagic(magic.Spell, Direction, ObjectID, CurrentLocation);
                    return;
                }

                magic = GetMagic(Spell.护体神盾);
                if (CanUseMagic(magic) && !HasBuff(BuffType.ProtectionField))
                {
                    BeginMagic(magic.Spell, Direction, ObjectID, CurrentLocation);
                    return;
                }
            }
        }
        protected override void ProcessAttack()
        {
            if (Target == null || Target.Dead) return;
            TargetDistance = Functions.MaxDistance(CurrentLocation, Target.CurrentLocation);

            if (HasMagic(Spell.烈火剑法) && Envir.Time > FlamingSwordTime)
                SpellToggle(Spell.烈火剑法, SpellToggleState.True);
        }
        protected override void Attack()
        {
            if (Target != null && Owner.PMode == PetMode.FocusMasterTarget && !Target.IsAttackTarget(Owner))
            {
                Target = null;
                return;
            }

            if (Target != null && CanAttack)
            {
                Direction = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);
                var behindEnemyLocation = Functions.PointMove(Target.CurrentLocation, Direction, 1);
                var thrustCell = CurrentMap.GetCell(behindEnemyLocation);
                bool ThrustObject = false;

                if (thrustCell.Objects != null && thrustCell.Objects.Count != 0) ThrustObject = true;

                Spell spell = Spell.无;

                if (Info.Thrusting && ((TargetDistance == 2) || (TargetDistance == 1 && ThrustObject == true)))
                {
                    spell = Spell.刺杀剑术;
                }

                if (spell == Spell.无 && Slaying)
                    spell = Spell.攻杀剑术;

                if (spell == Spell.无)
                {
                    if (Info.HalfMoon)
                        spell = Spell.半月弯刀;

                    if (Info.CrossHalfMoon)
                        spell = Spell.双半月;

                    if (TwinDrakeBlade)
                        spell = Spell.双烈火剑;

                    if (FlamingSword)
                        spell = Spell.烈火剑法;
                }

                Attack(Direction, spell);
            }
        }
    }
}
