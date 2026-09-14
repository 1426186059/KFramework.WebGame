namespace KFramework.Example3
{
    internal enum TileCollision
    {
        /// <summary>
        /// 可通行瓦片：完全不会阻碍玩家移动的瓦片（空气、背景装饰）。
        /// </summary>
        Passable = 0,

        /// <summary>
        /// 不可通行瓦片：完全不允许玩家穿过的瓦片，是实心的。
        /// 包括普通砖块、问号砖块、石头等。
        /// </summary>
        Impassable = 1,

        /// <summary>
        /// 单向平台瓦片：从下方可以跳上去，跳到上面可以当地面踩上去行走。
        /// 从侧面和下方不会阻挡玩家。
        /// </summary>
        Platform = 2,

        /// <summary>
        /// 可破坏瓦片：行为类似实心瓦片，但当玩家在它下方
        /// 向上跳跃撞击时，该瓦片会被打碎/消失。
        /// </summary>
        Breakable = 3,

        /// <summary>
        /// 致死瓦片：玩家接触后立即死亡，无伤害过程。
        /// 用于尖刺、岩浆、悬崖底部等。
        /// </summary>
        Lethal = 4,

        /// <summary>
        /// 弹跳瓦片：玩家从上方踩上去会被弹起，弹跳力度可配置。
        /// 用于弹簧、弹跳蘑菇等。
        /// </summary>
        Bouncy = 5,

        /// <summary>
        /// 伤害瓦片：玩家接触后受到伤害但不立即死亡，
        /// 附带短暂无敌时间防止连续扣血。
        /// 用于毒液、小刺等。
        /// </summary>
        Damage = 6,

        /// <summary>
        /// 斜坡瓦片：角色可以沿斜面行走，碰撞检测时
        /// 根据角色X坐标计算对应的Y高度。
        /// </summary>
        Slope = 7,

        /// <summary>
        /// 可攀爬瓦片：玩家可以上下攀爬，不受重力影响。
        /// 用于梯子、藤蔓等。
        /// </summary>
        Climbable = 8,

        /// <summary>
        /// 水域瓦片：玩家进入后改变移动方式，
        /// 速度降低、受浮力影响、跳跃方式改变。
        /// </summary>
        Water = 9,

        /// <summary>
        /// 传送带瓦片：自动沿指定方向推动站在上面的玩家。
        /// 可配置方向和速度。
        /// </summary>
        Conveyor = 10,

        /// <summary>
        /// 打滑瓦片：摩擦力极低，玩家在上面难以停下，
        /// 会像踩在冰面上一样滑行。
        /// </summary>
        Slippery = 11,

        Exit = 12,
    }
}