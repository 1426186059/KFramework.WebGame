// partial 文件：Envir 策划配置表加载（JSON 运行时表 + 原始传奇分片权威表）。
using ClientPackets;
using Server.Library.MirDatabase;
using Server.Library.Utils;
using Server.MirDatabase;
using Server.MirNetwork;
using Server.MirObjects;
using Server.MirObjects.Monsters;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text.RegularExpressions;
using Library.Network;
using S = ServerPackets;
using Shared.Helpers;
using System.Net.Http;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Server.MirEnvir
{
    public partial class Envir
    {
        public bool LoadDB()
        {
            lock (LoadLock)
            {
                return LoadDBFromJson();
            }
        }


        // ===== 配置表从本地 JSON 加载（替代旧的 .db 二进制加载；不再从 Web 下载，避免依赖 5082 资源服务器） =====
        private bool LoadDBFromJson()
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
                // 以下三张表是运行时数据，编辑器实例不需要加载（也不打日志）。
                // 注意：MapInfoList（MapInfo_Fix.json）不在此列——那是编辑器要编辑的配置本身，MapInfoForm 等表单依赖它。
                if (!ReferenceEquals(this, Edit))
                {
                    // 地图 NPC 摆放直接读 5config 分片的 mapnpc 表（权威源），不依赖单条 MapIndex/Location。
                    LoadMapNpcPlacements(baseDir);
                    // 原始传奇商店：读 4config 分片的 npcstore 表（NPCInfo.OriginSettings.StoreId → 商品）。
                    LoadOriginStores(baseDir);
                    // 原始传奇显示条件表（0config.conditions），供 npcstore.showCondition 过滤高级/限时商品
                    LoadOriginConditions(baseDir);
                    // 原始传奇配置表全部加载备用（静态数据，将来用）：
                    // 4config._task / 5config.goals / 0config.global[9201] / 5config.textstyle
                    LoadOriginQuestTables(baseDir);
                }
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

                PostLoadDB();
                return true;
            }
            catch (Exception ex)
            {
                MessageQueue.Enqueue("从本地加载配置表失败: " + ex.Message);
                return false;
            }
        }

        private void PostLoadDB()
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
        /// 地图 NPC 摆放来自 NPCInfoTable.OriginSettings.Placements：构建期 Mir2NPCConverter 已把
        /// 5config.mapnpc 的摆放点内嵌到每条 NPC 定义，运行时不再读分片。
        /// MapInfoTable.CreateMap 会按 MapId 把每个摆放点挂到对应地图，一个 NPC 定义可生成多个实例。
        /// </summary>
        private void LoadMapNpcPlacements(string baseDir)
        {
            if (ReferenceEquals(this, Edit)) return; // 编辑器不加载运行时摆放数据
            MapNpcPlacements.Clear();
            foreach (var npc in NPCInfoList)
                if (npc.OriginSettings?.Placements != null && npc.OriginSettings.Placements.Count > 0)
                    MapNpcPlacements[npc.Index] = npc.OriginSettings.Placements;

            MessageQueue.Enqueue($"[{InstanceTag}] mapnpc 摆放点: {MapNpcPlacements.Sum(kv => kv.Value.Count)} 处 / {MapNpcPlacements.Count} 个 NPC（NPCInfo 内嵌）");
        }

        /// <summary>
        /// 原始传奇商店表：整文件反序列化 4config 分片的 npcstore，按 StoreId 建索引。
        /// 仿客户端 OriginMir2Config 写法：DTO 只声明用到的表，其余顶层表自动忽略。
        /// </summary>
        private void LoadOriginStores(string baseDir)
        {
            OriginStores.Clear();
            try
            {
                string file = Directory.GetFiles(baseDir, "4config*.json").FirstOrDefault();
                if (file == null)
                {
                    MessageQueue.Enqueue("原始传奇商店表未找到 4config*.json（ExcelConfig 目录）");
                    return;
                }

                var cfg = JsonTool.FromJson<OriginMir2Config4>(File.ReadAllText(file), AppJsonContext.Default.OriginMir2Config4);
                if (cfg?.npcstore == null) return;

                foreach (var store in cfg.npcstore)
                {
                    var list = store.Value?.Values.OrderBy(x => x.id).ToList() ?? new List<OriginStoreItemTable>();
                    OriginStores[store.Key] = list;
                }

                MessageQueue.Enqueue($"[{InstanceTag}] 原始传奇商店表: {OriginStores.Count} 个商店 / {OriginStores.Sum(kv => kv.Value.Count)} 条商品（4config.npcstore）");
            }
            catch (Exception ex)
            {
                MessageQueue.Enqueue("加载原始传奇商店表失败: " + ex.Message);
            }
        }

        /// <summary>加载 0config.conditions 条件表（原始传奇 Logic.checkCondition 用）。</summary>
        private void LoadOriginConditions(string baseDir)
        {
            OriginConditions.Clear();
            try
            {
                string file = Directory.GetFiles(baseDir, "0config*.json").FirstOrDefault();
                if (file == null)
                {
                    MessageQueue.Enqueue("原始传奇条件表未找到 0config*.json（ExcelConfig 目录）");
                    return;
                }
                var cfg = JsonTool.FromJson<OriginMir2Config0>(File.ReadAllText(file), AppJsonContext.Default.OriginMir2Config0);
                if (cfg?.conditions != null)
                {
                    foreach (var kv in cfg.conditions)
                    {
                        // 原始配置里有 "undefined" 等非数字 key，跳过；只收数字 id 的条件
                        if (int.TryParse(kv.Key, out int condId) && kv.Value != null)
                            OriginConditions[condId] = kv.Value;
                    }
                }
                MessageQueue.Enqueue($"[{InstanceTag}] 原始传奇条件表: {OriginConditions.Count} 条（0config.conditions）");
            }
            catch (Exception ex)
            {
                MessageQueue.Enqueue("加载原始传奇条件表失败: " + ex.Message);
            }
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
