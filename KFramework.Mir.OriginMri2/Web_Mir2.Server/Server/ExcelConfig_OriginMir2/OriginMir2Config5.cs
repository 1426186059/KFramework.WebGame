// 原始传奇配置分片 5config 的表 DTO（对应 5config10.25341.1.json）。
// 风格：属性名 = 原键名，[JsonPropertyName] 绑定，不做语义改名。
// Dictionary 的 key 一律用 string：原始配置存在 "undefined" 等非数字 key，用 int 会导致整表反序列化失败。
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Server.ExcelConfig
{
    // ===================== 5config.goals：任务目标表 =====================
    // key = 目标 id（如 532000001）。mapId/x/y 原始为字符串，保持 string。
    public class OriginGoalTable
    {
        [JsonPropertyName("id")] public int id { get; set; } // 目标 id
        [JsonPropertyName("type")] public int type { get; set; } // 目标类型（335=击杀 516=学技能 等）
        [JsonPropertyName("count")] public int count { get; set; } // 目标数量
        [JsonPropertyName("parentType")] public int parentType { get; set; } // 父类型
        [JsonPropertyName("goInfo")] public string goInfo { get; set; } = ""; // 目标描述文本（| 分两段，{0}=数量）
        [JsonPropertyName("param")] public string param { get; set; } = ""; // 目标参数（怪物id/物品id 等，# 分隔）
        [JsonPropertyName("link")] public string link { get; set; } = ""; // 关联链接
        [JsonPropertyName("mapId")] public string mapId { get; set; } = ""; // 目标地图 id
        [JsonPropertyName("x")] public string x { get; set; } = ""; // 目标点 x
        [JsonPropertyName("y")] public string y { get; set; } = ""; // 目标点 y
        // 原始配置有 4 条显式 descType:null，用 int? 避免整表反序列化失败
        [JsonPropertyName("descType")] public int? descType { get; set; } // 描述类型（可为 null）
    }

    // ===================== 5config.mapnpc：NPC 摆放表 =====================
    // key = 摆放记录 id。
    public class OriginMapNpcTable
    {
        [JsonPropertyName("id")] public int id { get; set; } // 摆放记录 id
        [JsonPropertyName("npcid")] public int npcid { get; set; } // NPC id
        [JsonPropertyName("mapid")] public int mapid { get; set; } // 地图 id
        [JsonPropertyName("x")] public int x { get; set; } // 坐标 x
        [JsonPropertyName("y")] public int y { get; set; } // 坐标 y
    }

    // ===================== 5config.skill：技能表 =====================
    // key = 技能 id（Spell id）。
    public class OriginSkillTable
    {
        [JsonPropertyName("id")] public int id { get; set; } // 技能 id（Spell）
        [JsonPropertyName("name")] public string name { get; set; } = ""; // 技能名
        [JsonPropertyName("icon")] public string icon { get; set; } = ""; // 图标（原版参考，本地未用）
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

    // ===================== 5config.textstyle：富文本样式表（任务追踪文字颜色来源） =====================
    // 原版 Logic.getCol(id) = cm.textstyle[id].textColor；任务追踪 info0=31(白) / info1=32(绿+下划线) / 标题=28/29(金)。
    public class OriginTextStyleTable
    {
        [JsonPropertyName("id")] public int id { get; set; } // 样式 id（31=白 32=绿+下划线）
        [JsonPropertyName("stroke")] public int stroke { get; set; } // 描边宽度
        [JsonPropertyName("bold")] public int bold { get; set; } // 是否粗体
        [JsonPropertyName("size")] public int size { get; set; } // 字号
        [JsonPropertyName("underline")] public int underline { get; set; } // 是否下划线
        [JsonPropertyName("textColor")] public string textColor { get; set; } = ""; // 文字颜色（无 # 前缀）
        [JsonPropertyName("strokeColor")] public string strokeColor { get; set; } = ""; // 描边颜色
    }

    // ===================== 5config 顶层容器 =====================
    // 已建具体 DTO 的表：goals / mapnpc / skill / textstyle；其余 70 张表由基类 ExtraTables 全量兜底。
    public class OriginMir2Config5 : OriginConfigRoot
    {
        [JsonPropertyName("goals")] public Dictionary<string, OriginGoalTable> goals { get; set; } = new(); // 任务目标表（id → 目标）
        [JsonPropertyName("mapnpc")] public Dictionary<string, OriginMapNpcTable> mapnpc { get; set; } = new(); // NPC 摆放表（id → 摆放点）
        [JsonPropertyName("skill")] public Dictionary<string, OriginSkillTable> skill { get; set; } = new(); // 技能表（id → 技能）
        [JsonPropertyName("textstyle")] public Dictionary<string, OriginTextStyleTable> textstyle { get; set; } = new(); // 富文本样式表（id → 样式）
        // ↓ 工具转换/排查用表，运行时不直接参与逻辑。
        [JsonPropertyName("soundnormal")] public Dictionary<string, Dictionary<string, OriginSoundNormalTable>> soundnormal { get; set; } = new(); // 地图背景音（4组：group → mapId → 音频记录），MapInfoEditor 用
        [JsonPropertyName("skillEffects")] public Dictionary<string, List<OriginSkillEffectTable>> skillEffects { get; set; } = new(); // 技能特效归属（key=技能id或技能id*100+等级 → 特效记录数组），Mir2SkillConverter/动画导出用
        [JsonPropertyName("effect")] public Dictionary<string, OriginEffectTable> effect { get; set; } = new(); // 特效表（2336）：effectId → model，动画导出取名用
        [JsonPropertyName("title")] public Dictionary<string, OriginTitleTable> title { get; set; } = new(); // 称号表（2503）：称号 model 特效取名用
    }
}
