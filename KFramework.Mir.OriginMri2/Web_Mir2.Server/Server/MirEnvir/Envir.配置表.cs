// partial 文件：Envir 配置表统一加载入口。
// LoadExcelConfig → LoadMyExcelConfig（我们自己的 ExcelConfig/*.json）
//                → LoadOriginMir2Configs（原始传奇 0config~5config 六个分片，先全加载再派生索引）
//                → PostLoadExcelConfig。只由运行时实例调用。
using Server.MirDatabase;
using Server.MirObjects;
using Shared.Helpers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace Server.MirEnvir
{
    public partial class Envir
    {
        public bool LoadExcelConfig()
        {
            lock (LoadLock)
            {
                return LoadExcelConfigFromJson();
            }
        }


        // ===== 配置表从本地 JSON 加载（替代旧的 .db 二进制加载；不再从 Web 下载，避免依赖 5082 资源服务器） =====
        private bool LoadExcelConfigFromJson()
        {
            try
            {
                // 本地配置表目录：Web_Mir2.Server.Version/ExcelConfig/（ItemInfoTable/MonsterInfoTable/.../Meta 等 *.json）
                string baseDir = Settings.ExcelConfigPath;
                if (!Directory.Exists(baseDir))
                {
                    MessageQueue.Enqueue("从本地加载配置表失败：配置目录不存在 " + baseDir);
                    return false;
                }

                var meta = LoadConfig<ConfigMeta>(baseDir, "Meta.json");
                LoadVersion = meta.version;
                LoadCustomVersion = meta.customVersion;

                if (LoadVersion < MinVersion)
                {
                    MessageQueue.Enqueue(string.Format("Cannot load a database version {0}. Mininum supported is {1}.", LoadVersion, MinVersion));
                    return false;
                }
                else if (LoadVersion > Version)
                {
                    MessageQueue.Enqueue(string.Format("Cannot load a database version {0}. Maximum supported is {1}.", LoadVersion, Version));
                    return false;
                }

                // 两类配置分开加载：先我们自己的 ExcelConfig 表，再原始传奇 0~5config 分片。
                LoadMyExcelConfig(baseDir);
                LoadOriginMir2Configs(baseDir);

                PostLoadExcelConfig();
                return true;
            }
            catch (Exception ex)
            {
                MessageQueue.Enqueue("从本地加载配置表失败: " + ex.Message);
                return false;
            }
        }

        /// <summary>加载我们自己的 ExcelConfig/*.json 表（Item/Monster/NPC/Quest/Magic/Shop 等）。</summary>
        private void LoadMyExcelConfig(string baseDir)
        {
            MapInfoList = JsonTool.FromJson<List<MapInfoTable>>(
                NormalizeMapInfoJson(File.ReadAllText(Path.Combine(Settings.MapPath, "MapInfo_Fix.json"))), MapInfoJsonOptions) ?? new List<MapInfoTable>();
            // SafeZoneInfo.Info 已 [JsonIgnore]，反序列化后手动挂回所属地图（SetBind 依赖 szi.Info.Index）。
            foreach (MapInfoTable mi in MapInfoList)
                foreach (SafeZoneInfo sz in mi.SafeZones)
                    sz.Info = mi;

            ItemInfoList = LoadConfigList<ItemInfoTable>(baseDir, "ItemInfo.json");
            MonsterInfoList = LoadConfigList<MonsterInfoTable>(baseDir, "MonsterInfo.json");
            NPCInfoList = LoadConfigList<NPCInfoTable>(baseDir, "NPCInfo.json");
            // NameUI 为空时默认等于 Name（界面显示名），需显式配置的（重名 NPC）以配置为准。
            foreach (var npc in NPCInfoList) if (string.IsNullOrEmpty(npc.NameUI)) npc.NameUI = npc.Name;

            QuestInfoList = LoadConfigList<QuestInfoTable>(baseDir, "QuestInfo.json");
            // JSON 反序列化走无参构造函数，不会触发 QuestInfoTable.LoadInfo()，
            // 需手动读取 FileName 指向的 .txt，填充 Description/任务/奖励等。
            foreach (var quest in QuestInfoList) quest.LoadInfo();

            DragonInfoTable = LoadConfig<DragonInfoTable>(baseDir, "DragonInfo.json");
            MagicInfoList = LoadConfigList<MagicInfoTable>(baseDir, "MagicInfo.json");
            // 原始传奇技能书映射：物品 id → 技能/目标等级（构建期从 1config.item 导出）。
            SkillBookInfoTable.BuildIndex(LoadConfigList<SkillBookInfoTable>(baseDir, "SkillBookInfo.json"));

            if (LoadVersion >= 63)
                GameShopList = LoadConfigList<GameShopItemTable>(baseDir, "GameShopItem.json");
            if (LoadVersion >= 66)
                ConquestInfoList = LoadConfigList<ConquestInfoTable>(baseDir, "ConquestInfo.json");
            if (LoadVersion > 67)
                RespawnTick = LoadConfig<RespawnTimerTable>(baseDir, "RespawnTimer.json");
        }

        /// <summary>
        /// 统一加载原始传奇全部静态配置（0config~5config）：
        /// 第一阶段把 6 个分片 DTO 全部加载进 OriginConfigs（已声明表强类型，其余表 ExtraTables 兜底）；
        /// 第二阶段等全部表都加载完，再从根类统一派生运行时索引（商店/条件/任务/目标/文字样式/奖励包/NPC 摆放）。
        /// </summary>
        private void LoadOriginMir2Configs(string baseDir)
        {
            OriginConfigs.Clear();
            OriginStores.Clear();
            OriginConditions.Clear();
            OriginBoxes.Clear();
            OriginTasks.Clear();
            OriginGoals.Clear();
            OriginTaskTypeNames.Clear();
            OriginTextStyles.Clear();
            MapNpcPlacements.Clear();

            try
            {
                // ===== 阶段一：6 个分片 DTO 全部加载（先加载完，再处理逻辑） =====
                LoadOriginConfig<OriginMir2Config0>(baseDir, 0, AppJsonContext.Default.OriginMir2Config0);
                LoadOriginConfig<OriginMir2Config1>(baseDir, 1, AppJsonContext.Default.OriginMir2Config1);
                LoadOriginConfig<OriginMir2Config2>(baseDir, 2, AppJsonContext.Default.OriginMir2Config2);
                LoadOriginConfig<OriginMir2Config3>(baseDir, 3, AppJsonContext.Default.OriginMir2Config3);
                LoadOriginConfig<OriginMir2Config4>(baseDir, 4, AppJsonContext.Default.OriginMir2Config4);
                LoadOriginConfig<OriginMir2Config5>(baseDir, 5, AppJsonContext.Default.OriginMir2Config5);

                // ===== 阶段二：所有表加载完后，统一派生运行时索引（全是内存操作，不再读文件） =====
                OriginConfigs.TryGetValue(0, out var root0);
                OriginConfigs.TryGetValue(2, out var root2);
                OriginConfigs.TryGetValue(4, out var root4);
                OriginConfigs.TryGetValue(5, out var root5);
                var cfg0 = root0 as OriginMir2Config0;
                var cfg2 = root2 as OriginMir2Config2;
                var cfg4 = root4 as OriginMir2Config4;
                var cfg5 = root5 as OriginMir2Config5;

                if (cfg0 != null)
                {
                    // 0config.conditions → OriginConditions（条件过滤），key "undefined" 等非数字 id 跳过。
                    if (cfg0.conditions != null)
                        foreach (var kv in cfg0.conditions)
                            if (int.TryParse(kv.Key, out int condId) && kv.Value != null)
                                OriginConditions[condId] = kv.Value;

                    // 0config.global[9201] → 任务类型前缀（type#[前缀]&...）。
                    if (cfg0.global != null && cfg0.global.TryGetValue("9201", out var g) && g != null)
                        foreach (var item in g.value.Split('&', StringSplitOptions.RemoveEmptyEntries))
                        {
                            var parts = item.Split('#');
                            if (parts.Length >= 2 && int.TryParse(parts[0], out int type))
                                OriginTaskTypeNames[type] = parts[1];
                        }
                }

                if (cfg2 != null && cfg2.box != null)
                {
                    foreach (var kv in cfg2.box)
                    {
                        if (!int.TryParse(kv.Key, out int id) || kv.Value == null) continue;
                        kv.Value.ParseRewards();
                        OriginBoxes[id] = kv.Value;
                    }
                }

                if (cfg4 != null)
                {
                    // 4config.npcstore → OriginStores（商店 id → 商品列表，按 id 排序）。
                    if (cfg4.npcstore != null)
                        foreach (var store in cfg4.npcstore)
                            OriginStores[store.Key] = store.Value?.Values.OrderBy(x => x.id).ToList() ?? new List<OriginStoreItemTable>();

                    // 4config._task → OriginTasks（任务权威表）。
                    if (cfg4._task != null)
                        foreach (var kv in cfg4._task)
                            if (int.TryParse(kv.Key, out int id) && kv.Value != null)
                                OriginTasks[id] = kv.Value;
                }

                if (cfg5 != null)
                {
                    if (cfg5.goals != null)
                        foreach (var kv in cfg5.goals)
                            if (int.TryParse(kv.Key, out int id) && kv.Value != null)
                                OriginGoals[id] = kv.Value;
                    if (cfg5.textstyle != null)
                        foreach (var kv in cfg5.textstyle)
                            if (int.TryParse(kv.Key, out int id) && kv.Value != null)
                                OriginTextStyles[id] = kv.Value;
                }

                // 地图 NPC 摆放：构建期已内嵌到 NPCInfo.OriginSettings.Placements，运行时不再读 5config.mapnpc。
                foreach (var npc in NPCInfoList)
                    if (npc.OriginSettings?.Placements != null && npc.OriginSettings.Placements.Count > 0)
                        MapNpcPlacements[npc.Index] = npc.OriginSettings.Placements;

                MessageQueue.Enqueue($"[{InstanceTag}] 原始传奇配置表: {OriginConfigs.Count}/6 分片 / 未声明表 {OriginConfigs.Sum(kv => kv.Value.ExtraTables.Count)} 张 / " +
                    $"奖励包 {OriginBoxes.Count} / 任务 {OriginTasks.Count} / 目标 {OriginGoals.Count} / 商店 {OriginStores.Count} / " +
                    $"条件 {OriginConditions.Count} / 文字样式 {OriginTextStyles.Count} / 类型前缀 {OriginTaskTypeNames.Count} / 摆放点 {MapNpcPlacements.Sum(kv => kv.Value.Count)}");
            }
            catch (Exception ex)
            {
                MessageQueue.Enqueue("加载原始传奇配置表失败: " + ex.Message);
            }
        }

        /// <summary>加载单个分片根类并存进 OriginConfigs；文件不存在或解析失败只记日志，不影响其它分片。</summary>
        private T LoadOriginConfig<T>(string baseDir, int index, JsonTypeInfo<T> typeInfo) where T : OriginConfigRoot
        {
            string file = Directory.GetFiles(baseDir, index + "config*.json").FirstOrDefault();
            if (file == null)
            {
                MessageQueue.Enqueue($"原始传奇配置表未找到 {index}config*.json（ExcelConfig 目录）");
                return null;
            }
            var cfg = JsonTool.FromJson<T>(File.ReadAllText(file), typeInfo);
            if (cfg != null) OriginConfigs[index] = cfg;
            return cfg;
        }

        private void PostLoadExcelConfig()
        {
            for (int i = 0; i < ItemInfoList.Count; i++)
            {
                if (ItemInfoList[i] != null && ItemInfoList[i].RandomStatsId < Settings.RandomItemStatsList.Count)
                    ItemInfoList[i].RandomStats = Settings.RandomItemStatsList[ItemInfoList[i].RandomStatsId];
            }
            if (LoadVersion >= 63)
            {
                for (int i = 0; i < GameShopList.Count; i++)
                    Main.BindGameShop(GameShopList[i]);
            }
            Settings.LinkGuildCreationItems(ItemInfoList);
        }

        /// <summary>
        /// 判断玩家是否满足原始传奇 npcstore.showCondition（忠实复刻 Logic.checkCondition/checkACondition）。
        /// showCondition 是条件 id 列表（用 & 或 | 组合，原始传奇 splitCondition 按“或”分组、“与”组内）。
        /// 我们游戏没有 VIP/转生/充值等系统，这类条件一律判为不满足（普通玩家看不到高级商品，与原始传奇一致）。
        /// 支持：condType 1=等级≥ / 2=等级≤ / 34=等级区间 / 9=开服天数(按 Settings.OpenServerDate 计算，未配置则放行)。空条件视为满足。
        /// </summary>
        public bool CheckOriginShowCondition(PlayerObject player, string showCondition)
        {
            if (string.IsNullOrWhiteSpace(showCondition)) return true;
            if (OriginConditions.Count == 0) return true; // 条件表没加载时不误伤，保持全显示

            // splitCondition：原始传奇按“&”拆组（组内“与”），这里简化为整体“与”+“|”“或”
            var idTokens = showCondition.Replace("&", "|").Split('|', StringSplitOptions.RemoveEmptyEntries);
            bool any = false;
            foreach (var token in idTokens)
            {
                if (!int.TryParse(token.Trim(), out int condId)) continue;
                if (!OriginConditions.TryGetValue(condId, out var cond)) continue;

                bool ok = false;
                switch (cond.conditionType)
                {
                    case 1: // 等级 >= param
                        ok = player != null && player.Info.Level >= ParseFirstInt(cond.conditionParam, 0);
                        break;
                    case 2: // 等级 < param
                        ok = player != null && player.Info.Level < ParseFirstInt(cond.conditionParam, int.MaxValue);
                        break;
                    case 34: // 等级区间 param="min#max#..."（原始传奇 34 型：level<min 或 level>max 都不满足的写法各异，这里取 [min,max]）
                        {
                            var p = cond.conditionParam?.Split('#') ?? Array.Empty<string>();
                            int lo = p.Length > 0 ? ParseFirstInt(p[0], 0) : 0;
                            int hi = p.Length > 1 ? ParseFirstInt(p[1], int.MaxValue) : int.MaxValue;
                            ok = player != null && player.Info.Level >= lo && player.Info.Level <= hi;
                            break;
                        }
                    case 9: // 开服天数（如 90007=开服第7天）：按 Settings.OpenServerDate 真正计算
                        {
                            int needDay = ParseFirstInt(cond.conditionParam, 1);
                            if (string.IsNullOrWhiteSpace(Settings.OpenServerDate))
                            {
                                ok = true; // 未配置开服日期 → 不限制（视为开服已久）
                            }
                            else if (DateTime.TryParse(Settings.OpenServerDate, out var openDate))
                            {
                                // 开服当天记为第 1 天
                                int openDays = (DateTime.Today.Date - openDate.Date).Days + 1;
                                ok = openDays >= needDay;
                            }
                            else
                            {
                                ok = true; // 日期格式非法 → 不误伤，放行
                            }
                            break;
                        }
                    default:
                        // VIP/转生/开服天数/充值/拥有物品等我们没有的系统，判为不满足 → 过滤掉高级商品
                        ok = false;
                        break;
                }
                if (ok) { any = true; break; }
            }
            return any;
        }

        private static int ParseFirstInt(string text, int fallback)
            => int.TryParse(text, out int v) ? v : fallback;

        /// <summary>
        /// 按原始传奇 StoreId 生成可下发的商品列表。item 的 "物品id#数量" 按 Index 映射到我们 ItemInfo。
        /// 价格沿用我们 ItemInfo.Price（转换器已从 1config col22 映射），原版 cost 仅作参考未覆盖。
        /// </summary>
        public List<UserItem> GetOriginStoreGoods(int storeId, PlayerObject player = null)
        {
            var result = new List<UserItem>();
            if (!OriginStores.TryGetValue(storeId, out var list)) return result;

            foreach (var rec in list)
            {
                // 原始传奇 showCondition：玩家不满足条件则不显示（如修复油 8153 需 999 级）。
                // player 为 null（购买兜底查找）时不过滤，保证能按 UniqueID 找到已展示的商品。
                if (player != null && !CheckOriginShowCondition(player, rec.showCondition)) continue;

                if (string.IsNullOrEmpty(rec.item)) continue;
                var itemParts = rec.item.Split('#');
                if (!int.TryParse(itemParts[0], out int itemIndex)) continue;

                ItemInfoTable info = GetItemInfo(itemIndex);
                if (info == null) continue;

                UserItem item = CreateShopItem(info, (ulong)(storeId * 100000L + rec.id));
                if (itemParts.Length > 1 && int.TryParse(itemParts[1], out int count) && count > 0)
                    item.Count = (ushort)Math.Min(count, ushort.MaxValue);

                // 原始传奇 cost = 货币物品id#价格。货币映射：1/499=金币(0)，6=元宝(1，价/100)，41=传奇币(2)
                var costParts = rec.cost?.Split('#');
                if (costParts != null && costParts.Length > 1 && int.TryParse(costParts[0], out int currencyId) && int.TryParse(costParts[1], out int price))
                {
                    switch (currencyId)
                    {
                        case 6: // 元宝，原始传奇单位是分
                            item.ShopCurrencyType = 1;
                            item.ShopPrice = (uint)Math.Max(0, price / 100);
                            break;
                        case 41: // 传奇币
                            item.ShopCurrencyType = 2;
                            item.ShopPrice = (uint)Math.Max(0, price);
                            break;
                        default: // 金币(1/499)及其它特殊货币，暂按金币显示与扣费
                            item.ShopCurrencyType = 0;
                            item.ShopPrice = (uint)Math.Max(0, price);
                            break;
                    }
                }

                result.Add(item);
            }

            return result;
        }

        private T LoadConfig<T>(string baseDir, string fileName) where T : class
            => JsonTool.FromJson<T>(File.ReadAllText(Path.Combine(baseDir, fileName)), JsonTool.ConfigOptions);

        private List<T> LoadConfigList<T>(string baseDir, string fileName)
            => JsonTool.FromJson<List<T>>(File.ReadAllText(Path.Combine(baseDir, fileName)), JsonTool.ConfigOptions) ?? new List<T>();

        private sealed class ConfigMeta
        {
            public int version { get; set; }
            public int customVersion { get; set; }
        }

        // MapInfoTable 不再使用 ReferenceHandler.Preserve：SafeZoneInfo.Info 已用 [JsonIgnore] 排除，
        // 因此文件里不再出现 $id/$ref/$values 元数据（编辑器曾误把 $id 写成数字导致加载失败）。
        private static readonly JsonSerializerOptions MapInfoJsonOptions = new JsonSerializerOptions
        {
            TypeInfoResolver = new JsonConfigResolver(),
        };

        /// <summary>把旧的“引用信封”格式归一化为干净 JSON：递归删除 $id/$ref，并把 {"$values":[...]} 拆成数组。
        /// 这样旧的信封格式与新格式都能被无 Preserve 的 MapInfoJsonOptions 反序列化。</summary>
        private static string NormalizeMapInfoJson(string json)
        {
            JsonNode node = JsonNode.Parse(json);
            if (node == null) return json;
            JsonNode normalized = NormalizeNode(node);
            return normalized.ToJsonString();
        }

        private static JsonNode NormalizeNode(JsonNode node)
        {
            // 重建新树（不就地改），避免把已挂父节点的节点重复挂到新父节点抛出 "already has a parent"。
            if (node is JsonObject obj)
            {
                if (obj["$values"] is JsonArray vals) return NormalizeNode(vals);
                var result = new JsonObject();
                foreach (var p in obj)
                {
                    if (p.Key == "$id" || p.Key == "$ref") continue;
                    result[p.Key] = NormalizeNode(p.Value);
                }
                return result;
            }
            if (node is JsonArray arr)
            {
                var result = new JsonArray();
                foreach (var item in arr) result.Add(NormalizeNode(item));
                return result;
            }
            return node.DeepClone();
        }

    }
}
