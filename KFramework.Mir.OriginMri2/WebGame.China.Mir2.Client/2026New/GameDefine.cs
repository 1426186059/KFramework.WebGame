namespace Client
{
    /// <summary>
    /// 与原始传奇(fugu_20201203) GameDefine 对齐的全局常量。
    /// </summary>
    public static class GameDefine
    {
        /// <summary>
        /// 技能飞行子弹的命中测试/手部高度偏移（屏幕像素）。
        /// JS: addSkillBullet(t.x, t.y - SKILL_HitTestY, ...)，子弹起点与终点都按此值抬高。
        /// 客户端 CellHeight=32，与原始传奇 MAP_GRID_HEIGHT=32 一致，可直接作为屏幕像素偏移。
        /// </summary>
        public const int SKILL_HitTestY = 60;
    }
}
