public static class Globals
{
    public const string ProductCodename = "Crystal";

    public const int

        MinAccountIDLength = 3,
        MaxAccountIDLength = 15,

        MinPasswordLength = 5,
        MaxPasswordLength = 15,

        MinCharacterNameLength = 3,
        MaxCharacterNameLength = 15,
        MaxCharacterCount = 4,

        MaxChatLength = 80,

        StorageGridSize = 80,

        MaxGroup = 15,

        MaxPets = 5,

        MaxAttackRange = 9,

        MaxDragonLevel = 13,

        ClassWeaponCount = 100,

        FlagIndexCount = 1999,

        MaxConcurrentQuests = 20,

        LogDelay = 10000,

        DataRange = 16;//Was 24

    public static float Commission = 0.05F;

    public const uint SearchDelay = 500,
                      ConsignmentLength = 7,
                      ConsignmentCost = 5000,
                      MinConsignment = 5000,
                      MaxConsignment = 50000000,
                      AuctionCost = 5000,
                      MinStartingBid = 0,
                      MaxStartingBid = 50000;

    public static int[] FishingRodShapes = new int[] { 49, 50 };

    // 原始传奇技能全部用原始 id 作为 Spell 键。远程技能按 5config.skill 的 releaseDis/Aoe 归入。
    public static Spell[] RangedSpells = new Spell[]
    {
        Spell.火球术,
        Spell.雷电术,
        Spell.爆裂火焰,
        Spell.火墙,
        Spell.寒冰掌,
        Spell.噬血术,
        Spell.灭天火,
        Spell.冰咆哮,
        Spell.流星火雨,
        Spell.冰风暴,
        Spell.灵魂火符,
        Spell.直射击,
        Spell.元素射击,
        Spell.毒箭
    };
}