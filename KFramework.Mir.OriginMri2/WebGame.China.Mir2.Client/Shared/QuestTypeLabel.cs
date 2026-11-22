using System.Collections.Generic;

/// <summary>
/// 原始传奇（页游 fugu_20201203）任务类型前缀，等价于客户端
/// Logic.getTaskTypeName(type) / CommonUtils.getTaskTypeName(type)：
/// 从 global[9201].value 里按 type 取 "type#[前缀]#可选参数" 的第 1 段。
///
/// 原版 global[9201]：
/// 1#[主线]&amp;201#[转生]&amp;106#[支线]&amp;107#[支线]&amp;7#[守护]&amp;2#[经验]&amp;3#[日常]
/// &amp;4#[捐献]&amp;5#[威名]&amp;8#[登录]&amp;101..105#[支线]&amp;108#[支线]&amp;109#[支线]
/// &amp;110#[推荐]&amp;900#[蛮荒]&amp;120#[神兽]&amp;130#[内功]
///
/// 注意：这里的键是「任务类型编号」，不是本工程 QuestType 枚举的命名（例如 2 在本工程
/// 叫 ChuMo，但原始传奇配置里对应的是 [经验]），所以严格按编号映射，忠于原版。
/// 原版 900#[蛮荒] 超出本工程 byte 枚举范围，无法表示，故不映射。
/// </summary>
public static class QuestTypeLabel
{
    private static readonly Dictionary<QuestType, string> Labels = new Dictionary<QuestType, string>
    {
        { QuestType.Main, "[主线]" },
        { (QuestType)2, "[经验]" },
        { (QuestType)3, "[日常]" },
        { (QuestType)4, "[捐献]" },
        { (QuestType)5, "[威名]" },
        { QuestType.ShouHu, "[守护]" },
        { QuestType.WdLogin, "[登录]" },
        { QuestType.Xuanshang101, "[支线]" },
        { QuestType.Xuanshang102, "[支线]" },
        { QuestType.Xuanshang103, "[支线]" },
        { QuestType.Xuanshang104, "[支线]" },
        { QuestType.Xuanshang105, "[支线]" },
        { QuestType.Xuanshang106, "[支线]" },
        { QuestType.Xuanshang107, "[支线]" },
        { QuestType.Xuanshang108, "[支线]" },
        { QuestType.Xuanshang109, "[支线]" },
        { QuestType.Xuanshang110, "[推荐]" },
        { QuestType.Xuanshang120, "[神兽]" },
        { QuestType.JiuShen, "[内功]" },
        { QuestType.Xuanshang201, "[转生]" },
    };

    /// <summary>取该类型在原始传奇里显示的前缀文字，无配置时返回空串。</summary>
    public static string Get(QuestType type)
    {
        return Labels.TryGetValue(type, out string label) ? label : string.Empty;
    }

    /// <summary>把类型前缀拼到任务名前（原版渲染：前缀 + 任务名）。</summary>
    public static string Decorate(QuestType type, string name)
    {
        string label = Get(type);
        string text = name ?? string.Empty;
        return string.IsNullOrEmpty(label) ? text : label + " " + text;
    }
}
