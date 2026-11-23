// 原始传奇配置分片 5config 的表 DTO（对应 5config10.25341.1.json）。
// 风格：属性名 = 原键名，[JsonPropertyName] 绑定，不做语义改名。
// Dictionary 的 key 一律用 string：原始配置存在 "undefined" 等非数字 key，用 int 会导致整表反序列化失败。
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Server.MirDatabase
{
    // ===================== 5config.goals：任务目标表 =====================
    // key = 目标 id（如 532000001）。mapId/x/y 原始为字符串，保持 string。
    public class OriginGoalTable
    {
        [JsonPropertyName("id")] public int id { get; set; }
        [JsonPropertyName("type")] public int type { get; set; }
        [JsonPropertyName("count")] public int count { get; set; }
        [JsonPropertyName("parentType")] public int parentType { get; set; }
        [JsonPropertyName("goInfo")] public string goInfo { get; set; } = "";
        [JsonPropertyName("param")] public string param { get; set; } = "";
        [JsonPropertyName("link")] public string link { get; set; } = "";
        [JsonPropertyName("mapId")] public string mapId { get; set; } = "";
        [JsonPropertyName("x")] public string x { get; set; } = "";
        [JsonPropertyName("y")] public string y { get; set; } = "";
        // 原始配置有 4 条显式 descType:null，用 int? 避免整表反序列化失败。
        [JsonPropertyName("descType")] public int? descType { get; set; }
    }

    // ===================== 5config.mapnpc：NPC 摆放表 =====================
    // key = 摆放记录 id。
    public class OriginMapNpcTable
    {
        [JsonPropertyName("id")] public int id { get; set; }
        [JsonPropertyName("npcid")] public int npcid { get; set; }
        [JsonPropertyName("mapid")] public int mapid { get; set; }
        [JsonPropertyName("x")] public int x { get; set; }
        [JsonPropertyName("y")] public int y { get; set; }
    }

    // ===================== 5config.skill：技能表 =====================
    // key = 技能 id（Spell id）。
    public class OriginSkillTable
    {
        [JsonPropertyName("id")] public int id { get; set; }
        [JsonPropertyName("name")] public string name { get; set; } = "";
        [JsonPropertyName("icon")] public string icon { get; set; } = "";
        [JsonPropertyName("cls")] public int cls { get; set; }
        [JsonPropertyName("career")] public int career { get; set; }
        [JsonPropertyName("isshow")] public int isshow { get; set; }
        [JsonPropertyName("condition")] public int condition { get; set; }
        [JsonPropertyName("conditionText")] public string conditionText { get; set; } = "";
        [JsonPropertyName("handlerType")] public int handlerType { get; set; }
        [JsonPropertyName("targetType")] public int targetType { get; set; }
        [JsonPropertyName("aoe")] public int aoe { get; set; }
        [JsonPropertyName("maxtarget")] public int maxtarget { get; set; }
        [JsonPropertyName("canup")] public int canup { get; set; }
        [JsonPropertyName("passive")] public int passive { get; set; }
        [JsonPropertyName("action")] public int action { get; set; }
        [JsonPropertyName("hurtDelay")] public int hurtDelay { get; set; }
        [JsonPropertyName("cdTime")] public int cdTime { get; set; }
        [JsonPropertyName("commonCd")] public int commonCd { get; set; }
        [JsonPropertyName("releaseDis")] public int releaseDis { get; set; }
        [JsonPropertyName("releaseDis2")] public int releaseDis2 { get; set; }
        [JsonPropertyName("releaseType")] public int releaseType { get; set; }
        [JsonPropertyName("areaDir")] public int areaDir { get; set; }
        [JsonPropertyName("areaDis")] public int areaDis { get; set; }
        [JsonPropertyName("params")] public int @params { get; set; }
        [JsonPropertyName("index")] public int index { get; set; }
        // 原始配置有 4 条显式 isAuto:null，用 int? 避免整表反序列化失败。
        [JsonPropertyName("isAuto")] public int? isAuto { get; set; }
        [JsonPropertyName("notargeteff")] public int notargeteff { get; set; }
        [JsonPropertyName("tips1")] public string tips1 { get; set; } = "";
        [JsonPropertyName("piaozi")] public string piaozi { get; set; } = "";
    }

    // ===================== 5config.textstyle：富文本样式表（任务追踪文字颜色来源） =====================
    // 原版 Logic.getCol(id) = cm.textstyle[id].textColor；任务追踪 info0=31(白) / info1=32(绿+下划线) / 标题=28/29(金)。
    public class OriginTextStyleTable
    {
        [JsonPropertyName("id")] public int id { get; set; }
        [JsonPropertyName("stroke")] public int stroke { get; set; }
        [JsonPropertyName("bold")] public int bold { get; set; }
        [JsonPropertyName("size")] public int size { get; set; }
        [JsonPropertyName("underline")] public int underline { get; set; }
        [JsonPropertyName("textColor")] public string textColor { get; set; } = "";
        [JsonPropertyName("strokeColor")] public string strokeColor { get; set; } = "";
    }

    // ===================== 5config 顶层容器 =====================
    // 已建具体 DTO 的表：goals / mapnpc / skill / textstyle；其余 70 张表由基类 ExtraTables 全量兜底。
    public class OriginMir2Config5 : OriginConfigRoot
    {
        [JsonPropertyName("goals")] public Dictionary<string, OriginGoalTable> goals { get; set; } = new();
        [JsonPropertyName("mapnpc")] public Dictionary<string, OriginMapNpcTable> mapnpc { get; set; } = new();
        [JsonPropertyName("skill")] public Dictionary<string, OriginSkillTable> skill { get; set; } = new();
        [JsonPropertyName("textstyle")] public Dictionary<string, OriginTextStyleTable> textstyle { get; set; } = new();
    }
}
