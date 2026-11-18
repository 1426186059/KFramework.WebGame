using Server.MirDatabase;
using S = ServerPackets;

namespace Server.MirObjects.Monsters
{
    /// <summary>
    /// 地狱骑士，AI=97（经典 Mir2 怪物类HellKnight）
    /// </summary>
    public class HellKnight : MonsterObject
    {
        public bool Summoned;
        public HellLord Lord;

        protected internal HellKnight(MonsterInfoTable info)
            : base(info)
        {
        }

        public override void Spawned()
        {
            base.Spawned();

            Summoned = true;
        }

        public override void Die()
        {
            if(Lord != null)
            {
                Lord.KnightKilled();
            }

            base.Die();
        }

        public override Packet GetInfo()
        {
            var packet = (S.ObjectMonster)base.GetInfo();
            packet.Extra = Summoned;
            return packet;
        }
    }
}
