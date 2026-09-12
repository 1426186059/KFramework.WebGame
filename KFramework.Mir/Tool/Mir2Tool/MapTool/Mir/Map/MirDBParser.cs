using System.Text;

namespace Mir.Map
{
    /// <summary>
    /// 移植自 Unity 工程 Assets/Editor/MirDBParser.cs
    /// 解析 Server.MirDB 二进制文件，提取所有地图的 (MapName → MiniMap, BigMap) 映射。
    /// 二进制格式与 Crystal Server 的 LoadDB() 完全一致。
    /// </summary>
    public static class MirDBParser
    {
        public struct MapEntry
        {
            public int Index;
            public string FileName;  // 如 "66"（无扩展名）
            public string Title;
            public ushort MiniMap;
            public ushort BigMap;
        }

        /// <summary>
        /// 解析 MirDB 文件，返回所有 MapInfo 条目列表。
        /// </summary>
        public static List<MapEntry>? Parse(string dbPath)
        {
            if (!File.Exists(dbPath))
            {
                MirLog.LogError($"MirDB 文件不存在: {dbPath}");
                return null;
            }

            var entries = new List<MapEntry>();
            using (var fs = new FileStream(dbPath, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(fs, Encoding.UTF8))
            {
                // ---- Header ----
                int version = reader.ReadInt32();
                int customVersion = reader.ReadInt32();
                MirLog.Log($"MirDB: LoadVersion={version}, CustomVersion={customVersion}");

                // ---- Indices ----
                SkipInt32(reader); // MapIndex
                SkipInt32(reader); // ItemIndex
                SkipInt32(reader); // MonsterIndex
                SkipInt32(reader); // NPCIndex
                SkipInt32(reader); // QuestIndex
                if (version >= 63) SkipInt32(reader); // GameShopIndex
                if (version >= 66) SkipInt32(reader); // ConquestIndex
                if (version >= 68) SkipInt32(reader); // RespawnIndex

                // ---- MapInfo Block ----
                int mapCount = reader.ReadInt32();
                MirLog.Log($"MirDB: MapInfo count = {mapCount}");

                for (int i = 0; i < mapCount; i++)
                {
                    try
                    {
                        var entry = ReadMapInfo(reader, version, customVersion);
                        if (entry.HasValue)
                            entries.Add(entry.Value);
                    }
                    catch (Exception ex)
                    {
                        MirLog.LogError($"MirDB MapInfo #{i} 解析失败: {ex.Message}");
                        break;
                    }
                }
            }

            MirLog.Log($"MirDB 解析完成: 共读取 {entries.Count} 张地图");
            return entries;
        }

        /// <summary>
        /// 解析单条 MapInfo，返回我们关心的字段，同时跳过其余字段。
        /// 字段顺序与 Server/MirDatabase/MapInfo.cs:MapInfo(BinaryReader) 一致。
        /// </summary>
        static MapEntry? ReadMapInfo(BinaryReader reader, int version, int customVersion)
        {
            int index = reader.ReadInt32();
            string fileName = reader.ReadString();
            string title = reader.ReadString();
            ushort miniMap = reader.ReadUInt16();
            reader.ReadByte();  // Light
            ushort bigMap = reader.ReadUInt16();

            // --- 跳过子列表 ---
            SkipSafeZones(reader);
            SkipRespawns(reader, version, customVersion);
            SkipMovements(reader, version);

            // --- 固定布尔/整数字段 ---
            reader.ReadBoolean(); // NoTeleport
            reader.ReadBoolean(); // NoReconnect
            reader.ReadString();  // NoReconnectMap
            reader.ReadBoolean(); // NoRandom
            reader.ReadBoolean(); // NoEscape
            reader.ReadBoolean(); // NoRecall
            reader.ReadBoolean(); // NoDrug
            reader.ReadBoolean(); // NoPosition
            reader.ReadBoolean(); // NoThrowItem
            reader.ReadBoolean(); // NoDropPlayer
            reader.ReadBoolean(); // NoDropMonster
            reader.ReadBoolean(); // NoNames
            reader.ReadBoolean(); // Fight
            reader.ReadBoolean(); // Fire
            reader.ReadInt32();  // FireDamage
            reader.ReadBoolean(); // Lightning
            reader.ReadInt32();  // LightningDamage
            reader.ReadByte();   // MapDarkLight

            SkipMineZones(reader);
            reader.ReadByte();   // MineIndex
            reader.ReadBoolean(); // NoMount
            reader.ReadBoolean(); // NeedBridle
            reader.ReadBoolean(); // NoFight
            reader.ReadUInt16(); // Music

            // --- 版本门控字段 ---
            if (version < 78) goto done;
            reader.ReadBoolean(); // NoTownTeleport
            if (version < 79) goto done;
            reader.ReadBoolean(); // NoReincarnation
            if (version >= 110)
                reader.ReadUInt16(); // WeatherParticles
            if (version >= 111)
            {
                reader.ReadBoolean(); // GT
                reader.ReadByte();    // GTIndex
            }
            if (version >= 114)
            {
                reader.ReadBoolean(); // NoExperience
                reader.ReadBoolean(); // NoGroup
                reader.ReadBoolean(); // NoPets
                reader.ReadBoolean(); // NoIntelligentCreatures
                reader.ReadBoolean(); // NoHero
                reader.ReadInt32();  // RequiredGroupSize
                reader.ReadBoolean(); // RequiredGroup
                reader.ReadBoolean(); // FireWallLimit
                reader.ReadInt32();  // FireWallCount
            }

        done:
            return new MapEntry
            {
                Index = index,
                FileName = fileName,
                Title = title,
                MiniMap = miniMap,
                BigMap = bigMap
            };
        }

        // ===== 子对象跳过函数 =====

        /// SafeZoneInfo: Int32(X) + Int32(Y) + UInt16(Size) + Boolean(StartPoint)
        static void SkipSafeZones(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                reader.ReadInt32();  // Location.X
                reader.ReadInt32();  // Location.Y
                reader.ReadUInt16(); // Size
                reader.ReadBoolean();// StartPoint
            }
        }

        /// RespawnInfo: Int32(MonsterIndex) + Int32(X) + Int32(Y) + 3xUInt16(Count,Spread,Delay)
        ///             + Byte(Direction) + String(RoutePath)
        ///             + [if Version>67] UInt16(RandomDelay) + Int32(RespawnIndex) + Boolean(SaveRespawnTime) + UInt16(RespawnTicks)
        static void SkipRespawns(BinaryReader reader, int version, int customVersion)
        {
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                reader.ReadInt32();  // MonsterIndex
                reader.ReadInt32();  // Location.X
                reader.ReadInt32();  // Location.Y
                reader.ReadUInt16(); // Count
                reader.ReadUInt16(); // Spread
                reader.ReadUInt16(); // Delay
                reader.ReadByte();   // Direction
                reader.ReadString(); // RoutePath
                if (version > 67)
                {
                    reader.ReadUInt16(); // RandomDelay
                    reader.ReadInt32();  // RespawnIndex
                    reader.ReadBoolean();// SaveRespawnTime
                    reader.ReadUInt16(); // RespawnTicks
                }
            }
        }

        /// MovementInfo: Int32(MapIndex) + 4xInt32(Source.X,Y,Dest.X,Y) + 2xBoolean(NeedHole,NeedMove)
        ///               + [if version>=69] Int32(ConquestIndex)
        ///               + [if version>=95] Boolean(ShowOnBigMap) + Int32(Icon)
        static void SkipMovements(BinaryReader reader, int version)
        {
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                reader.ReadInt32();  // MapIndex
                reader.ReadInt32();  // Source.X
                reader.ReadInt32();  // Source.Y
                reader.ReadInt32();  // Destination.X
                reader.ReadInt32();  // Destination.Y
                reader.ReadBoolean(); // NeedHole
                reader.ReadBoolean(); // NeedMove
                if (version >= 69)
                    reader.ReadInt32(); // ConquestIndex
                if (version >= 95)
                {
                    reader.ReadBoolean(); // ShowOnBigMap
                    reader.ReadInt32();  // Icon
                }
            }
        }

        /// MineZone: Int32(X) + Int32(Y) + UInt16(Size) + Byte(Mine)
        static void SkipMineZones(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                reader.ReadInt32();  // Location.X
                reader.ReadInt32();  // Location.Y
                reader.ReadUInt16(); // Size
                reader.ReadByte();   // Mine
            }
        }

        static void SkipInt32(BinaryReader reader) => reader.ReadInt32();
    }
}
