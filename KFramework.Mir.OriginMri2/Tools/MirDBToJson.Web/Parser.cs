using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Server.MirDatabase;
using Server.MirEnvir;

namespace MirDBToJsonWeb
{
    /// <summary>
    /// 与 Envir.LoadDB 完全一致的块顺序 / 版本门控反序列化。
    /// 复用官方数据类的 BinaryReader 构造器，因此能正确解析 Crystal / OriginMri2 的 Server.MirDB
    /// （二者 Envir.Version 均为 117）。
    /// </summary>
    public class ConfigMeta
    {
        public int version { get; set; }
        public int customVersion { get; set; }
    }

    public class ParseResult
    {
        public ConfigMeta meta { get; set; }
        public string source { get; set; }        // "upload" 或 "path"
        public string fileName { get; set; }      // 原始文件名（上传）或完整路径（路径）
        public long sizeBytes { get; set; }
        public DateTime parsedAt { get; set; }
        public int mapCount { get; set; }
        public int itemCount { get; set; }
        public int monsterCount { get; set; }
        public int npcCount { get; set; }
        public int questCount { get; set; }
        public int magicCount { get; set; }
        public int gameShopCount { get; set; }
        public int conquestCount { get; set; }
        public List<object> maps { get; set; }
        public List<object> items { get; set; }
        public List<object> monsters { get; set; }
        public List<object> npcs { get; set; }
        public List<object> quests { get; set; }
        public List<object> magics { get; set; }
        public List<object> gameShop { get; set; }
        public List<object> conquests { get; set; }
    }

    public static class MirDBParser
    {
        // Envir.LoadVersion 是官方各 BinaryReader 构造器内部依赖的静态版本号，解析时需先写回。
        // 用锁保证单进程内解析串行，避免并发改同一静态值导致读取错位。
        private static readonly object _lock = new object();

        public static ParseResult Parse(Stream stream, string sourceLabel, string fileName, long sizeBytes)
        {
            lock (_lock)
            {
                int version, customVersion;
                var maps = new List<MapInfo>();
                var items = new List<ItemInfo>();
                var monsters = new List<MonsterInfo>();
                var npcs = new List<NPCInfo>();
                var quests = new List<QuestInfo>();
                var magics = new List<MagicInfo>();
                var gameShop = new List<GameShopItem>();
                var conquests = new List<ConquestInfo>();
                DragonInfo dragon = null;
                RespawnTimer respawn = null;

                using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
                {
                    version = reader.ReadInt32();
                    customVersion = reader.ReadInt32();
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
                    c = reader.ReadInt32(); for (int i = 0; i < c; i++) maps.Add(new MapInfo(reader));
                    c = reader.ReadInt32(); for (int i = 0; i < c; i++) items.Add(new ItemInfo(reader, version, customVersion));
                    c = reader.ReadInt32(); for (int i = 0; i < c; i++) monsters.Add(new MonsterInfo(reader));
                    c = reader.ReadInt32(); for (int i = 0; i < c; i++) npcs.Add(new NPCInfo(reader));
                    c = reader.ReadInt32(); for (int i = 0; i < c; i++) quests.Add(new QuestInfo(reader));
                    dragon = new DragonInfo(reader);
                    c = reader.ReadInt32(); for (int i = 0; i < c; i++) magics.Add(new MagicInfo(reader, version, customVersion));
                    if (version >= 63) { c = reader.ReadInt32(); for (int i = 0; i < c; i++) gameShop.Add(new GameShopItem(reader, version, customVersion)); }
                    if (version >= 66) { c = reader.ReadInt32(); for (int i = 0; i < c; i++) conquests.Add(new ConquestInfo(reader)); }
                    if (version > 67) respawn = new RespawnTimer(reader);
                }

                return new ParseResult
                {
                    meta = new ConfigMeta { version = version, customVersion = customVersion },
                    source = sourceLabel,
                    fileName = fileName,
                    sizeBytes = sizeBytes,
                    parsedAt = DateTime.Now,
                    mapCount = maps.Count,
                    itemCount = items.Count,
                    monsterCount = monsters.Count,
                    npcCount = npcs.Count,
                    questCount = quests.Count,
                    magicCount = magics.Count,
                    gameShopCount = gameShop.Count,
                    conquestCount = conquests.Count,
                    // 地图投影为干净 DTO（去掉 Info 反向引用，避免循环），安全区/传送/刷怪/NPC 一目了然
                    maps = maps.Select(MapView).Cast<object>().ToList(),
                    items = items.Select(Summary).Cast<object>().ToList(),
                    monsters = monsters.Select(Summary).Cast<object>().ToList(),
                    npcs = npcs.Select(NpcView).Cast<object>().ToList(),
                    quests = quests.Select(Summary).Cast<object>().ToList(),
                    magics = magics.Select(Summary).Cast<object>().ToList(),
                    gameShop = gameShop.Select(Summary).Cast<object>().ToList(),
                    conquests = conquests.Select(Summary).Cast<object>().ToList(),
                };
            }
        }

        static object MapView(MapInfo m)
        {
            return new
            {
                index = m.Index,
                fileName = m.FileName,
                title = m.Title,
                miniMap = m.MiniMap,
                bigMap = m.BigMap,
                light = m.Light.ToString(),
                music = m.Music,
                noTeleport = m.NoTeleport,
                noRecall = m.NoRecall,
                noReconnect = m.NoReconnect,
                safeZones = m.SafeZones.Select(sz => new
                {
                    x = sz.Location.X,
                    y = sz.Location.Y,
                    size = sz.Size,
                    sizeY = sz.SizeY,
                    rotation = sz.Rotation,
                    startPoint = sz.StartPoint,
                }).ToArray(),
                movementCount = m.Movements.Count,
                respawnCount = m.Respawns.Count,
                npcCount = m.NPCs.Count,
            };
        }

        static object NpcView(NPCInfo n)
        {
            var t = n.GetType();
            int x = 0, y = 0;
            var lp = GetValue(n, "Location");
            if (lp is Point p) { x = p.X; y = p.Y; }
            return new
            {
                index = GetInt(n, "Index"),
                name = GetStr(n, new[] { "Name", "Title" }),
                fileName = GetStr(n, new[] { "FileName" }),
                mapIndex = GetInt(n, "MapIndex"),
                x, y,
            };
        }

        // 物品/怪物/任务/技能/商城/行会战：只需 索引 + 名称 即可检索
        static object Summary(object o)
        {
            return new
            {
                index = GetInt(o, "Index"),
                name = GetStr(o, new[] { "Name", "Title", "ItemName", "MonsterName" }),
            };
        }

        static int GetInt(object o, string field)
        {
            var f = o.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance);
            if (f != null && f.FieldType == typeof(int)) return (int)f.GetValue(o);
            var p = o.GetType().GetProperty(field, BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.PropertyType == typeof(int) && p.CanRead) return (int)p.GetValue(o);
            return 0;
        }

        static string GetStr(object o, string[] fields)
        {
            foreach (var fn in fields)
            {
                var f = o.GetType().GetField(fn, BindingFlags.Public | BindingFlags.Instance);
                if (f != null && f.FieldType == typeof(string)) return (string)f.GetValue(o);
                var p = o.GetType().GetProperty(fn, BindingFlags.Public | BindingFlags.Instance);
                if (p != null && p.PropertyType == typeof(string) && p.CanRead) return (string)p.GetValue(o);
            }
            return null;
        }

        static object GetValue(object o, string field)
        {
            var f = o.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance);
            if (f != null) return f.GetValue(o);
            var p = o.GetType().GetProperty(field, BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.CanRead) return p.GetValue(o);
            return null;
        }
    }
}
