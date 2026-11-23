// 原始传奇配置分片（0config~5config）权威表的 1:1 DTO，统一放 Server/ExcelConfig 目录管理。
// 风格与 OriginMir2Config4.cs 一致：属性名 = 原键名，用 [JsonPropertyName] 绑定，不做语义改名。
// 数据来源于 Tools/原始传奇-资源提取/原始传奇-资源参考/{i}config10.25341.1.json（权威源，服务器只读不写）。
// 说明：
//  1. 1config.item 是 17166 项的稀疏数字列数组（列号无语义名），1:1 JsonPropertyName 无意义，
//     其语义映射已在 Tools/Mir2ItemConverter + 服务器 ItemInfoTable 完成，故此处不重复建 DTO。
//  2. npcstore / conditions 的单条 DTO（OriginStoreItemTable / OriginConditionTable）已在 OriginMir2Config4.cs。
//  3. Dictionary 的 key 一律用 string：原始配置存在 "undefined" 等非数字 key，用 int 会导致整表反序列化失败。
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Server.MirDatabase
{
    // ===================== 4config._task / 2config._task_tx：任务表 =====================
    // 两表字段集合一致，共用同一 DTO。key = 任务 id。
    public class OriginTaskTable
    {
        [JsonPropertyName("id")] public int id { get; set; }
        [JsonPropertyName("name")] public string name { get; set; } = "";
        [JsonPropertyName("next")] public int next { get; set; }
        [JsonPropertyName("condition")] public string condition { get; set; } = "";
        [JsonPropertyName("goals")] public string goals { get; set; } = "";
        [JsonPropertyName("rewards")] public string rewards { get; set; } = "";
        [JsonPropertyName("mutireward")] public string mutireward { get; set; } = "";
        [JsonPropertyName("reward")] public string reward { get; set; } = "";
        [JsonPropertyName("fnpc")] public int fnpc { get; set; }
        [JsonPropertyName("tnpc")] public int tnpc { get; set; }
        [JsonPropertyName("type")] public int type { get; set; }
        [JsonPropertyName("tip3")] public string tip3 { get; set; } = "";
        [JsonPropertyName("completeinfo")] public string completeinfo { get; set; } = "";
        [JsonPropertyName("taskgroupid")] public int taskgroupid { get; set; }
        [JsonPropertyName("state")] public int state { get; set; }
        [JsonPropertyName("limit")] public int limit { get; set; }
        [JsonPropertyName("star")] public int star { get; set; }
        [JsonPropertyName("mode")] public int mode { get; set; }
        [JsonPropertyName("chuansong")] public int chuansong { get; set; }
        [JsonPropertyName("overchuan")] public int overchuan { get; set; }
        [JsonPropertyName("autopathing")] public int autopathing { get; set; }
        [JsonPropertyName("autofight")] public int autofight { get; set; }
        [JsonPropertyName("autofinsh")] public int autofinsh { get; set; }
        [JsonPropertyName("nofly")] public int nofly { get; set; }
        [JsonPropertyName("showboss")] public string showboss { get; set; } = "";
        [JsonPropertyName("tradebox")] public int tradebox { get; set; }
        // item 字段样本全为 null，保留原始 JsonElement 不丢失信息。
        [JsonPropertyName("item")] public JsonElement item { get; set; }
    }

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
        [JsonPropertyName("descType")] public int descType { get; set; }
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
        [JsonPropertyName("isAuto")] public int isAuto { get; set; }
        [JsonPropertyName("notargeteff")] public int notargeteff { get; set; }
        [JsonPropertyName("tips1")] public string tips1 { get; set; } = "";
        [JsonPropertyName("piaozi")] public string piaozi { get; set; } = "";
    }

    // ===================== 2config.skills_condition：技能等级条件表 =====================
    // 结构为 技能id -> 等级 -> 条件记录。condition 原始为字符串（如 "10100"）。
    public class OriginSkillConditionTable
    {
        [JsonPropertyName("skillId")] public int skillId { get; set; }
        [JsonPropertyName("level")] public int level { get; set; }
        [JsonPropertyName("effLevelID")] public int effLevelID { get; set; }
        [JsonPropertyName("condition")] public string condition { get; set; } = "";
        [JsonPropertyName("rate")] public int rate { get; set; }
        [JsonPropertyName("monHurtAddFix")] public int monHurtAddFix { get; set; }
        [JsonPropertyName("show")] public string show { get; set; } = "";
        [JsonPropertyName("cdTime")] public int cdTime { get; set; }
        [JsonPropertyName("mpCost")] public int mpCost { get; set; }
    }

    // ===================== 2config.box：奖励包表 =====================
    // key = 奖励包 id。items 形如 "6-10-30000#6-12-50000#..."。
    public class OriginBoxTable
    {
        [JsonPropertyName("id")] public int id { get; set; }
        [JsonPropertyName("isShow")] public int isShow { get; set; }
        [JsonPropertyName("items")] public string items { get; set; } = "";
    }

    // ===================== 分片顶层容器 =====================
    // 2config：任务文本表 + 技能等级条件 + 奖励包。
    public class OriginMir2Config2
    {
        [JsonPropertyName("_task_tx")] public Dictionary<string, OriginTaskTable> _task_tx { get; set; } = new();
        [JsonPropertyName("skills_condition")] public Dictionary<string, Dictionary<string, OriginSkillConditionTable>> skills_condition { get; set; } = new();
        [JsonPropertyName("box")] public Dictionary<string, OriginBoxTable> box { get; set; } = new();
    }

    // 5config：目标表 + NPC 摆放 + 技能表。
    public class OriginMir2Config5
    {
        [JsonPropertyName("goals")] public Dictionary<string, OriginGoalTable> goals { get; set; } = new();
        [JsonPropertyName("mapnpc")] public Dictionary<string, OriginMapNpcTable> mapnpc { get; set; } = new();
        [JsonPropertyName("skill")] public Dictionary<string, OriginSkillTable> skill { get; set; } = new();
    }
}
