/// <summary>
/// 背包格位布局（客户端侧定义；服务器 Library/InventoryLayout.cs 有一份同名副本，数值必须保持一致）。
///
/// 《原始传奇》初始解锁 54 格（其中 5 个是快捷物品栏格，不属于背包页）。
/// 背包共两页、每页 8 列 x 6 行 = 48 格，全解锁后 5 + 48 x 2 = 101 格。
///
/// 服务器 Info.Inventory / 客户端 User.Inventory 的数组下标即上表中的格位：
///   [0 .. BeltSlots-1]                     快捷物品栏
///   [BeltSlots .. FirstPageEndSlot-1]      背包第一页
///   [FirstPageEndSlot .. MaxSlots-1]       背包第二页（初期多数未解锁）
/// </summary>
public static class InventoryLayout
{
    /// <summary>开局已解锁的格位总数（含快捷物品栏）。</summary>
    public const int InitialSlots = 54;

    /// <summary>快捷物品栏格数，占用数组最前面的下标。</summary>
    public const int BeltSlots = 5;

    public const int BagColumns = 8;
    public const int BagRows = 6;

    /// <summary>每个背包标签页的格数。</summary>
    public const int SlotsPerBagPage = BagColumns * BagRows; // 48

    public const int BagPages = 2;

    /// <summary>两页背包总格数（不含快捷栏）。</summary>
    public const int BagSlots = SlotsPerBagPage * BagPages; // 96

    /// <summary>全部解锁后的数组长度。</summary>
    public const int MaxSlots = BeltSlots + BagSlots; // 101

    /// <summary>第一页结束（不含）的数组下标。</summary>
    public const int FirstPageEndSlot = BeltSlots + SlotsPerBagPage; // 53

    /// <summary>首次扩容一次性增加的格数。</summary>
    public const int InventoryExpandFirstStep = 8;

    /// <summary>之后每次扩容增加的格数。</summary>
    public const int InventoryExpandStep = 4;

    /// <summary>首次扩容价格，之后每级递增同样的金额。</summary>
    public const int InventoryExpandBaseGold = 1000000;
}
