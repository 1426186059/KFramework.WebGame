using Server.MirDatabase;
using S = ServerPackets;

namespace Server.MirObjects.Monsters
{
    /// <summary>
    /// 燃烧沃玛，AI=10（经典 Mir2 怪物类FlamingWooma）
    /// </summary>
    public class FlamingWooma : MonsterObject
    {
        protected internal FlamingWooma(MonsterInfoTable info) : base(info)
        {
        }
        
        protected override void Attack()
        {
            if (!Target.IsAttackTarget(this))
            {
                Target = null;
                return;
            }

            ShockTime = 0;

            Direction = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);
            Broadcast(new S.ObjectAttack {ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation});

            ActionTime = Envir.Time + 300;
            AttackTime = Envir.Time + AttackSpeed;

            int damage = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
            if (damage == 0) return;

            DelayedAction action = new DelayedAction(DelayedType.Damage, Envir.Time + 300, Target, damage, DefenceType.MACAgility);
            ActionList.Add(action);
        }
    }
}
