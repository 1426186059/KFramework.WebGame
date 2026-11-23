#nullable enable

// 1config.item：原始传奇物品表（17166 条，key=物品id，值=稀疏列对象 {"列号": 值}）。
// 字段名/类型依据：js 目录全部 12 个 JS 的逐列访问 + 0config~5config 跨表核验 + Mir2ItemConverter 映射；
// 与 Mir2ReverseWeb 的 _itemFieldOverrides 有 3 处冲突（col12/18/22），以 JS 实测为准。
// 注意 col21/22/26 是「按子类型多义」列（同一列在不同 c21 下含义不同），已用中性名并在各行注明。
// 稀疏数值列用 int?/uint?：JSON 缺列或 null 时为 null。
using System.Text.Json.Serialization;

namespace Server.ExcelConfig
{
    public class OriginItemTable
    {
        // 列 0：int，全表必有。物品唯一 id（Index），JS 中 cm.item 的 key 即此值。
        [JsonPropertyName("0")] public int Id { get; set; }

        // 列 1：int，全表必有，1~9。物品大类：1=货币/计数，2=装备，3=道具/材料（JS: 2==o[1] 判定装备）。
        [JsonPropertyName("1")] public int ItemClass { get; set; }

        // 列 2：string，全表必有。物品内部名，4572 条为「没用」废弃项；JS 公告/聊天显示的是 o[2]。
        [JsonPropertyName("2")] public string Name { get; set; } = "";

        // 列 3：string，15493 条有值。物品外显名（JS: tips 标题用 r[3] 套 r[9] 颜色、掉落名用 i[3]），4824 条与列 2 不同。
        [JsonPropertyName("3")] public string? DisplayName { get; set; }

        // 列 4：int?（26 条 null），1~55。品级/阶级（JS: 八卦套装按 o[4] 筛选）；转换器 -> Grade。
        [JsonPropertyName("4")] public int? Grade { get; set; }

        // 列 5：string，16246 条有值。中文类别串（"武器"/"宝箱"…）；转换器 -> ItemType。
        [JsonPropertyName("5")] public string? Category { get; set; }

        // 列 7：int，15553 条有值，1=普通、5=特殊级（仅黄金屠龙等 3 件，描述写明「特殊级」）；JS 不直接读。
        [JsonPropertyName("7")] public int? SpecialGradeFlag { get; set; }

        // 列 8：int，1989 条有值。物品图鉴/套装分类 id（JS: 999=普通装备、25=图鉴鱼、9=抽卡物、20~23=首饰位）。
        [JsonPropertyName("8")] public int? DisplayMode { get; set; }

        // 列 9：int，17165 条有值，0~60。名称颜色索引（JS: Html.ITEM_COLORS_ARR[o[9]]）；转换器 -> Quality。
        [JsonPropertyName("9")] public int ColorIndex { get; set; }

        // 列 10：string，8690 条有值，形如 "2#90001"：频道#条件id（90001=conditions「开服第1天」）；JS 寻宝播报按此过滤。
        [JsonPropertyName("10")] public string? ShowCondition { get; set; }

        // 列 11：string，37 条有值。分解提供的经验（JS: showPro 里 t+=Number(cm.item[lid][11])，与 gd.sevenstars.exp 相加）。
        [JsonPropertyName("11")] public string? DecomposeExp { get; set; }

        // 列 12：string，2810 条有值，形如 "8684#20"：回收产出 物品id#数量，多段用 & 分隔（JS: RecycleTipsRender）。
        [JsonPropertyName("12")] public string? RecycleReward { get; set; }

        // 列 13：string，585 条有值，形如 "50370001#1#10000"：宝箱id#数量#概率（跨表 2config.box 同 id 的 items 一一对应）。
        [JsonPropertyName("13")] public string? BoxReward { get; set; }

        // 列 14：int，10333 条有值。等价物品 id（JS: 与 o[0] 的持有数量合并计算）。
        [JsonPropertyName("14")] public int? EquivalentItemId { get; set; }

        // 列 15：int，2033 条有值。获取途径 id（JS: cm.wayGeter[o[15]]，5config.wayGeter.716=天神腰带[一阶]）。
        [JsonPropertyName("15")] public int? WayGetId { get; set; }

        // 列 16：int，17055 条有值。堆叠上限（JS: t[16]>1 才合并同类物品计数）；转换器 -> StackSize。
        [JsonPropertyName("16")] public int? StackLimit { get; set; }

        // 列 17：string，963 条有值，形如 "9501/9502"。外观模型（男/女），Mir2OriginModelResolver 使用。
        [JsonPropertyName("17")] public string? OriginModel { get; set; }

        // 列 18：string，323 条有值，形如 "50029/50029"。装备特效模型号（男/女）（JS: getEquipEffectId → getModleBySex）。
        [JsonPropertyName("18")] public string? EffectModel { get; set; }

        // 列 19：int，17165 条有值，1~1240102131。图标 id（JS: ResUrl.url(o[19],8)）；服务器 Image 已扩为 uint。
        [JsonPropertyName("19")] public uint IconId { get; set; }

        // 列 20：int，17165 条有值，与列 19 几乎恒等（大图标号同源），保留原列便于对比。
        [JsonPropertyName("20")] public uint IconIdMirror { get; set; }

        // 列 21：int，16858 条有值。子类型 id，决定使用逻辑：0=货币(9075)、4=幻武(2779)、2=红包宝袋(2170)、228=刺杀(831)、1=货币兑换(275)、216=技能书(255)、66=宝石(220)、198=天神阶(200)、222=宝箱(118)、136=宠物(88)、600=龙纹(61)、266=限时道具、211/230=用时放特效。
        [JsonPropertyName("21")] public int SubType { get; set; }

        // 列 22：string，4709 条有值（852 纯数值 + 3857 "A#B"，部分用 ; 分段）。按 c21 子类型多义的参数串：216=技能书"技能id#等级"、198=阶数、222=宝箱"id#数量;…"、11=徽章"id#分钟"、204=禁闭时长(时)、236=灵宠时长(分)、136=宠物id、600=龙纹subtype、洪荒=conditions条件id。
        [JsonPropertyName("22")] public string? UseParam { get; set; }

        // 列 23：int，4127 条有值。装备部位 id（JS: gd.player.equipInfo[o[23]]；31~42=衣服、407=疗伤石）。
        [JsonPropertyName("23")] public int? EquipSlot { get; set; }

        // 列 24：int?（30 条 null），1~700。需要等级（JS: 「需要等级」）；转换器 -> RequiredAmount。
        [JsonPropertyName("24")] public int? RequiredLevel { get; set; }

        // 列 25：int，1578 条有值。需要转生等级（JS: Logic.getReinLvStr(o[25])）。
        [JsonPropertyName("25")] public int? RequiredReinLevel { get; set; }

        // 列 26：string，2886 条有值，形如 "1#501#0#0#0"。功能开启配置串，首段为分支类型码（JS checkLinkOpen2）：1=按 UI 编号开界面、2=随机传送到某 NPC、3=直接获得物品。
        [JsonPropertyName("26")] public string? UnlockCondition { get; set; }

        // 列 27：int，48 条有值。消耗冷却标记（JS: o[27] → itemCDs + sendNotif(667,{type:2})）。
        [JsonPropertyName("27")] public int? ConsumeCDFlag { get; set; }

        // 列 28：int，23 条有值恒为 1。特殊功能道具标记（有值者恰为 6 个神宠蛋 + 17 个屠魔勋章）；JS 不直接读。
        [JsonPropertyName("28")] public int? SpecialFunctionFlag { get; set; }

        // 列 29：int，38 条有值。每日限购/可刷新次数（JS: 与 itemSurplusNum 比对显示「今日已刷新 x/y」）。
        [JsonPropertyName("29")] public int? DailyLimit { get; set; }

        // 列 30：int，6 条有值恒为 1。可丢弃标志（JS: 1==config[30] → btn_delete.visible=true）。
        [JsonPropertyName("30")] public int? CanDiscard { get; set; }

        // 列 31：int，39 条有值恒为 1。可直接结算的成长道具标记（血龙魔晶/宝石碎片/经验/月卡等）；JS 不直接读。
        [JsonPropertyName("31")] public int? AutoSettleGrowthFlag { get; set; }

        // 列 32：int，416 条有值，1/3。使用数量模式（JS: 1=用 1 个，3=数量>1 弹批量选择框）。
        [JsonPropertyName("32")] public int? UseCountMode { get; set; }

        // 列 33：int，4937 条有值，0/1。获得时自动使用标志（JS: 1==config[33]&&id!=405 → sendReqUseItem(count,…)）。
        [JsonPropertyName("33")] public int? AutoUseOnGain { get; set; }

        // 列 35：int，14912 条有值。物品排序权重（JS: Logic.a(config,35) 作列表 compare() 的末级排序键，共 24 处）。
        [JsonPropertyName("35")] public int? SortWeight { get; set; }

        // 列 37：int，17097 条有值，60/40/120。展示分组档位（与 col35 强相关：有值组 87% 有 c35）；JS 不直接读。
        [JsonPropertyName("37")] public int DisplayGroup { get; set; }

        // 列 39：int，4092 条有值。装备评分/战力（JS: getAllPower 里 a+=item[39]；回收时与 checkNbvalue 比较）。
        [JsonPropertyName("39")] public int? PowerValue { get; set; }

        // 列 40：string，6779 条有值，形如 "4#1111|5#3333"。属性串（JS: getTipsAttr(o[40],…)；id 全部落在 attribute_score）。
        [JsonPropertyName("40")] public string? Attributes { get; set; }

        // 列 41：string，300 条有值。Buff id 列表（JS: cm.buff[c].nbvalue 累加战力）。
        [JsonPropertyName("41")] public string? BuffIds { get; set; }

        // 列 42：string，11663 条有值，含 <font>/<br>。描述文本（JS: txt_desc.textFlow=o[42]）；转换器 -> ToolTip。
        [JsonPropertyName("42")] public string? Description { get; set; }

        // 列 43：string，6 条有值，形如 "499#100"。限时售价 物品id#数量（JS: t[43] 才显示「出售」按钮）。
        [JsonPropertyName("43")] public string? SellPrice { get; set; }

        // 列 44：string，94 条有值，形如 "1#1440"/"2#2025-11-30-00-00-00"。有效期/到期时间配置串；JS 不直接读。
        [JsonPropertyName("44")] public string? ValidityString { get; set; }

        // 列 45：int，2186 条有值，444~898。关联图鉴等级 id（跨表 2config.tujian.444=火龙神9级、893~898=半兽人统帅24~29级）。
        [JsonPropertyName("45")] public int? TujianLevelId { get; set; }

        // 列 48：string，29 条有值。满阶属性串（同列 40 编码，至尊神器数值为同物品 col40 的 1.5 倍）。
        [JsonPropertyName("48")] public string? MaxAttributes { get; set; }

        // 列 49：int，20 条有值，30/90/270/810/2430。魂印/法魂/面甲/魂器 5 阶评分（与 col39 战力同步递增）；JS 不直接读。
        [JsonPropertyName("49")] public int? SoulStageScore { get; set; }

        // 列 50：string，1644 条有值，形如 "9#0#1"。拍卖行渠道/价格规则（与列 51 配套，首段恒为 1/2/3/7/8/9）。
        [JsonPropertyName("50")] public string? AuctionChannelRule { get; set; }

        // 列 51：string，1644 条有值。拍卖行上架参数，多段用 & 分隔（JS: getItemPaimaiparam 取 [1]时长/[4]价/[5]分钟/[6]保护/[7]货币）。
        [JsonPropertyName("51")] public string? AuctionParams { get; set; }

        // 列 52：int，484 条有值，0~3。职业 1=战/2=法/3=道（JS: sameCareer(t[52]) 管技能书/穿戴/回收）。
        [JsonPropertyName("52")] public int? Career { get; set; }

        // 列 54：string，284 条有值。基础属性串（同列 40 编码，数值与 col39 战力同步）。
        [JsonPropertyName("54")] public string? BaseAttributes { get; set; }

        // 列 56：int，374 条有值，1/2。性别限制（JS: 不符提示「角色性别不符」，回收筛选也用）；转换器 -> RequiredGender。
        [JsonPropertyName("56")] public int? Gender { get; set; }

        // 列 57：int，2118 条有值。持久/耐久（JS: tips 输出「持　　久：W/W」）。
        [JsonPropertyName("57")] public int? Durability { get; set; }

        // 列 58：string，851 条有值，形如 "499#2000000"。合成/使用消耗 货币id#数量（JS: showCost 比对持有量）。
        [JsonPropertyName("58")] public string? ConsumeCost { get; set; }

        // 列 59：int，1051 条有值，1~3。装备职业限制（JS: !r[59]||r[59]==gd.player.career 才可穿/回收）。
        [JsonPropertyName("59")] public int? EquipCareer { get; set; }

        // 列 60：string，579 条有值，形如 "13#19200"。回收声望 货币id#数量（13=声望、58=鸿蒙碎晶，与 col13 宝箱产出对应）。
        [JsonPropertyName("60")] public string? RecycleReputation { get; set; }

        // 列 61：int，321 条有值，10701~130212。场景特效 id（JS: sceneEffect.init(n.preGro,o[61])）。
        [JsonPropertyName("61")] public int? SceneEffectId { get; set; }

        // 列 62：int，208 条有值恒为 1。可投保标志（JS: !config[62] 的装备被过滤出投保列表）。
        [JsonPropertyName("62")] public int? CanInsure { get; set; }

        // 列 63：string，199 条有值，含 <font>。附加提示文本（JS: txt_tips2.textFlow=Html.toEle(o[63])）。
        [JsonPropertyName("63")] public string? TipsText { get; set; }
    }
}
