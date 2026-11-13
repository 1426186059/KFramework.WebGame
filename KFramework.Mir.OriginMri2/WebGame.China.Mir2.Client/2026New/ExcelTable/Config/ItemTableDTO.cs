// 物品配置表 ItemInfo（原始传奇 1config.item 经 Mir2ItemConverter 转换后的服务器侧物品表，
// 12594 项；JSON 为 PascalCase 顶层数组，位于 Mir2Res/MyRes/ExcelConfig/ItemInfo.json）。
// 与 mapTable 平级，由 ExcelTableMgr.InitializeAsync 预加载，供 GM 面板等需要全量物品的场合直接取用。
// 字段类型与服务器 ItemInfo 一致（IncludeFields=true 反序列化），Stats 直接复用 Shared/Data/Stat.cs 的 Stats 类。
using System.Text.Json.Serialization;


/// <summary>物品配置表 ItemInfo 的单条记录（对应 ItemInfo.json 数组的一个元素）。</summary>
public class ItemTableDTO
{
    // 物品 Index（= 原始传奇 1config.item 的 col0）
    [JsonPropertyName("Index")]
    public int Index { get; set; }

    // 物品名称（中文）
    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";

    // 物品大类
    [JsonPropertyName("Type")]
    public int Type { get; set; }

    // 品质
    [JsonPropertyName("Grade")]
    public int Grade { get; set; }

    [JsonPropertyName("RequiredType")]
    public int RequiredType { get; set; }

    [JsonPropertyName("RequiredClass")]
    public int RequiredClass { get; set; }

    [JsonPropertyName("RequiredGender")]
    public int RequiredGender { get; set; }

    [JsonPropertyName("Set")]
    public int Set { get; set; }

    // 装备部位/形态
    [JsonPropertyName("Shape")]
    public int Shape { get; set; }

    [JsonPropertyName("Weight")]
    public int Weight { get; set; }

    [JsonPropertyName("Light")]
    public int Light { get; set; }

    [JsonPropertyName("RequiredAmount")]
    public int RequiredAmount { get; set; }

    // 图标号（= 原始传奇 col19 大图标 ID）。约 46% 超过 ushort（最大 1.24 亿），
    // 故用 uint 保留原值，避免截断后画错图标。
    [JsonPropertyName("Image")]
    public uint Image { get; set; }

    [JsonPropertyName("Durability")]
    public int Durability { get; set; }

    [JsonPropertyName("Price")]
    public uint Price { get; set; }

    [JsonPropertyName("StackSize")]
    public int StackSize { get; set; }

    [JsonPropertyName("StartItem")]
    public bool StartItem { get; set; }

    [JsonPropertyName("Effect")]
    public int Effect { get; set; }

    [JsonPropertyName("NeedIdentify")]
    public bool NeedIdentify { get; set; }

    [JsonPropertyName("ShowGroupPickup")]
    public bool ShowGroupPickup { get; set; }

    [JsonPropertyName("GlobalDropNotify")]
    public bool GlobalDropNotify { get; set; }

    [JsonPropertyName("ClassBased")]
    public bool ClassBased { get; set; }

    [JsonPropertyName("LevelBased")]
    public bool LevelBased { get; set; }

    [JsonPropertyName("CanMine")]
    public bool CanMine { get; set; }

    [JsonPropertyName("CanFastRun")]
    public bool CanFastRun { get; set; }

    [JsonPropertyName("CanAwakening")]
    public bool CanAwakening { get; set; }

    [JsonPropertyName("Bind")]
    public int Bind { get; set; }

    [JsonPropertyName("Unique")]
    public int Unique { get; set; }

    [JsonPropertyName("RandomStatsId")]
    public int RandomStatsId { get; set; }

    [JsonPropertyName("ToolTip")]
    public string ToolTip { get; set; } = "";

    [JsonPropertyName("Slots")]
    public int Slots { get; set; }

    // 物品属性：与服务器 ItemInfo.Stats 同型（Shared/Data/Stat.cs 的 Stats 类）。
    // Stats.Values 是 SortedDictionary<Stat, int>，JSON 里键为属性枚举名（"HP"/"MinDC"/"MaxDC"...），
    // 反序列化时 System.Text.Json 自动把字符串键转成 Stat 枚举键。
    // 用法与服务器一致：dto.Stats[Stat.MinDC]、dto.Stats[Stat.HP]。
    [JsonPropertyName("Stats")]
    public Stats Stats { get; set; } = new Stats();
}
