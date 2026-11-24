using Server.MirDatabase;

namespace Server.MirObjects.Monsters
{
    /// <summary>
    /// 复活僵尸，AI=25（经典 Mir2 怪物类RevivingZombie）
    /// </summary>
    public class RevivingZombie : MonsterObject
    {
        public byte RevivalCount;
        public int LifeCount;
        public long RevivalTime, DieTime;

        public override uint Experience
        {
            // 配置已无经验字段：基于等级公式算出的 base.Experience 按复活次数递减。
            get { return (uint)(base.Experience * (100 - (25 * RevivalCount)) / 100); }
        }

        protected internal RevivingZombie(MonsterInfoTable info)
            : base(info)
        {
            RevivalCount = 0;
            LifeCount = Envir.Random.Next(3);
        }

        public override void Die()
        {
            DieTime = Envir.Time;
            RevivalTime = (4 + Envir.Random.Next(20)) * 1000;
            base.Die();
        }

        protected override void ProcessAI()
        {
            if (Dead && Envir.Time > DieTime + RevivalTime && RevivalCount < LifeCount)
            {
                RevivalCount++;

                int newhp = Stats[Stat.HP] * (100 - (25 * RevivalCount)) / 100;
                Revive(newhp, false);
            }

            base.ProcessAI();
        }
    }
}