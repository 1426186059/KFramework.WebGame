using Server.MirDatabase;
using Server.MirEnvir;

namespace Server.MirObjects.Monsters
{
    /// <summary>
    /// 抱尸虫，AI=70（经典 Mir2 怪物类Hugger）
    /// </summary>
    public class Hugger : MonsterObject
    {
        public long ExplosionTime;

        protected internal Hugger(MonsterInfoTable info)
            : base(info)
        {
            ExplosionTime = Envir.Time + 1000 * 60 * 5;
        }

        protected override void ProcessTarget()
        {
            if (!CanAttack) return;

            if (Target == null)
            {
                Die(); return;
            }

            if (Envir.Time > ExplosionTime)
            {
                Die(); return;
            }

            if (InAttackRange())
            {
                Attack();

                if (Target.Dead)
                    Die();

                return;
            }

            if (Envir.Time < ShockTime)
            {
                Target = null;
                return;
            }

            MoveTo(Target.CurrentLocation);
        }

        public override void Die()
        {
            ActionList.Add(new DelayedAction(DelayedType.Die, Envir.Time + 500));
            base.Die();
        }

        protected override void CompleteDeath(IList<object> data)
        {
            List<MapObject> targets = FindAllTargets(1, CurrentLocation, false);
            if (targets.Count == 0) return;

            for (int i = 0; i < targets.Count; i++)
            {
                int damage = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
                if (damage == 0) return;

                if (targets[i].Attacked(this, damage, DefenceType.ACAgility) <= 0) return;

                PoisonTarget(targets[i], 5, 5, PoisonType.Green, 2000);
            }
        }

    }
}
