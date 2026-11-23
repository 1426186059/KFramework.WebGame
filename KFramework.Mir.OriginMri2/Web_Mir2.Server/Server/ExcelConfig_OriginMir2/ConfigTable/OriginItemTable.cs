#nullable enable

// 1config.item：原始传奇物品表（17166 条，key=物品id，值=稀疏列对象 {"列号": 值}）。
//
// 本 DTO 仅用于把原表整张显式加载进 OriginMir2Config1，方便与转换产物 ExcelConfig/ItemInfo.json 对比排查；
// 运行时逻辑不使用它。每列都给出【真实 JSON 类型】和【明确字段名】，不再用 JsonElement / colN 偷懒。
//
// 依据（已穷尽工作区来源）：
//   ① 全表类型扫描 .ai/scripts/probe_item_allcol_types.py；
//   ② 两个客户端 JS 包 + 全部 12 个 JS 的列访问扫描 .ai/scripts/probe_js_item_columns_v2.py、
//      probe_js_all_files_cols.py、probe_js_config_indexes.py（「JS 确证」）；
//   ③ 跨配置分片核验 .ai/scripts/probe_cross_table_values.py、probe_cross_keys.py、
//      probe_named_records.py、probe_last_cols.py（「跨表确证」）；
//   ④ Tools/Mir2ItemConverter/Program.cs 已落地的列映射；
//   ⑤ 0config.attribute_score 只用于解释列 40 内部的属性 id，绝不当作列义。
// 列 7/28/31/35/37 在全部 12 个 JS 中零访问、6 个配置分片也没有列名字段，已无更多可查来源：
// 只保留实测数据特征，不臆造业务名。稀疏数值列用 int?/uint?：JSON 缺列或 null 时为 null。
using System.Text.Json.Serialization;

namespace Server.ExcelConfig
{
    public class OriginItemTable
    {
        // 列 0：int，全表必有，1~250205046。物品唯一 id（Index）；JS 中 cm.item 的 key 即此值。
        [JsonPropertyName("0")] public int Id { get; set; }

        // 列 1：int，全表必有，1~9。物品大类：1=货币/计数类，2=装备，3=道具/材料…（JS: 2==o[1] 判定装备）。
        [JsonPropertyName("1")] public int ItemClass { get; set; }

        // 列 2：string，全表必有，最长 26 字。物品中文名（权威源；4572 条为「没用」废弃项）。
        [JsonPropertyName("2")] public string Name { get; set; } = "";

        // 列 3：string，15493 条有值。别名/展示名；JS 合成界面用 cm.item[n[0]][3] 显示主材名。
        [JsonPropertyName("3")] public string? AliasName { get; set; }

        // 列 4：int 或 null（26 条 null），1~55。品级/阶级（JS: e<=o[4] 八卦套装筛选；转换器 -> Grade）。
        [JsonPropertyName("4")] public int? Grade { get; set; }

        // 列 5：string，16246 条有值。中文类别串（"武器"/"宝箱"/"洪荒武器"…），转换器 -> ItemType。
        [JsonPropertyName("5")] public string? Category { get; set; }

        // 列 7：int，15553 条有值，1=普通(15550)、5=黄金系列(3 件黄金屠龙/黄金嗜魂法杖/赤金逍遥扇)。
        // 客户端 12 个 JS 全部零访问，具体系统语义无法再查；保留原值。
        [JsonPropertyName("7")] public int? Field7 { get; set; }

        // 列 8：int，1989 条有值，4~31702。显示模式（JS 确证：999=常规装备图标，25=图鉴鱼）。
        [JsonPropertyName("8")] public int? DisplayMode { get; set; }

        // 列 9：int，17165 条有值，0~60。名称颜色索引（JS 确证：Html.ITEM_COLORS_ARR[o[9]]）；转换器 -> Quality。
        [JsonPropertyName("9")] public int ColorIndex { get; set; }

        // 列 10：string，8690 条有值，形如 "2#90001"/"3#90001"/"3#100008"：频道/类型#条件id。
        // 跨表确证：90001=0config.conditions「开服第1天」，100003/100008=「开服天数小于N天」；
        // JS 确证：寻宝播报用 c[10] 过滤（!c[10]||1===c[10] 则不播报）。
        [JsonPropertyName("10")] public string? ShowCondition { get; set; }

        // 列 11：string，37 条有值且均为数字串。分解为七星经验（JS 确证：t+=Number(cm.item[lid][11]) 累加进 selcfg.exp）。
        [JsonPropertyName("11")] public string? SevenStarExp { get; set; }

        // 列 12：string，2810 条有值，形如 "8684#20"/"67#18"。装备回收列表（JS 确证：按 & 拆段显示「装备回收：物品名*数量」）。
        [JsonPropertyName("12")] public string? RecycleList { get; set; }

        // 列 13：string，585 条有值，形如 "50370001#1#10000"：宝箱id#数量#概率（后两段语义未完全确证）。
        // 跨表确证：2config.box.50370001.items="13-100-10000"（13=声望物品，产出100声望）。
        [JsonPropertyName("13")] public string? BoxReward { get; set; }

        // 列 14：int，10333 条有值，1~250205046。等价物品 id（JS 确证：o[14] 与 o[0] 的持有数量合并计算）。
        [JsonPropertyName("14")] public int? EquivalentItemId { get; set; }

        // 列 15：int，2033 条有值，407~988。获取途径 id（JS 确证：cm.wayGeter[o[15]]；跨表确证：5config.wayGeter.716=天神腰带[一阶]）。
        [JsonPropertyName("15")] public int? WayGetId { get; set; }

        // 列 16：int，17055 条有值，0~9999999。堆叠上限（转换器 -> StackSize；计数类物品兜底 9999）。
        [JsonPropertyName("16")] public int? StackLimit { get; set; }

        // 列 17：string，963 条有值，形如 "9041/9042"/"9501/9502"/单号。外观模型（男/女），Mir2OriginModelResolver 使用。
        [JsonPropertyName("17")] public string? OriginModel { get; set; }

        // 列 18：string，323 条有值，形如 "50029/50029"/单号字符串。外观特效号（转换器 -> Effect，byte 上限保护）。
        [JsonPropertyName("18")] public string? OriginEffect { get; set; }

        // 列 19：int，17165 条有值，1~1240102131。图标 id（JS 确证：ResUrl.url(o[19],8)）；服务器 Image 已扩为 uint。
        [JsonPropertyName("19")] public uint IconId { get; set; }

        // 列 20：int，17165 条有值，与列 19 几乎恒等（大图标号同源），保留原列便于对比。
        [JsonPropertyName("20")] public uint IconIdMirror { get; set; }

        // 列 21：int，16858 条有值，0~8019。子类型（64/216=技能书，60=随机传送，61=回城，66/266=特殊限时道具）。
        [JsonPropertyName("21")] public int SubType { get; set; }

        // 列 22：string，4709 条有值，形如 "79001"/"2#0#500#10#4"。使用参数/子类型串（JS 确证：o[22] 回退 o[23] 作 subType）。
        [JsonPropertyName("22")] public string? UseParam { get; set; }

        // 列 23：int，4127 条有值，1~7038。装备部位（JS 确证：equipInfo[o[23]]；405=疗伤石）。
        [JsonPropertyName("23")] public int? EquipSlot { get; set; }

        // 列 24：int 或 null（30 条 null），1~700。需要等级（JS 确证：「需要等级」；转换器 -> RequiredAmount）。
        [JsonPropertyName("24")] public int? RequiredLevel { get; set; }

        // 列 25：int，1578 条有值，0~155。需要转生等级（JS 确证：Logic.getReinLvStr(o[25])）。
        [JsonPropertyName("25")] public int? RequiredReinLevel { get; set; }

        // 列 26：string，2886 条有值，形如 "1#501#0#0#0"。开启条件串（JS 确证：Logic.checkLinkOpen2(o[26])）。
        [JsonPropertyName("26")] public string? UnlockCondition { get; set; }

        // 列 27：int，48 条有值，1~60。消耗冷却标记（JS 确证：o[27] → itemCDs + sendNotif(667,{type:2})）。
        [JsonPropertyName("27")] public int? ConsumeCDFlag { get; set; }

        // 列 28：int，23 条有值且恒为 1（宠物蛋/特殊道具）。客户端零访问，语义无法再查；保留原值。
        [JsonPropertyName("28")] public int? Field28 { get; set; }

        // 列 29：int，38 条有值，1~1000。每日限购/可刷新次数（JS 确证：config[29] 与 itemSurplusNum 显示「今日已刷新 x/y」）。
        [JsonPropertyName("29")] public int? DailyLimit { get; set; }

        // 列 30：int，6 条有值且恒为 1。可丢弃/可销毁标志（JS 确证：1==config[30] → btn_delete.visible=true）。
        [JsonPropertyName("30")] public int? CanDiscard { get; set; }

        // 列 31：int，39 条有值且恒为 1（月卡/卷轴类道具）。客户端零访问，语义无法再查；保留原值。
        [JsonPropertyName("31")] public int? Field31 { get; set; }

        // 列 32：int，416 条有值，1/3。使用数量模式（JS 确证：1=sendReqUseItem(1,…)，3=数量>1 弹批量选择框 UI 683）。
        [JsonPropertyName("32")] public int? UseCountMode { get; set; }

        // 列 33：int，4937 条有值，0/1。获得时自动使用标志（JS 确证：1==config[33]&&id!=405 → sendReqUseItem(count,…)）。
        [JsonPropertyName("33")] public int? AutoUseOnGain { get; set; }

        // 列 35：int，14912 条有值，1~11017426。客户端 12 个 JS 零访问、跨表无对应 id 空间；保留原值。
        [JsonPropertyName("35")] public int? Field35 { get; set; }

        // 列 37：int，17097 条有值，60(15937)/40(1130)/120(27)/30/61/62。客户端零访问；保留原值。
        [JsonPropertyName("37")] public int Field37 { get; set; }

        // 列 39：int，4092 条有值，1~9666666。基础战力值（JS 确证：getAllPower 里 a+=item[39]）；另用于回收过滤与 checkNbvalue 比较。
        [JsonPropertyName("39")] public int? PowerValue { get; set; }

        // 列 40：string，6779 条有值，形如 "4#1111|5#3333|..."（可按 & 分多段）。属性串（JS 确证：getTipsAttr(o[40],...,o[52],...)）。
        [JsonPropertyName("40")] public string? Attributes { get; set; }

        // 列 41：string，300 条有值，形如 "70001"/"70005#70003"。Buff id 列表（JS 确证：cm.buff[c].nbvalue 累加战力）。
        [JsonPropertyName("41")] public string? BuffIds { get; set; }

        // 列 42：string，11663 条有值，最长 1608 字，含 <font>/<br>。描述文本（JS 确证：txt_desc.textFlow=o[42]），转换器 -> ToolTip。
        [JsonPropertyName("42")] public string? Description { get; set; }

        // 列 43：string，6 条有值，形如 "3001701#1"/"499#100"：使用消耗 物品id#数量。
        // 跨表确证：499=金币、3001701=标准鱼饵（限定鱼饵消耗标准鱼饵）。
        [JsonPropertyName("43")] public string? UseCost { get; set; }

        // 列 44：string，94 条有值，形如 "1#1440"/"2#2025-11-30-00-00-00"。有效期/时限配置串。
        [JsonPropertyName("44")] public string? ValidityString { get; set; }

        // 列 45：int，2186 条有值，444~898。关联图鉴等级 id（跨表确证：2config.tujian.444=火龙神9级、893~898=半兽人统帅24~29级；
        // 神域/战狂/天地等同一图鉴组装备共享同一 c45）。JS 不直接访问本列，业务用途未完全确证。
        [JsonPropertyName("45")] public int? TujianLevelId { get; set; }

        // 列 48：string，29 条有值，形如 "1#190000|4#53000|5#125800"。属性串格式（同列 40 编码），语义未确证。
        [JsonPropertyName("48")] public string? ExtraAttributes { get; set; }

        // 列 49：int，20 条有值，30/90/270/810/2430（30×3^n）。魂印/法魂/面甲/魂器 5 阶的阶位评分；
        // 与 col39 战力(480/960/1920/3600/6000)同步递增，客户端零访问，仅数据规律可证。
        [JsonPropertyName("49")] public int? SoulStageScore { get; set; }

        // 列 50：string，1644 条有值，形如 "9#0#1"/"1#3#1"。职业相关串；转换器取首段作职业兜底。
        [JsonPropertyName("50")] public string? CareerString { get; set; }

        // 列 51：string，1644 条有值，形如 "1#0#1#1#1#240#120#2#1&3#0#..."。多段职业数值串，与列 50 同现。
        [JsonPropertyName("51")] public string? CareerDetailString { get; set; }

        // 列 52：int，484 条有值，0~3。职业 1=战/2=法/3=道（JS 确证：Logic.getCareerName(o[52])）；技能书职业来源。
        [JsonPropertyName("52")] public int? Career { get; set; }

        // 列 54：string，284 条有值，形如 "4#100|5#100|92#100|93#100|94#100|95#100"。属性串格式，语义未确证。
        [JsonPropertyName("54")] public string? BonusAttributes { get; set; }

        // 列 56：int，374 条有值，1/2。性别（JS 确证：ia=o[56]，缺失时回退玩家性别）；转换器 -> RequiredGender。
        [JsonPropertyName("56")] public int? Gender { get; set; }

        // 列 57：int，2118 条有值，1~530。持久（JS 确证：「持久：o[57]/o[57]」）。
        [JsonPropertyName("57")] public int? Durability { get; set; }

        // 列 58：string，851 条有值，形如 "499#2000000"：消耗货币 物品id#数量。
        // JS 确证：splitNum('#') 后 gd.bag.getCount(id) >= count 判断合成/消耗材料是否够。
        [JsonPropertyName("58")] public string? ConsumeCost { get; set; }

        // 列 59：int，1051 条有值，1~3。装备职业归属（转换器优先级高于列 52/50）。
        [JsonPropertyName("59")] public int? EquipCareer { get; set; }

        // 列 60：string，579 条有值，形如 "13#19200"/"58#15552"：装备回收/分解声望 货币id#数量。
        // 跨表确证：13=声望、58=鸿蒙碎晶；且与 col13 宝箱配置的第一段产出值一致
        // （裁决 c13→box 产出 300 声望 = c60 "13#300"）。
        [JsonPropertyName("60")] public string? RecycleReputation { get; set; }

        // 列 61：int，321 条有值，10701~130212。场景特效 id（JS 确证：sceneEffect.init(n.preGro,o[61])）。
        [JsonPropertyName("61")] public int? SceneEffectId { get; set; }

        // 列 62：int，208 条有值且恒为 1。可投保标志（JS 确证：!config[62] 的装备被过滤出投保列表）。
        [JsonPropertyName("62")] public int? CanInsure { get; set; }

        // 列 63：string，199 条有值，最长 38 字且含 <font>。附加提示文本（JS 确证：txt_tips2.textFlow=Html.toEle(o[63])）。
        [JsonPropertyName("63")] public string? TipsText { get; set; }
    }
}
