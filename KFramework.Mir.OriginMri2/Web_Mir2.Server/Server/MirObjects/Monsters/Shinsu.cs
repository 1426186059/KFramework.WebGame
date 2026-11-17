using Server.MirDatabase;
using Server.MirEnvir;
using S = ServerPackets;

namespace Server.MirObjects.Monsters
{
    /// <summary>
    /// 神兽，AI=18（经典 Mir2 怪物类Shinsu）：道士召唤兽，由 SummonShinsu 召唤
    /// </summary>
    public class Shinsu : MonsterObject
    {
        public bool Mode = false;
        public bool Summoned;
        public long ModeTime;

        protected override bool CanAttack
        {
            get
            {
                return base.CanAttack && Mode;
            }
        }

        protected internal Shinsu(MonsterInfo info) : base(info)
        {
            ActionTime = Envir.Time + 1000;
        }

        protected override void ProcessAI()
        {
            if (!Dead && Envir.Time > ActionTime)
            {
                if (Target != null) ModeTime = Envir.Time + 30000;

                if (!Mode && Envir.Time < ModeTime)
                {
                    Mode = true;
                    Broadcast(new S.ObjectShow { ObjectID = ObjectID });
                    ActionTime = Envir.Time + 1000;
                }
                else if (Mode && Envir.Time > ModeTime)
                {
                    Mode = false;
                    Broadcast(new S.ObjectHide { ObjectID = ObjectID });
                    ActionTime = Envir.Time + 1000;
                }
            }

            base.ProcessAI();
        }

        protected override bool InAttackRange()
        {
            if (Target.CurrentMap != CurrentMap) return false;
            if (Target.CurrentLocation == CurrentLocation) return false;

            int x = Math.Abs(Target.CurrentLocation.X - CurrentLocation.X);
            int y = Math.Abs(Target.CurrentLocation.Y - CurrentLocation.Y);

            if (x > 2 || y > 2) return false;


            return (x <= 1 && y <= 1) || (x == y || x % 2 == y % 2);
        }

        protected override void Attack()
        {
            if (!Target.IsAttackTarget(this))
            {
                Target = null;
                return;
            }

            ActionTime = Envir.Time + 300;
            AttackTime = Envir.Time + AttackSpeed;
            ShockTime = 0;

            Direction = Functions.DirectionFromPoint(CurrentLocation, Target.CurrentLocation);
            Broadcast(new S.ObjectAttack { ObjectID = ObjectID, Direction = Direction, Location = CurrentLocation });

            int damage = GetAttackPower(Stats[Stat.MinDC], Stats[Stat.MaxDC]);
            if (damage == 0) return;

            LineAttack(damage, 2);
        }

        public override void Spawned()
        {
            base.Spawned();

            Summoned = true;
        }

        public override Packet GetInfo()
        {
            var packet = (S.ObjectMonster)base.GetInfo();
            // 原始传奇下 Image 必须是正式 MonsterInfo 表的 CDN model 号（神兽=4171）。
            // 旧代码这里强制改回经典 Mir2 枚举 Monster.Shinsu(79)/Shinsu1(80)，
            // 客户端据此去 CDN 拉 model/79，资源不存在→只显示名字没有模型。
            // base.GetInfo() 已经填了 Info.Image，直接保留即可；Mode 的睁眼/闭眼由 ObjectShow/Hide 表达。
            packet.Extra = Summoned;
            return packet;
        }
    }
}
