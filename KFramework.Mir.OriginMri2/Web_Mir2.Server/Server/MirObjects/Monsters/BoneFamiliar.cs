using Server.MirDatabase;
using S = ServerPackets;

namespace Server.MirObjects.Monsters
{
    /// <summary>
    /// 骷髅/骨之使魔，AI=23（经典 Mir2 怪物类BoneFamiliar）：召唤兽
    /// </summary>
    public class BoneFamiliar : MonsterObject
    {
        public bool Summoned;

        protected internal BoneFamiliar(MonsterInfoTable info) : base(info)
        {
            Direction = MirDirection.DownLeft;
        }
        
        public override void Spawned()
        {
            base.Spawned();

            Summoned = true;
        }

        public override Packet GetInfo()
        {
            var packet = (S.ObjectMonster)base.GetInfo();
            packet.Extra = Summoned;
            return packet;
        }
    }
}
