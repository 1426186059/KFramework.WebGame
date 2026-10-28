using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Server.MirDatabase;   // MapInfo / MonsterInfo / NPCInfo / QuestInfo / DragonInfo / MagicInfo / ConquestInfo
using Server.MirEnvir;     // RespawnTimer
// 注意：ItemInfo / GameShopItem / Stats 位于全局命名空间（ItemData.cs 无 namespace 声明），直接可用。

namespace MirDBToJson
{
    /// <summary>
    /// 持有从 Server.MirDB 反序列化出来的全部静态配置表（不含账号/公会等玩家数据）。
    /// 字段全为 public，便于 System.Text.Json（IncludeFields）序列化。
    /// </summary>
    public class ConfigData
    {
        public List<MapInfo> Maps = new();
        public List<ItemInfo> Items = new();
        public List<MonsterInfo> Monsters = new();
        public List<NPCInfo> NPCs = new();
        public List<QuestInfo> Quests = new();
        public DragonInfo Dragon;
        public List<MagicInfo> Magics = new();
        public List<GameShopItem> GameShop = new();
        public List<ConquestInfo> Conquests = new();
        public RespawnTimer Respawn;
    }

    /// <summary>
    /// 与服务器 Envir.ConfigMeta 同结构，用“字段”承载版本号。
    /// 注意：必须用字段而非匿名对象——匿名对象的属性是只读的，会被 JSON 序列化器的
    /// 只读属性忽略规则（JsonConfigResolver）剔除，导致 Meta 丢失版本号。
    /// </summary>
    public class ConfigMeta
    {
        public int version;
        public int customVersion;
    }

    public static class Program
    {
        public static string OutDir = "ExportedConfig";

        public static int Main(string[] args)
        {
            // 中文化模式：MirDBToJson --cn [dir]，把 ItemInfo.json 的英文名翻译成中文
            if (args.Length > 0 && args[0] == "--cn")
                return LocalizeAll(args.Length > 1 ? args[1] : "ExportedConfig");

            // 全量一致汉化（服务器端直接写中文名）：
            //   MirDBToJson --cn-server <crystalConfigDir> <dataRoot1> [dataRoot2 ...]
            //   crystalConfigDir = Mir2Res_Crystal\Crystal\Config（ItemInfo/MonsterInfo 的英文源，原位改写 Name）
            //   dataRoot         = 服务器运行目录（含 Envir\ 与 Configs\）
            //   关键：Name 是掉落/配方/NPC脚本/启动检查的关联键，改写 Name 的同时必须用同一份
            //   映射重写所有引用（Envir\**\*.txt 的名字 token、Configs\*.ini 的名字值），
            //   否则就是 2026-10-28 那次“未找到物品/无法启动服务器”的事故。
            if (args.Length > 0 && args[0] == "--cn-server")
            {
                if (args.Length < 3)
                {
                    Console.WriteLine("usage: MirDBToJson --cn-server <crystalConfigDir> <dataRoot1> [dataRoot2 ...]");
                    return 1;
                }
                return LocalizeServer(args[1], args.Skip(2).ToArray());
            }

            string dbPath = args.Length > 0 ? args[0] : "Server.MirDB";
            if (!File.Exists(dbPath))
            {
                Console.WriteLine($"找不到 Server.MirDB: {dbPath}");
                Console.WriteLine("usage: MirDBToJson <path-to-Server.MirDB> [output-dir]");
                return 1;
            }

            int version, customVersion;
            var data = new ConfigData();

            // ---- 复用官方数据类的 BinaryReader 构造器，按 Envir.LoadDB 的块顺序精确反序列化 ----
            using (var fs = File.OpenRead(dbPath))
            using (var reader = new BinaryReader(fs, Encoding.UTF8))
            {
                version = reader.ReadInt32();
                customVersion = reader.ReadInt32();

                // 关键：官方各数据类的 BinaryReader 构造器内部依赖静态版本号 Envir.LoadVersion，
                // 必须先写回，否则会按 LoadVersion=0 走错分支导致读取错位 / 越界。
                Envir.LoadVersion = version;
                Envir.LoadCustomVersion = customVersion;

                // 索引段（版本门控）
                reader.ReadInt32(); // MapIndex
                reader.ReadInt32(); // ItemIndex
                reader.ReadInt32(); // MonsterIndex
                reader.ReadInt32(); // NPCIndex
                reader.ReadInt32(); // QuestIndex
                if (version >= 63) reader.ReadInt32(); // GameshopIndex
                if (version >= 66) reader.ReadInt32(); // ConquestIndex
                if (version >= 68) reader.ReadInt32(); // RespawnIndex

                int c;
                c = reader.ReadInt32(); // MapInfo 块
                for (int i = 0; i < c; i++) data.Maps.Add(new MapInfo(reader));
                c = reader.ReadInt32(); // ItemInfo 块
                for (int i = 0; i < c; i++) data.Items.Add(new ItemInfo(reader, version, customVersion));
                c = reader.ReadInt32(); // MonsterInfo 块
                for (int i = 0; i < c; i++) data.Monsters.Add(new MonsterInfo(reader));
                c = reader.ReadInt32(); // NPCInfo 块
                for (int i = 0; i < c; i++) data.NPCs.Add(new NPCInfo(reader));
                c = reader.ReadInt32(); // QuestInfo 块
                for (int i = 0; i < c; i++) data.Quests.Add(new QuestInfo(reader));
                data.Dragon = new DragonInfo(reader);
                c = reader.ReadInt32(); // MagicInfo 块（无版本门控，总是读取）
                for (int i = 0; i < c; i++) data.Magics.Add(new MagicInfo(reader, version, customVersion));
                if (version >= 63)
                {
                    c = reader.ReadInt32(); // GameShop 块
                    for (int i = 0; i < c; i++) data.GameShop.Add(new GameShopItem(reader, version, customVersion));
                }
                if (version >= 66)
                {
                    c = reader.ReadInt32(); // ConquestInfo 块
                    for (int i = 0; i < c; i++) data.Conquests.Add(new ConquestInfo(reader));
                }
                if (version > 67)
                    data.Respawn = new RespawnTimer(reader);
            }

            var opts = new JsonSerializerOptions
            {
                WriteIndented = true,
                IncludeFields = true,
                ReferenceHandler = ReferenceHandler.Preserve,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                TypeInfoResolver = new IgnoreResolver(),
            };

            OutDir = args.Length > 1 ? args[1] : "ExportedConfig";
            Directory.CreateDirectory(OutDir);

            Console.WriteLine($"Server.MirDB 版本: {version}.{customVersion}");
            Write("MapInfo", () => JsonSerializer.Serialize(data.Maps, opts));
            Write("ItemInfo", () => JsonSerializer.Serialize(data.Items, opts));
            Write("MonsterInfo", () => JsonSerializer.Serialize(data.Monsters, opts));
            Write("NPCInfo", () => JsonSerializer.Serialize(data.NPCs, opts));
            Write("QuestInfo", () => JsonSerializer.Serialize(data.Quests, opts));
            Write("DragonInfo", () => JsonSerializer.Serialize(data.Dragon, opts));
            Write("MagicInfo", () => JsonSerializer.Serialize(data.Magics, opts));
            Write("GameShopItem", () => JsonSerializer.Serialize(data.GameShop, opts));
            Write("ConquestInfo", () => JsonSerializer.Serialize(data.Conquests, opts));
            Write("RespawnTimer", () => JsonSerializer.Serialize(data.Respawn, opts));

            File.WriteAllText(Path.Combine(OutDir, "Meta.json"),
                JsonSerializer.Serialize(new ConfigMeta { version = version, customVersion = customVersion }, opts));

            // 往返验证：把导出的 ItemInfo.json 反序列化回官方 List<ItemInfo>，
            // 确认第二步（服务器改为从 JSON 加载配置表）在技术上可行。
            try
            {
                string json = File.ReadAllText(Path.Combine(OutDir, "ItemInfo.json"));
                var back = JsonSerializer.Deserialize<List<ItemInfo>>(json, opts);
                int withStats = 0;
                foreach (var it in back)
                    if (it.Stats != null && it.Stats.Values.Count > 0) withStats++;
                Console.WriteLine($"往返验证: 反序列化 {back.Count} 个物品，其中 {withStats} 个含 Stats 战斗属性。");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"往返验证失败: {ex.GetType().Name}: {ex.Message}");
            }

            Console.WriteLine("完成。输出目录: " + Path.GetFullPath(OutDir));
            return 0;
        }

        static void Write(string fileName, Func<string> serialize)
        {
            try
            {
                string json = serialize();
                File.WriteAllText(Path.Combine(OutDir, fileName + ".json"), json);
                Console.WriteLine($"  [OK]   {fileName}.json  ({json.Length} 字节)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [FAIL] {fileName}.json: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // 把 ItemInfo.json 的英文名按词根翻译成中文（颜色/元素/部位/材质等通用词中文化，
        // Crystal 自定义专有名词如 GonRyu 无标准中文对应则保留英文）。
        static JsonSerializerOptions GetOpts()
        {
            return new JsonSerializerOptions
            {
                WriteIndented = true,
                IncludeFields = true,
                ReferenceHandler = ReferenceHandler.Preserve,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                TypeInfoResolver = new IgnoreResolver(),
                // 关键：不转义非 ASCII，让中文直接写成 “灵刃” 而不是 “\u7075\u5203”
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };
        }

        // 把所有配置表的 Name / Title 字段按词根翻译成中文（颜色/元素/部位/材质等通用词中文化，
        // Crystal 自定义专有名词如 GonRyu 等无标准中文对应则保留英文）。
        static int LocalizeAll(string dir)
        {
            var tables = new (string File, Type Type, bool IsList)[]
            {
                ("ItemInfo.json", typeof(ItemInfo), true),
                ("MonsterInfo.json", typeof(MonsterInfo), true),
                ("NPCInfo.json", typeof(NPCInfo), true),
                ("MapInfo.json", typeof(MapInfo), true),
                ("MagicInfo.json", typeof(MagicInfo), true),
                ("QuestInfo.json", typeof(QuestInfo), true),
                ("GameShopItem.json", typeof(GameShopItem), true),
                ("ConquestInfo.json", typeof(ConquestInfo), true),
                ("DragonInfo.json", typeof(DragonInfo), false),
                ("RespawnTimer.json", typeof(RespawnTimer), false),
            };
            var opts = GetOpts();
            int totalChanged = 0, totalKept = 0;
            foreach (var t in tables)
            {
                string path = Path.Combine(dir, t.File);
                if (!File.Exists(path)) { Console.WriteLine($"  {t.File}: 不存在，跳过"); continue; }
                var fields = new[] { "Name", "Title" }.Select(n => t.Type.GetField(n))
                    .Where(f => f != null && f.FieldType == typeof(string)).ToArray();
                if (fields.Length == 0) { Console.WriteLine($"  {t.File}: 无 Name/Title 字段，跳过"); continue; }

                int c = 0, k = 0;
                if (t.IsList)
                {
                    var listType = typeof(List<>).MakeGenericType(t.Type);
                    var list = (System.Collections.IEnumerable)JsonSerializer.Deserialize(File.ReadAllText(path), listType, opts);
                    foreach (var it in list)
                        foreach (var f in fields)
                        {
                            var s = (string)f.GetValue(it);
                            var cn = Cn(s);
                            if (cn != s) { f.SetValue(it, cn); c++; } else k++;
                        }
                    File.WriteAllText(path, JsonSerializer.Serialize(list, listType, opts));
                }
                else
                {
                    var obj = JsonSerializer.Deserialize(File.ReadAllText(path), t.Type, opts);
                    foreach (var f in fields)
                    {
                        var s = (string)f.GetValue(obj);
                        var cn = Cn(s);
                        if (cn != s) { f.SetValue(obj, cn); c++; } else k++;
                    }
                    File.WriteAllText(path, JsonSerializer.Serialize(obj, t.Type, opts));
                }
                totalChanged += c; totalKept += k;
                Console.WriteLine($"  {t.File}: {c} 改 / {k} 留");
            }
            Console.WriteLine($"中文化完成: 共 {totalChanged} 个已翻译，{totalKept} 个保留原英文（专有名词）。");
            return 0;
        }

        static string Cn(string en)
        {
            if (string.IsNullOrEmpty(en)) return en;
            // 整词替换已知多词/专有名词（避免被驼峰拆分破坏），如 GonRyu → 昆流。
            // 大小写不敏感，覆盖 GonRyu / Gonryu 等拼写变体。
            var proper = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                {"GonRyu","昆流"},{"Raider","掠"},{"Tarrag","塔"},{"Unstab","不稳"},
                {"Cheong","青"},{"Nokyou","诺"},{"Hwayou","华"},{"Nephri","涅"},
                {"OmaKin","沃玛"},{"MirArm","米尔"},{"OfAgon","奥"},{"EvilDr","邪"},
                {"EvilSl","邪"},{"CraftR","匠"},{"MagicA","魔"},{"MagicS","魔"},
                {"HellYa","狱"},{"BallOf","球"},{"RedJad","红玉"},{"WhiteG","白"},
                {"BlackD","黑"},{"BlackT","黑"},{"BlackI","黑"},{"Premiu","尊"},
            };
            foreach (var kv in proper)
                en = System.Text.RegularExpressions.Regex.Replace(en, kv.Key, kv.Value, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                {"Black","黑"},{"White","白"},{"Red","红"},{"Blue","蓝"},{"Green","绿"},
                {"Gold","金"},{"Golden","金"},{"Silver","银"},{"Purple","紫"},{"Yellow","黄"},
                {"Dark","暗"},{"Light","光"},
                {"Fire","火"},{"Ice","冰"},{"Thunder","雷"},{"Lightning","雷"},{"Poison","毒"},
                {"Wind","风"},{"Water","水"},{"Earth","土"},{"Holy","圣"},{"Shadow","影"},
                {"Spirit","灵"},{"Soul","魂"},{"Dragon","龙"},{"Frost","霜"},{"Storm","暴"},
                {"Nature","自然"},{"Chaos","混沌"},{"Void","虚"},{"Abyss","渊"},{"Ghost","鬼"},
                {"Demon","魔"},{"Blood","血"},
                {"Blade","刃"},{"Sword","剑"},{"Axe","斧"},{"Spear","矛"},{"Staff","杖"},
                {"Wand","杖"},{"Bow","弓"},{"Helm","盔"},{"Helmet","盔"},{"Armor","甲"},
                {"Robe","袍"},{"Ring","戒"},{"Necklace","链"},{"Bracelet","镯"},
                {"Belt","带"},{"Boots","靴"},{"Shield","盾"},{"Cape","披风"},{"Coat","衣"},
                {"Bronze","青铜"},{"Iron","铁"},{"Steel","钢"},{"Leather","皮"},{"Magic","魔"},
                {"Mystic","秘"},{"Ancient","古"},{"Crystal","晶"},{"Bone","骨"},{"Stone","石"},
                {"Potion","药"},{"Scroll","卷"},{"Ore","矿"},{"Gem","宝"},{"Book","书"},
                {"Map","图"},{"Ticket","券"},{"Powder","粉"},{"Seal","印"},{"Charm","符"},
                {"Orb","珠"},{"Sphere","球"},{"Core","核"},{"Heart","心"},{"Eye","眼"},
                {"Fang","牙"},{"Claw","爪"},{"Horn","角"},{"Scale","鳞"},{"Wing","翼"},{"Tail","尾"},
                {"Totem","腾"},{"Idol","像"},{"Relic","物"},{"Amulet","符"},{"Talisman","符"},
                {"Health","生"},{"Mana","法"},{"Power","力"},{"Defense","防"},
                {"Attack","攻"},{"Speed","速"},{"Double","双"},{"Premium","尊"},
                {"Craft","匠"},{"Credit","信"},{"Protect","护"},{"Heaven","天"},
                {"Evil","邪"},{"Hell","狱"},{"Wonder","奇"},{"Myster","秘"},{"Summon","唤"},
                {"Destru","灭"},{"Canni","噬"},{"Formal","礼"},{"Ball","球"},{"Footba","足"},
                {"King","王"},{"Queen","后"},{"Lord","主"},{"Master","师"},{"War","战"},
                {"Battle","战"},{"Beast","兽"},{"Tiger","虎"},{"Snake","蛇"},{"Wolf","狼"},
                {"Eagle","鹰"},{"Phoenix","凤"},{"God","神"},{"Saint","圣"},{"Emperor","皇"},
                {"Prince","王"},{"Knight","骑"},{"Warrior","战"},{"Mage","法"},{"Taoist","道"},
                {"Archer","弓"},{"Assassin","刺"},{"Hunter","猎"},{"Slayer","屠"},{"Bane","祸"},
                {"Bless","祝"},{"Curse","咒"},                {"Guard","卫"},{"Keeper","守"},{"Ward","护"},
            {"Recall","召"},{"Orchid","兰"},{"Flower","花"},{"Smash","碎"},{"Pendulum","摆"},
            {"Wheel","轮"},{"Devil","魔"},{"Great","巨"},{"Hwan","幻"},{"Unstab","不稳"},
            {"Cheong","青"},{"Oma","沃玛"},{"Mir","米尔"},{"Stab","刺"},{"Jad","玉"},
            {"GonRyu","昆流"},{"Raider","掠"},{"Tarrag","塔"},{"Nokyou","诺"},
            {"Hwayou","华"},{"Nephri","涅"},{"RedJad","红玉"},{"Premiu","尊"},
            {"HellYa","狱"},{"BallOf","球"},{"Armour","甲"},
            };
            var parts = System.Text.RegularExpressions.Regex.Split(en, "(?<!^)(?=[A-Z])|(?=\\d)|(?<=\\d)(?=\\D)|[\\s_\\-]+");
            var sb = new System.Text.StringBuilder();
            foreach (var raw in parts)
            {
                if (string.IsNullOrEmpty(raw)) continue;
                if (char.IsDigit(raw[0])) { sb.Append(raw); continue; }
                if (raw.Length == 1 && char.IsLetter(raw[0])) continue;
                if (dict.TryGetValue(raw, out var zh)) sb.Append(zh);
                else sb.Append(raw);
            }
            return sb.ToString();
        }

        // ====================================================================
        // --cn-server：全量一致汉化（服务器端直接写中文名）
        // ====================================================================

        // 整词表：高频物品/关键怪物（完整匹配，优先级最高）。
        // 约束：译名不得包含空格/逗号/等号（否则会破坏 NPC 脚本与掉落表按空格分词的解析）。
        static readonly Dictionary<string, string> CnWholeWords = new(StringComparer.OrdinalIgnoreCase)
        {
            // 药水（传奇惯用名：金创药/魔法药/太阳水）
            {"SunPotion","太阳水"},
            {"(HP)DrugSmall","小金创药"},{"(HP)DrugMedium","中金创药"},{"(HP)DrugLarge","大金创药"},{"(HP)DrugXL","特制金创药"},
            {"(MP)DrugSmall","小魔法药"},{"(MP)DrugMedium","中魔法药"},{"(MP)DrugLarge","大魔法药"},{"(MP)DrugXL","特制魔法药"},
            {"TaoistDrug(S)","道士药(小)"},{"StormDrug(S)","风暴药(小)"},
            // 矿石（精炼/挖矿系统按名引用）
            {"BlackIronOre","黑铁矿"},{"IronOre","铁矿"},{"CopperOre","铜矿"},{"SilverOre","银矿"},{"GoldOre","金矿"},
            {"MythrilOre","秘银矿"},{"PlatinumOre","白金矿"},{"RubyOre","红宝石矿"},{"NephriteOre","碧玉矿"},{"AmethystOre","紫晶矿"},
            // 材料/杂物
            {"Bone","骨头"},{"LargeBone","大骨头"},
            {"WhiteThread","白线"},{"RedThread","红线"},{"BlackThread","黑线"},{"SewingGoods","缝纫包"},
            {"GreenPoison","绿毒粉"},{"RedPoison","红毒粉"},
            {"OmaTeeth","奥玛之牙"},{"BugEye","虫眼"},{"ZombieEye","僵尸眼"},{"DeerHorn","鹿角"},{"DeerBlood","鹿血"},
            {"AmuletOfRevival","复活护符"},{"ChampionTrophy[1d]","冠军奖杯[1日]"},
            // 烹饪（配方系统按名引用）
            {"Dumpling","饺子"},{"BeefDumpling","牛肉饺子"},{"ChickenDumpling","鸡肉饺子"},
            {"EggDumpling","鸡蛋饺子"},{"BoneDumpling","骨头饺子"},
            {"BoneBroth","骨头汤"},{"ChickenBroth","鸡汤"},{"EggBroth","蛋汤"},{"MeatSoup","肉汤"},
            {"HerbalTea","草药茶"},{"ZerkSoup","狂暴汤"},{"FishBait","鱼饵"},
            // 召唤系技能书物品（词根切分译不完整，必须整词，否则与掉落引用断链）
            {"SummonShinsu","召唤神兽"},{"SummonHolyDeva","召唤圣精灵"},
            {"SummonVampire","召唤吸血蜘蛛"},{"SummonSnakes","召唤魅惑之蛇"},
            // 关键怪物（CanStartEnvir 启动检查按名引用）
            {"BoneFamiliar","骷髅"},{"Shinsu","神兽"},{"BugBat","虫蝙蝠"},
            {"HellKnight1","地狱骑士1"},{"HellKnight2","地狱骑士2"},{"HellKnight3","地狱骑士3"},{"HellKnight4","地狱骑士4"},
            {"HellBomb1","地狱爆弹1"},{"HellBomb2","地狱爆弹2"},{"HellBomb3","地狱爆弹3"},
            {"WhiteSerpent","白蛇"},{"HolyDeva","圣精灵"},{"AssassinClone","刺客分身"},
            {"VampireSpider","吸血蜘蛛"},{"BombSpider","自爆蜘蛛"},{"Clone","分身"},
            {"Hugger","抱噬虫"},{"PoisonHugger","毒抱噬虫"},{"MutatedHugger","变异抱噬虫"},
            {"GiantKeratoid","巨角虫"},{"SpittingToad","喷毒蟾蜍"},{"SnakeTotem","蛇图腾"},
            {"CharmedSnake","魅惑之蛇"},{"StoneTrap","岩石陷阱"},{"AncientBat","远古蝙蝠"},
            {"WarriorScroll","战士秘卷"},{"TaoistScroll","道士秘卷"},{"WizardScroll","法师秘卷"},{"AssassinScroll","刺客秘卷"},
            {"StainHammerCat","锈锤猫"},{"BlackHammerCat","黑锤猫"},{"StrayCat","流浪猫"},{"CatShaman","猫巫师"},
            {"Hydrax","海德拉克斯"},{"HornedSorceror","有角巫师"},{"BoulderSpirit","巨岩之灵"},{"SnowWolf","雪狼"},
            {"WoomaTaurus","沃玛教主"},{"ZumaTaurus","祖玛教主"},{"KingHog","猪王"},{"Behemoth","贝希摩斯"},
            {"Skeleton","骷髅兵"},
            {"SealedHero","封印英雄"},{"BlackCreatureStone","黑灵石"},
        };

        // 词根表：完整单词（按驼峰/下划线/括号/数字切分后的整段匹配）。
        // 与旧 Cn() 的区别：这里只有完整词，绝无 "BlackI"/"Premiu"/"RedJad" 之类截断词干的前缀 hack，
        // 因此不会再出现 黑ron矿 / 黑HammerCat 这类残缺译名。
        // 注意：不要放多词复合键（如 WoodenSword）——切分后永远不会整段命中，属死代码。
        static readonly Dictionary<string, string> CnRootWords = new(StringComparer.OrdinalIgnoreCase)
        {
            // 颜色
            {"Black","黑"},{"White","白"},{"Red","红"},{"Blue","蓝"},{"Green","绿"},
            {"Gold","金"},{"Golden","金"},{"Silver","银"},{"Purple","紫"},{"Yellow","黄"},
            {"Dark","暗"},{"Light","轻"},
            // 元素/意象
            {"Fire","火"},{"Ice","冰"},{"Thunder","雷"},{"Lightning","雷"},{"Poison","毒"},
            {"Wind","风"},{"Water","水"},{"Earth","土"},{"Holy","圣"},{"Shadow","影"},
            {"Spirit","之灵"},{"Soul","魂"},{"Dragon","龙"},{"Frost","霜"},{"Storm","暴"},
            {"Nature","自然"},{"Chaos","混沌"},{"Void","虚"},{"Ghost","鬼"},
            {"Demon","魔"},{"Blood","血"},{"Sun","太阳"},{"Moon","月"},{"Star","星"},
            // 武器/防具部位/材质
            {"Blade","刃"},{"Sword","剑"},{"Axe","斧"},{"Spear","矛"},{"Staff","杖"},
            {"Wand","杖"},{"Bow","弓"},{"Helm","盔"},{"Helmet","盔"},{"Armor","甲"},{"Armour","甲"},
            {"Robe","袍"},{"Ring","戒"},{"Necklace","项链"},{"Bracelet","手镯"},{"Glove","手套"},
            {"Gloves","拳套"},{"Belt","腰带"},{"Boots","靴"},{"Shield","盾"},{"Cape","披风"},{"Coat","衣"},
            {"Bronze","青铜"},{"Iron","铁"},{"Steel","钢"},{"Leather","皮革"},{"Cloth","布"},
            {"Wooden","木"},{"Ebony","乌木"},{"Hoa","豪"},{"Sharp","锋利"},{"Thin","细"},
            {"Glass","琉璃"},{"Hexagonal","六角"},{"Precision","精准"},{"Accuracy","准确"},
            {"Agility","敏捷"},{"Endurance","耐力"},{"Durability","耐久"},{"Disillusion","幻灭"},
            {"Freezing","冰冻"},{"Protection","防护"},{"Bravery","勇气"},{"Magic","魔法"},
            {"Skill","技能"},{"Mystic","秘"},{"Ancient","远古"},{"Crystal","水晶"},
            {"Bone","骨"},{"Stone","石"},{"Potion","药水"},{"Scroll","卷轴"},{"Ore","矿"},
            {"Gem","宝石"},{"Book","书"},{"Map","图"},{"Ticket","券"},{"Powder","粉"},
            {"Seal","印"},{"Orb","珠"},{"Sphere","球"},{"Core","核"},{"Heart","心"},
            {"Eye","眼"},{"Fang","牙"},{"Claw","爪"},{"Horn","角"},{"Scale","鳞"},
            {"Wing","翼"},{"Tail","尾"},{"Totem","图腾"},{"Idol","像"},{"Relic","圣物"},
            {"Amulet","护符"},{"Talisman","符"},{"Health","生命"},{"Mana","魔法"},
            {"Power","力量"},{"Defense","防御"},{"Attack","攻击"},{"Speed","速度"},
            {"Double","双"},{"Premium","尊享"},{"Craft","匠"},{"Credit","信用"},
            {"Protect","守护"},{"Heaven","天"},{"Evil","邪"},{"Hell","地狱"},
            {"Wonder","奇"},{"Mystery","谜"},{"Summon","召唤"},{"Cannibal","食人"},
            {"Formal","礼"},{"Ball","球"},{"Fortune","福"},{"Box","箱"},
            // 身份/职业
            {"King","王"},{"Queen","后"},{"Lord","领主"},{"Master","大师"},{"War","战争"},
            {"Battle","战斗"},{"Beast","兽"},{"Tiger","虎"},{"Snake","蛇"},{"Wolf","狼"},
            {"Eagle","鹰"},{"Phoenix","凤凰"},{"God","神"},{"Saint","圣"},{"Emperor","皇"},
            {"Prince","王子"},{"Knight","骑士"},{"Warrior","武士"},{"Mage","法师"},
            {"Taoist","道士"},{"Archer","弓手"},{"Assassin","刺客"},{"Hunter","猎手"},
            {"Slayer","斩杀"},{"Bless","祝福"},{"Curse","诅咒"},{"Guard","守卫"},
            {"Keeper","守望者"},{"Ward","守护"},{"Recall","召回"},{"Flower","花"},
            {"Wheel","轮"},{"Devil","恶魔"},{"Great","巨"},{"Giant","巨"},{"Hero","英雄"},
            // 怪物词根
            {"Skeleton","骷髅"},{"Zombie","僵尸"},{"Ghoul","食尸鬼"},{"Spider","蜘蛛"},
            {"Scorpion","蝎子"},{"Boar","野猪"},{"Maggot","蛆"},{"Bat","蝙蝠"},{"Cat","猫"},
            {"Rat","鼠"},{"Ape","猿"},{"Moth","蛾"},{"Viper","毒蛇"},{"Serpent","蛇"},
            {"Worm","蠕虫"},{"Bee","蜂"},{"Turtle","龟"},{"Toad","蟾蜍"},{"Hog","猪"},
            {"Fox","狐"},{"Frog","蛙"},{"Baboon","狒狒"},{"Minotaur","牛头怪"},{"Warg","恶狼"},
            {"Rabbit","兔"},{"Bull","公牛"},{"Bear","熊"},{"Chick","小鸡"},{"Deer","鹿"},
            {"Yob","小混混"},{"Scarecrow","稻草人"},{"Statue","雕像"},{"Guardian","守卫"},
            {"Soldier","士兵"},{"Fighter","战士"},{"Priest","祭司"},{"Shaman","巫医"},
            {"Warlock","术士"},{"Rogue","盗贼"},{"Whoo","枭"},{"Familiar","使魔"},
            {"Keratoid","角虫"},{"Hugger","抱噬虫"},{"Wraith","幽魂"},{"Phantom","幻影"},
            {"Pilar","石柱"},{"Solider","士兵"},{"Trap","陷阱"},{"Blast","爆裂"},
            {"Blaster","爆破手"},{"KekTal","甲虫"},{"Dung","粪怪"},{"Slasher","砍杀者"},
            {"Pirate","海盗"},{"Bolt","弩箭"},{"Crawler","爬行者"},{"Centipede","蜈蚣"},
            {"Weaver","织网蛛"},{"Barbarian","蛮兵"},{"Ronin","浪人"},{"Toxic","毒"},
            {"Shi","尸"},{"Hungry","饥饿"},{"Manectric","雷电兽"},{"Devourer","吞噬者"},
            {"Witch","女巫"},{"Doctor","医生"},{"Tucson","图森"},{"Snail","蜗牛"},
            {"General","将军"},{"Egg","蛋"},{"Young","幼年"},{"Sand","沙"},{"Tongs","钳虫"},
            {"Nipper","钳手"},{"Vale","谷地"},{"Cracking","裂壳"},{"Root","根须"},
            {"Lure","诱惑"},{"Gang","巨颚"},{"Big","大"},{"Small","小"},{"Elite","精英"},
            {"Awakening","觉醒"},{"Frozen","冰封"},{"Flaming","烈焰"},{"Electric","雷电"},
            {"Winged","有翼"},{"Flail","连枷"},{"Crossbow","弩"},{"Yin","阴"},{"Node","节点"},
            {"Dream","梦境"},{"Incarnated","化身"},{"Cursed","受诅"},{"Burning","燃烧"},
            {"Mud","泥"},{"Spearman","矛兵"},{"Blademan","刀兵"},{"Captain","队长"},
            {"Hedge","刺猬"},{"Fishing","钓鱼"},{"Strongbox","保险箱"},{"Blackstone","黑石"},
            {"Accessory","饰品"},{"Rune","符文"},{"Dice","骰子"},{"Group","团队"},
            {"Robot","机器人"},{"Default","默认"},{"Monster","怪物"},{"Wedge","楔"},
            {"Taurus","教主"},{"Finial","檐"},{"Tower","塔"},{"Palace","宫殿"},{"Wall","墙"},
            {"Michelangelo","米开朗基罗"},{"Raphael","拉斐尔"},{"Donatello","多纳泰罗"},
            {"Left","左"},{"Right","右"},{"Elder","长老"},{"Chieftain","酋长"},
            {"Armadillo","犰狳"},{"Horo","吼罗"},{"Khadzard","卡扎德"},
            {"Whimpering","哀鸣"},{"Currish","野犬"},{"Warewolf","狼人"},{"Forest","森林"},
            {"Yeti","雪怪"},{"Hooking","钩爪"},{"Decapitator","断头斧"},{"Hood","兜帽"},
            {"Bug","虫"},{"Thread","线"},{"Silk","丝"},{"Pearl","珍珠"},{"Coral","珊瑚"},
            {"Ruby","红宝石"},{"Adamantine","精金"},{"Platinum","铂金"},{"Nephrite","碧玉"},
            {"Jade","玉"},{"Twin","双生"},{"Drake","龙兽"},{"Malefic","凶煞"},
            {"Lithe","轻盈"},{"Kriss","短剑"},{"Hooked","钩"},{"Compound","复合"},
            // 传奇经典地名/种族/专有名词
            {"Oma","奥玛"},{"Wooma","沃玛"},{"Zuma","祖玛"},{"Mir","米尔"},
            {"Bichon","比奇"},{"Woomyon","沃玛森林"},{"Mongchon","盟重"},
            {"Prajna","般若"},{"Past","过往"},{"Sabuk","沙巴克"},{"GonRyu","昆流"},
            {"Cheong","青"},{"Nokyou","诺"},{"Hwayou","华"},{"Nephri","涅"},
            {"Tarrag","塔拉格"},{"Raider","掠夺者"},{"Stab","刺"},{"Unstab","不稳"},
            // 第二轮补充（来自首轮运行后的残留词统计）
            {"Raiders","掠夺者"},{"Long","长"},{"Dagger","匕首"},{"Base","基础"},
            {"Dress","布衣"},{"Purifier","净化"},{"Martial","武侠"},{"Circle","轮"},
            {"Spring","泉"},{"Five","五"},{"String","弦"},{"Blades","刃"},
            {"Meat","肉"},{"Old","老"},{"Ginseng","山参"},{"Wine","酒"},
            {"Glyph","纹章"},{"Pendant","坠子"},{"Token","代币"},{"Solid","坚硬"},
            {"Tough","坚韧"},{"Tempered","锻造"},{"Mask","面具"},{"Bell","铃"},
            {"Pill","丹"},{"Sabre","军刀"},{"Bangle","手环"},{"Liquor","烈酒"},
            {"Pass","通行证"},{"Collar","项圈"},{"Carnation","康乃馨"},
            {"Footballer","球员"},{"Football","足球"},{"Shoes","鞋"},{"Brace","镯"},
            {"EXP","经验"},{"Drug","药"},{"Tao","道"},{"Rock","岩石"},
            {"Cave","洞穴"},{"Minion","爪牙"},{"Man","人"},{"Venom","毒液"},
            {"Slave","仆从"},{"Keel","龙骨"},{"Sky","天"},{"Stinger","毒刺"},
            {"Royal","皇家"},{"Tree","树"},{"Kitten","小猫"},{"Baby","幼"},
            {"Jar","坛"},{"Grass","草"},{"Yang","阳"},{"Element","元素"},
            {"Chestnut","栗子"},{"Horned","有角"},{"Of","之"},{"Agony","痛苦"},
            {"Strength","力量"},{"Doom","末日"},{"Unstable","不稳"},{"Pearll","珍珠"},
            {"Yama","阎罗"},{"Gonryun","昆云"},{"Tarragon","塔拉贡"},{"Cheongbo","青宝"},
        };

        // 翻译单个名字：整词优先 → 剥离 (XX) 尾缀 → 词根逐段翻译 → 拼回尾缀。
        // 输出保证不含空格（脚本按空格分词）。译不出的词根保留英文（数据本就中英混名）。
        static string TranslateName(string en)
        {
            if (string.IsNullOrEmpty(en)) return en;
            // 已含中文的名字（原数据可能残留）原样保留
            if (System.Text.RegularExpressions.Regex.IsMatch(en, "[\\u4e00-\\u9fff]")) return en;
            if (CnWholeWords.TryGetValue(en, out var whole)) return whole;

            // 剥离尾部 (M)/(F)/(H)/(S)/(XL) 等括号尾缀，原样拼回
            var m = System.Text.RegularExpressions.Regex.Match(en, "\\([A-Za-z0-9]+\\)$");
            string suffix = "", core = en;
            if (m.Success) { suffix = m.Value; core = en.Substring(0, m.Index); }

            var parts = System.Text.RegularExpressions.Regex.Split(core,
                "(?<!^)(?=[A-Z])|(?=\\d)|(?<=\\d)(?=\\D)|(?=\\()|(?<=\\))|[\\s_]+");
            var sb = new System.Text.StringBuilder();
            foreach (var raw in parts)
            {
                if (string.IsNullOrEmpty(raw)) continue;
                if (char.IsDigit(raw[0])) { sb.Append(raw); continue; }
                if (CnRootWords.TryGetValue(raw, out var zh)) sb.Append(zh);
                else sb.Append(raw);
            }
            return sb.ToString() + suffix;
        }

        // 主流程：改写配置表 Name + 同步重写所有引用
        static int LocalizeServer(string configDir, string[] dataRoots)
        {
            string itemPath = Path.Combine(configDir, "ItemInfo.json");
            string monsterPath = Path.Combine(configDir, "MonsterInfo.json");
            if (!File.Exists(itemPath) || !File.Exists(monsterPath))
            {
                Console.WriteLine($"找不到配置表: {itemPath} / {monsterPath}");
                return 1;
            }

            // ---- 1. 建立英→中映射（含冲突检测：两英文名译成同一中文时后者保留英文） ----
            // 幂等支持：configDir 可能已汉化（Name 为中文），此时用工具自带的英文原版导出
            // ExportedConfig 按序配对还原英文名，保证重复运行仍能得到完整英→中映射。
            string exportDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "ExportedConfig"));
            var enSource = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase); // 表名 -> 英文名列表
            foreach (var table in new[] { "ItemInfo.json", "MonsterInfo.json" })
            {
                string p = Path.Combine(exportDir, table);
                if (!File.Exists(p)) continue;
                var names = new List<string>();
                using (var d = System.Text.Json.JsonDocument.Parse(File.ReadAllText(p)))
                    foreach (var el in d.RootElement.GetProperty("$values").EnumerateArray())
                        names.Add(el.GetProperty("Name").GetString());
                enSource[table] = names;
            }
            if (enSource.Count > 0) Console.WriteLine($"英文原版导出: {exportDir}（用于幂等配对）");

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var cnOwner = new Dictionary<string, string>(StringComparer.Ordinal); // 中文 -> 首个英文名
            var collisions = new List<string>();
            int translated = 0, kept = 0;
            foreach (var path in new[] { itemPath, monsterPath })
            {
                string table = Path.GetFileName(path);
                var curNames = new List<string>();
                using (var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path)))
                    foreach (var el in doc.RootElement.GetProperty("$values").EnumerateArray())
                        curNames.Add(el.GetProperty("Name").GetString());

                for (int i = 0; i < curNames.Count; i++)
                {
                    string cur = curNames[i];
                    // 已是中文的名字：按序从英文原版配对还原英文名（顺序一致才可信）
                    string en = cur;
                    if (System.Text.RegularExpressions.Regex.IsMatch(cur ?? "", "[\\u4e00-\\u9fff]") &&
                        enSource.TryGetValue(table, out var ens) && i < ens.Count)
                    {
                        en = ens[i];
                    }
                    if (string.IsNullOrEmpty(en) || map.ContainsKey(en)) continue;
                    string cn = TranslateName(en);
                    if (cn == en) { map[en] = en; kept++; continue; }
                    if (cnOwner.TryGetValue(cn, out var owner))
                    {
                        collisions.Add($"{en} -> {cn}（与 {owner} 冲突，保留英文）");
                        map[en] = en; kept++; continue;
                    }
                    cnOwner[cn] = en;
                    map[en] = cn; translated++;
                }
            }
            Console.WriteLine($"名字映射: {translated} 个翻译 / {kept} 个保留英文");
            foreach (var c in collisions) Console.WriteLine("  [冲突] " + c);

            // ---- 2. 原位改写 ItemInfo.json / MonsterInfo.json 的 Name（JsonNode 保留 $id 结构） ----
            var jsonOpts = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };
            foreach (var path in new[] { itemPath, monsterPath })
            {
                bool isMonster = Path.GetFileName(path) == "MonsterInfo.json";
                var root = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path));
                int changed = 0;
                foreach (var el in root["$values"].AsArray())
                {
                    string en = el["Name"].GetValue<string>();
                    if (!map.TryGetValue(en, out var cn)) cn = en; // 已是中文（或未知），不改写
                    if (cn != en)
                    {
                        el["Name"] = cn;
                        changed++;
                        // 怪物改名后，若 DropPath 为空且旧名掉落文件存在 → 用旧名兜底定位掉落文件
                        if (isMonster &&
                            string.IsNullOrEmpty((string)el["DropPath"]) &&
                            dataRoots.Any(r => File.Exists(Path.Combine(r, "Envir", "Drops", en + ".txt"))))
                        {
                            el["DropPath"] = en;
                        }
                    }
                }
                File.WriteAllText(path, root.ToJsonString(jsonOpts));
                Console.WriteLine($"  {Path.GetFileName(path)}: {changed} 个 Name 已改写");
            }

            // ---- 2b. DragonInfo.json 的 MonsterName（带空格的名字如 "Evil Mir"，去空格查映射） ----
            string dragonPath = Path.Combine(configDir, "DragonInfo.json");
            if (File.Exists(dragonPath))
            {
                var dragon = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(dragonPath));
                string monName = dragon["MonsterName"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(monName) &&
                    map.TryGetValue(monName.Replace(" ", ""), out var monCn) && monCn != monName)
                {
                    dragon["MonsterName"] = monCn;
                    File.WriteAllText(dragonPath, dragon.ToJsonString(jsonOpts));
                    Console.WriteLine($"  DragonInfo.json: MonsterName {monName} -> {monCn}");
                }
            }

            // ---- 3. 重写所有数据根目录的 Envir\**\*.txt（掉落/配方/NPC脚本/任务/事件…） ----
            var replacePairs = map.Where(kv => kv.Value != kv.Key)
                .OrderByDescending(kv => kv.Key.Length).ToList();
            if (replacePairs.Count == 0) { Console.WriteLine("无可替换名字。"); return 0; }
            var rx = new System.Text.RegularExpressions.Regex(
                "(?<![A-Za-z0-9_])(?:" + string.Join("|", replacePairs.Select(kv => System.Text.RegularExpressions.Regex.Escape(kv.Key))) + ")(?![A-Za-z0-9_])",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var mapCI = new Dictionary<string, string>(map, StringComparer.OrdinalIgnoreCase);

            foreach (var root in dataRoots)
            {
                string envir = Path.Combine(root, "Envir");
                if (Directory.Exists(envir))
                {
                    int files = 0, hits = 0;
                    foreach (var file in Directory.EnumerateFiles(envir, "*.txt", SearchOption.AllDirectories))
                    {
                        var (text, bom) = ReadTextDetectBom(file);
                        string newText = rx.Replace(text, m => mapCI[m.Value]);
                        if (newText != text)
                        {
                            WriteTextKeepBom(file, newText, bom);
                            files++; hits += rx.Matches(text).Count;
                        }
                    }
                    Console.WriteLine($"  [{Path.GetFileName(root)}] Envir: {files} 个文件重写, {hits} 处替换");
                }

                // ---- 3b. 重命名 Envir\Recipe\*.txt（配方文件名 = 配方名 = 产物物品名，必须与物品表中文名一致，
                //      否则“未找到物品：Dumpling”/NPC 脚本“未找到配方”）
                string recipePath = Path.Combine(root, "Envir", "Recipe");
                if (Directory.Exists(recipePath))
                {
                    int renamed = 0;
                    foreach (var file in Directory.GetFiles(recipePath, "*.txt"))
                    {
                        string name = Path.GetFileNameWithoutExtension(file);
                        if (mapCI.TryGetValue(name, out var cn) && cn != name &&
                            !File.Exists(Path.Combine(recipePath, cn + ".txt")))
                        {
                            File.Move(file, Path.Combine(recipePath, cn + ".txt"));
                            renamed++;
                        }
                    }
                    Console.WriteLine($"  [{Path.GetFileName(root)}] Recipe: {renamed} 个配方文件已重命名为中文");
                }

                // ---- 4. 重写 Configs\*.ini 里“值恰好是名字”的键（Setup.ini 启动检查名、矿/鱼/公会等） ----
                string configs = Path.Combine(root, "Configs");
                if (Directory.Exists(configs))
                {
                    var iniLine = new System.Text.RegularExpressions.Regex(
                        "^(\\s*[\\w.\\[\\]-]+\\s*=\\s*)([^;\\r\\n]*?)(\\s*)$");
                    int files = 0, hits = 0;
                    foreach (var file in Directory.EnumerateFiles(configs, "*.ini"))
                    {
                        var (text, bom) = ReadTextDetectBom(file);
                        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                        bool changed = false;
                        for (int i = 0; i < lines.Length; i++)
                        {
                            var m = iniLine.Match(lines[i]);
                            if (!m.Success) continue;
                            string val = m.Groups[2].Value.Trim();
                            if (val.Length > 0 && mapCI.TryGetValue(val, out var cn) && cn != val)
                            {
                                lines[i] = m.Groups[1].Value + cn + m.Groups[3].Value;
                                changed = true; hits++;
                            }
                        }
                        if (changed)
                        {
                            WriteTextKeepBom(file, string.Join("\r\n", lines), bom);
                            files++;
                        }
                    }
                    Console.WriteLine($"  [{Path.GetFileName(root)}] Configs: {files} 个 ini 重写, {hits} 处替换");
                }
            }

            Console.WriteLine("全量汉化完成。请重启资源服务器(start-crystal.bat)与游戏服务器验证日志无“未找到”。");
            return 0;
        }

        // 读取文本并检测 UTF-8 BOM（写回时保持原样）
        static (string text, bool bom) ReadTextDetectBom(string path)
        {
            var bytes = File.ReadAllBytes(path);
            bool bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            string text = Encoding.UTF8.GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));
            return (text, bom);
        }

        static void WriteTextKeepBom(string path, string text, bool bom)
        {
            var bytes = new List<byte>();
            if (bom) bytes.AddRange(new byte[] { 0xEF, 0xBB, 0xBF });
            bytes.AddRange(Encoding.UTF8.GetBytes(text));
            File.WriteAllBytes(path, bytes.ToArray());
        }

    }

    /// <summary>
    /// 在序列化时移除不可序列化（指针 / byref / LinkedListNode 等运行时引用）的属性，
    /// 这些字段属于运行时状态（如 QuestInfo 内指向 NPC 实例的链表节点），不属于静态配置。
    /// </summary>
    internal sealed class IgnoreResolver : DefaultJsonTypeInfoResolver
    {
        public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            JsonTypeInfo ti = base.GetTypeInfo(type, options);
            if (ti == null) return ti;
            foreach (var p in ti.Properties.ToList())
            {
                var member = p.AttributeProvider as System.Reflection.MemberInfo;
                if (member is System.Reflection.PropertyInfo pi)
                {
                    // 属性：只保留有 setter 的（如 Stats.Values），移除只读计算属性
                    // （如 QuestInfo.FinishNPC 调 Envir.NPCs.Single() 在空匹配时抛异常、ItemInfo.FriendlyName 等）。
                    if (!pi.CanWrite) { ti.Properties.Remove(p); continue; }
                }
                Type pt = p.PropertyType;
                if (pt == null) continue;
                if (pt.IsPointer || pt.IsByRef ||
                    (pt.IsGenericType && pt.GetGenericTypeDefinition() == typeof(LinkedListNode<>)))
                {
                    ti.Properties.Remove(p);
                }
            }
            return ti;
        }
    }
}
