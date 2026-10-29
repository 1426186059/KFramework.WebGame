using System;
using System.IO;
using System.Text;
using System.Linq;
using Server.MirDatabase;
using Server.MirEnvir;

class Program
{
    static int Main()
    {
        string dbPath = @"D:\OpenSource\KFramework.WebGame\KFramework.Mir.OriginMri2\Web_Mir2.Server.Version\Server.MirDB";
        if (!File.Exists(dbPath)) { Console.WriteLine("找不到 " + dbPath); return 1; }
        using var fs = File.OpenRead(dbPath);
        using var reader = new BinaryReader(fs, Encoding.UTF8);
        int version = reader.ReadInt32();
        int customVersion = reader.ReadInt32();
        Envir.LoadVersion = version;
        Envir.LoadCustomVersion = customVersion;

        // 严格按 MirDBToJson 的块顺序消费索引段与数据块，避免错位
        reader.ReadInt32(); reader.ReadInt32(); reader.ReadInt32(); reader.ReadInt32(); reader.ReadInt32();
        if (version >= 63) reader.ReadInt32();
        if (version >= 66) reader.ReadInt32();
        if (version >= 68) reader.ReadInt32();

        int c = reader.ReadInt32(); // MapInfo
        var maps = new System.Collections.Generic.List<MapInfo>();
        for (int i = 0; i < c; i++) maps.Add(new MapInfo(reader));

        c = reader.ReadInt32(); // ItemInfo
        for (int i = 0; i < c; i++) new ItemInfo(reader, version, customVersion);
        c = reader.ReadInt32(); // MonsterInfo
        for (int i = 0; i < c; i++) new MonsterInfo(reader);
        c = reader.ReadInt32(); // NPCInfo
        var npcs = new System.Collections.Generic.List<NPCInfo>();
        for (int i = 0; i < c; i++) npcs.Add(new NPCInfo(reader));
        c = reader.ReadInt32(); // QuestInfo
        for (int i = 0; i < c; i++) new QuestInfo(reader);
        new DragonInfo(reader);
        c = reader.ReadInt32(); // MagicInfo
        for (int i = 0; i < c; i++) new MagicInfo(reader, version, customVersion);
        if (version >= 63) { c = reader.ReadInt32(); for (int i = 0; i < c; i++) new GameShopItem(reader, version, customVersion); }
        if (version >= 66) { c = reader.ReadInt32(); for (int i = 0; i < c; i++) new ConquestInfo(reader); }
        if (version > 67) new RespawnTimer(reader);

        int totalSZ = maps.Sum(x => x.SafeZones.Count);
        int totalRS = maps.Sum(x => x.Respawns.Count);
        int totalMV = maps.Sum(x => x.Movements.Count);
        int totalMZ = maps.Sum(x => x.MineZones.Count);

        Console.WriteLine($"version {version}.{customVersion}  maps={maps.Count}  globalNPCs={npcs.Count}");
        Console.WriteLine($"全库 TOTAL: SafeZones={totalSZ}  Respawns={totalRS}  Movements={totalMV}  MineZones={totalMZ}");
        var m = maps.FirstOrDefault(x => x.Index == 103);
        if (m != null)
            Console.WriteLine($"map103({m.Title}) SafeZones={m.SafeZones.Count} Respawns={m.Respawns.Count} Movements={m.Movements.Count} MineZones={m.MineZones.Count}");
        else
            Console.WriteLine("未找到 map Index=103");
        Console.WriteLine($"全局 NPC 中 MapIndex==103 的数量: {npcs.Count(x => x.MapIndex == 103)}");
        return 0;
    }
}
