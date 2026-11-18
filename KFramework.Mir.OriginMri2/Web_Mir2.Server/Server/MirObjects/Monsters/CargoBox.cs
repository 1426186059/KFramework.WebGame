using Server.MirDatabase;

namespace Server.MirObjects.Monsters
{
    /// <summary>
    /// 货物箱（经典 Mir2 怪物类CargoBox）：特殊对象：不可移动/攻击
    /// </summary>
    public class CargoBox : MonsterObject
    {
        protected override bool CanMove { get { return false; } }
        protected internal CargoBox(MonsterInfoTable info)
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
    }
}
