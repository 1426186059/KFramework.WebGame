using System.Text.Json.Serialization;

namespace Server.ExcelConfig
{
    // 5config.skill：技能表，key = 技能 id（Spell id）。
    public class OriginSkillTable
    {
        [JsonPropertyName("id")] public Spell id { get; set; } // 技能 id（Spell）
        [JsonPropertyName("name")] public string name { get; set; } = ""; // 技能名
        [JsonPropertyName("icon")] public string icon { get; set; } = ""; // 图标
        [JsonPropertyName("cls")] public int cls { get; set; } // 职业（1战 2法 3道）
        [JsonPropertyName("career")] public int career { get; set; } // 职业（备用）
        [JsonPropertyName("isshow")] public int isshow { get; set; } // 是否玩家可见（1=可见）
        [JsonPropertyName("condition")] public int condition { get; set; } // 学习条件（10000+等级）
        [JsonPropertyName("conditionText")] public string conditionText { get; set; } = ""; // 条件显示文本
        [JsonPropertyName("handlerType")] public int handlerType { get; set; } // 处理器类型
        [JsonPropertyName("targetType")] public int targetType { get; set; } // 目标类型（3=自身增益）
        [JsonPropertyName("aoe")] public int aoe { get; set; } // 是否群攻
        [JsonPropertyName("maxtarget")] public int maxtarget { get; set; } // 最大目标数
        [JsonPropertyName("canup")] public int canup { get; set; } // 可否升级
        [JsonPropertyName("passive")] public int passive { get; set; } // 是否被动
        [JsonPropertyName("action")] public int action { get; set; } // 动作 id
        [JsonPropertyName("hurtDelay")] public int hurtDelay { get; set; } // 伤害延迟
        [JsonPropertyName("cdTime")] public int cdTime { get; set; } // 冷却时间
        [JsonPropertyName("commonCd")] public int commonCd { get; set; } // 公共冷却
        [JsonPropertyName("releaseDis")] public int releaseDis { get; set; } // 施法距离
        [JsonPropertyName("releaseDis2")] public int releaseDis2 { get; set; } // 施法距离2
        [JsonPropertyName("releaseType")] public int releaseType { get; set; } // 施法类型
        [JsonPropertyName("areaDir")] public int areaDir { get; set; } // 区域方向
        [JsonPropertyName("areaDis")] public int areaDis { get; set; } // 区域距离
        [JsonPropertyName("params")] public int @params { get; set; } // 技能参数
        [JsonPropertyName("index")] public int index { get; set; } // 技能索引
        // 原始配置有 4 条显式 isAuto:null，用 int? 避免整表反序列化失败
        [JsonPropertyName("isAuto")] public int? isAuto { get; set; } // 是否自动（可为 null）
        [JsonPropertyName("notargeteff")] public int notargeteff { get; set; } // 无目标特效
        [JsonPropertyName("tips1")] public string tips1 { get; set; } = ""; // 提示文本
        [JsonPropertyName("piaozi")] public string piaozi { get; set; } = ""; // 飘字文本
    }
}
