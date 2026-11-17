using Server.MirDatabase;

namespace Server.MirObjects.Monsters
{
    /// <summary>
    /// 沙虫，AI=35（经典 Mir2 怪物类SandWorm）
    /// </summary>
    public class SandWorm : SpittingSpider
    {
        protected internal SandWorm(MonsterInfo info)
            : base(info)
        {
        }

        protected override void CompleteAttack(IList<object> data)
        {
            MapObject target = (MapObject)data[0];
            int damage = (int)data[1];
            DefenceType defence = (DefenceType)data[2];

            if (target == null || !target.IsAttackTarget(this) || target.CurrentMap != CurrentMap || target.Node == null) return;

            target.Attacked(this, damage, defence);
        }
    }
}
