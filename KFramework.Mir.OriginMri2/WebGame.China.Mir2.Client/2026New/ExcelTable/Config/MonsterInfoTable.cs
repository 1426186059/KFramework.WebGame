// 怪物配置表 MonsterInfo.json 的完整客户端映射（服务端 Web_Mir2.Server.Version/ExcelConfig/MonsterInfo.json
// 与客户端 Mir2Res/MyRes/ExcelConfig/MonsterInfo.json 同步，2773 项）。
// 字段与服务端 MonsterInfo 一一对应；新增的 WalkAudio/AttackAudio/DieAudio 来自原始传奇
// 4config.monster 的 walkaudio/attackaudio/dieaudio（按 model=Image 映射，CDN audio/{id}.mp3）。
// 由 ExcelTableMgr 从本地资源服务器加载，供 MonsterObject 取怪物专属战斗音效。
// AOT：在 JsonTool.AppJsonContext 注册 MonsterInfoTable 与 List<MonsterInfoTable>。
using System.Text.Json.Serialization;

/// <summary>怪物配置表 MonsterInfo.json 的单条记录（完整映射服务端 MonsterInfo 字段）。</summary>
public class MonsterInfoTable
{
    [JsonPropertyName("Index")]
    public int Index { get; set; }

    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("Image")]
    public int Image { get; set; }

    [JsonPropertyName("AI")]
    public byte AI { get; set; }

    [JsonPropertyName("Effect")]
    public byte Effect { get; set; }

    [JsonPropertyName("ViewRange")]
    public byte ViewRange { get; set; }

    [JsonPropertyName("CoolEye")]
    public byte CoolEye { get; set; }

    [JsonPropertyName("Level")]
    public ushort Level { get; set; }

    [JsonPropertyName("Light")]
    public byte Light { get; set; }

    [JsonPropertyName("AttackSpeed")]
    public ushort AttackSpeed { get; set; }

    [JsonPropertyName("MoveSpeed")]
    public ushort MoveSpeed { get; set; }

    [JsonPropertyName("Experience")]
    public uint Experience { get; set; }

    // 原始传奇怪物音效 id（0=未配置，回退通用音）
    [JsonPropertyName("WalkAudio")]
    public int WalkAudio { get; set; }

    [JsonPropertyName("AttackAudio")]
    public int AttackAudio { get; set; }

    [JsonPropertyName("DieAudio")]
    public int DieAudio { get; set; }

    [JsonPropertyName("DropPath")]
    public string DropPath { get; set; } = "";

    [JsonPropertyName("DropSettings")]
    public List<MonsterDropSettingTable> DropSettings { get; set; } = new List<MonsterDropSettingTable>();

    // 服务端此字段全为空数组，客户端不引入复杂 ItemInfo/GroupDropInfo 类型
    [JsonPropertyName("Drops")]
    public List<object> Drops { get; set; } = new List<object>();

    [JsonPropertyName("CanTame")]
    public bool CanTame { get; set; }

    [JsonPropertyName("CanPush")]
    public bool CanPush { get; set; }

    [JsonPropertyName("AutoRev")]
    public bool AutoRev { get; set; }

    [JsonPropertyName("Undead")]
    public bool Undead { get; set; }

    [JsonPropertyName("CanRecall")]
    public bool CanRecall { get; set; }

    [JsonPropertyName("IsBoss")]
    public bool IsBoss { get; set; }

    [JsonPropertyName("HasSpawnScript")]
    public bool HasSpawnScript { get; set; }

    [JsonPropertyName("HasDieScript")]
    public bool HasDieScript { get; set; }

    [JsonPropertyName("Stats")]
    public Stats Stats { get; set; } = new Stats();
}

/// <summary>MonsterInfo.DropSettings 的单条（对应服务端 MonsterInfo.MonsterDropSetting）。</summary>
public class MonsterDropSettingTable
{
    [JsonPropertyName("Chance")]
    public int Chance { get; set; }

    [JsonPropertyName("ItemIndex")]
    public int ItemIndex { get; set; }

    [JsonPropertyName("Gold")]
    public uint Gold { get; set; }

    [JsonPropertyName("QuestRequired")]
    public bool QuestRequired { get; set; }

    [JsonPropertyName("Type")]
    public byte Type { get; set; }

    [JsonPropertyName("Random")]
    public bool Random { get; set; }

    [JsonPropertyName("First")]
    public bool First { get; set; }

    [JsonPropertyName("ItemIndexes")]
    public List<int> ItemIndexes { get; set; } = new List<int>();
}
