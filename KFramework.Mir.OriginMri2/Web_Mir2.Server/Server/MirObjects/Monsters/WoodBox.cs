using Server.MirDatabase;

namespace Server.MirObjects.Monsters
{
    /// <summary>
    /// 木箱，AI=158（经典 Mir2 怪物类WoodBox）：特殊对象：死亡掉物
    /// </summary>
    public class WoodBox : MonsterObject
    {
        protected override bool CanMove { get { return false; } }
        protected internal WoodBox(MonsterInfo info)
            : base(info)
        {
        }

        protected override void Attack() { }

        public override void Turn(MirDirection dir) { }

        public override bool Walk(MirDirection dir) { return false; }

        protected override void ProcessRoam() { }

        protected override void ProcessTarget()
        {
        }

        public override void Die()
        {
            ActionList.Add(new DelayedAction(DelayedType.Die, Envir.Time + 300));
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

                if (targets[i].Attacked(this, damage, DefenceType.ACAgility) <= 0) continue;
            }
        }
    }
}
