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
