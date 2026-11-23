using System.Drawing;
using Server.MirEnvir;

namespace Server.ExcelConfig
{
    // 原始传奇 NPC 内嵌配置：非 null 表示对话/功能数据直接来自配置表，不走 Envir/NPCs/*.txt。
    public class NPCOriginSettings
    {
        public int OriginNpcId { get; set; }
        public string Title { get; set; } = "";
        public string Depiction { get; set; } = "";
        public string Button { get; set; } = "";
        public string Function { get; set; } = "";
        public string Deliver { get; set; } = "";
        public int StoreId { get; set; }
        public string OpenCondition { get; set; } = "";
        public string ShowCondition { get; set; } = "";
        public string Tips { get; set; } = "";
        public List<NPCPlacement> Placements { get; set; } = new();

        /// <summary>原始页游显示缩放百分比（3config.npc.scale），100 = 原尺寸。</summary>
        public int ScalePercent { get; set; } = 100;
    }

    public class NPCPlacement
    {
        public int MapId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public bool MapResolved { get; set; }
    }

    public class NPCInfoTable
    {
        protected static Envir EditEnvir
        {
            get { return Envir.Edit; }
        }

        public int Index { get; set; }

        public string FileName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string NameUI { get; set; } = string.Empty;

        public int MapIndex { get; set; }
        public Point Location { get; set; }
        public ushort Rate { get; set; } = 100;
        public ushort Image { get; set; }
        public Color Colour { get; set; }

        public bool TimeVisible { get; set; } = false;
        public byte HourStart { get; set; } = 0;
        public byte MinuteStart { get; set; } = 0;
        public byte HourEnd { get; set; } = 0;
        public byte MinuteEnd { get; set; } = 1;
        public short MinLev { get; set; } = 0;
        public short MaxLev { get; set; } = 0;
        public string DayofWeek { get; set; } = "";
        public string ClassRequired { get; set; } = "";
        public bool Sabuk { get; set; } = false;
        public int FlagNeeded { get; set; } = 0;
        public int Conquest { get; set; }
        public bool ShowOnBigMap { get; set; }
        public int BigMapIcon { get; set; }
        public bool CanTeleportTo { get; set; }
        public bool ConquestVisible { get; set; } = true;

        // 本 NPC 在脚本 [Quests] 段中声明的任务索引数组（由 NPCScript.ParseQuests 加载时填充）：
        //   CollectQuestIndexes —— 可由本 NPC 接取（发放/起始）的任务；加载时会回填到对应 QuestInfoTable.NpcIndex。
        //   FinishQuestIndexes  —— 需在本 NPC 处交付（完成）的任务；加载时会回填到对应 QuestInfoTable.FinishNpcIndex。
        // 即：NPCInfoTable 是“谁发放/谁交付某任务”的权威来源，QuestInfoTable.NpcIndex 由这里自动推导，无需在 QuestInfoTable 中手动维护。
        public List<int> CollectQuestIndexes { get; set; } = new List<int>();
        public List<int> FinishQuestIndexes { get; set; } = new List<int>();

        // 旧表没有该字段（反序列化为 null），继续走 FileName 指向的脚本。
        public NPCOriginSettings OriginSettings { get; set; }

        public NPCInfoTable() { }

        /// <summary>
        /// 按 mapnpc 表的一个摆放点生成浅拷贝：共享静态配置（名字 / model / OriginSettings / 任务），
        /// 只覆盖 Location，使同一个 NPC 定义能在多张地图、多个坐标各生成一个实例。
        /// 仅运行时使用，不写回配置表。
        /// </summary>
        public NPCInfoTable CloneAt(int x, int y)
        {
            var copy = (NPCInfoTable)MemberwiseClone();
            copy.Location = new Point(x, y);
            return copy;
        }

        public NPCInfoTable(BinaryReader reader)
        {
            Index = reader.ReadInt32();
            MapIndex = reader.ReadInt32();

            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
                CollectQuestIndexes.Add(reader.ReadInt32());

            count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
                FinishQuestIndexes.Add(reader.ReadInt32());

            FileName = reader.ReadString();
            Name = reader.ReadString();

            Location = new Point(reader.ReadInt32(), reader.ReadInt32());

            if (Envir.LoadVersion >= 72)
            {
                Image = reader.ReadUInt16();
            }
            else
            {
                Image = reader.ReadByte();
            }

            Rate = reader.ReadUInt16();

            if (Envir.LoadVersion >= 64)
            {
                TimeVisible = reader.ReadBoolean();
                HourStart = reader.ReadByte();
                MinuteStart = reader.ReadByte();
                HourEnd = reader.ReadByte();
                MinuteEnd = reader.ReadByte();
                MinLev = reader.ReadInt16();
                MaxLev = reader.ReadInt16();
                DayofWeek = reader.ReadString();
                ClassRequired = reader.ReadString();
                if (Envir.LoadVersion >= 66)
                    Conquest = reader.ReadInt32();
                else
                    Sabuk = reader.ReadBoolean();
                FlagNeeded = reader.ReadInt32();
            }

            if (Envir.LoadVersion > 95)
            {
                ShowOnBigMap = reader.ReadBoolean();
                BigMapIcon = reader.ReadInt32();
            }
            if (Envir.LoadVersion > 96)
                CanTeleportTo = reader.ReadBoolean();

            if (Envir.LoadVersion >= 107)
            {
                ConquestVisible = reader.ReadBoolean();
            }
        }
        public void Save(BinaryWriter writer)
        {
            writer.Write(Index);
            writer.Write(MapIndex);

            writer.Write(CollectQuestIndexes.Count());
            for (int i = 0; i < CollectQuestIndexes.Count; i++)
                writer.Write(CollectQuestIndexes[i]);

            writer.Write(FinishQuestIndexes.Count());
            for (int i = 0; i < FinishQuestIndexes.Count; i++)
                writer.Write(FinishQuestIndexes[i]);

            writer.Write(FileName);
            writer.Write(Name);

            writer.Write(Location.X);
            writer.Write(Location.Y);
            writer.Write(Image);
            writer.Write(Rate);

            writer.Write(TimeVisible);
            writer.Write(HourStart);
            writer.Write(MinuteStart);
            writer.Write(HourEnd);
            writer.Write(MinuteEnd);
            writer.Write(MinLev);
            writer.Write(MaxLev);
            writer.Write(DayofWeek);
            writer.Write(ClassRequired);
            writer.Write(Conquest);
            writer.Write(FlagNeeded);

            writer.Write(ShowOnBigMap);
            writer.Write(BigMapIcon);
            writer.Write(CanTeleportTo);
            writer.Write(ConquestVisible);
        }

        public static void FromText(string text)
        {
            string[] data = text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            if (data.Length < 6) return;

            NPCInfoTable info;
            bool isNew = false;
            if (!int.TryParse(data[0], out var index))
            {
                index = -1;
            }
            if (index == -1 || (info = EditEnvir.NPCInfoList.FirstOrDefault(d => d.Index == index)) == null)
            {
                info = new NPCInfoTable() { Index = ++EditEnvir.NPCIndex };
                isNew = true;
            }
            info.FileName = data[1];

            info.MapIndex = EditEnvir.MapInfoList.Where(d => d.FileName == data[2]).FirstOrDefault().Index;

            if (!int.TryParse(data[3], out int x)) return;
            if (!int.TryParse(data[4], out int y)) return;

            info.Location = new Point(x, y);

            info.Name = data[5];

            if (!ushort.TryParse(data[6], out ushort image)) return;
            if (!ushort.TryParse(data[7], out ushort rate)) return;

            if (!bool.TryParse(data[8], out bool showOnBigMap)) return;
            if (!int.TryParse(data[9], out int bigMapIcon)) return;
            if (!bool.TryParse(data[10], out bool canTeleportTo)) return;
            if (!bool.TryParse(data[11], out bool conquestVisible)) return;
            if (!short.TryParse(data[12], out short minLev)) return;
            if (!short.TryParse(data[13], out short maxLev)) return;
            if (!bool.TryParse(data[14], out bool timeVisible)) return;
            if (!byte.TryParse(data[15], out byte hourStart)) return;
            if (!byte.TryParse(data[16], out byte minuteStart)) return;
            if (!byte.TryParse(data[17], out byte hourEnd)) return;
            if (!byte.TryParse(data[18], out byte minuteEnd)) return;

            info.Image = image;
            info.Rate = rate;
            info.ShowOnBigMap = showOnBigMap;
            info.BigMapIcon = bigMapIcon;
            info.CanTeleportTo = canTeleportTo;
            info.ConquestVisible = conquestVisible;
            info.MinLev = minLev;
            info.MaxLev = maxLev;
            info.TimeVisible = timeVisible;
            info.HourStart = hourStart;
            info.MinuteStart = minuteStart;
            info.HourEnd = hourEnd;
            info.MinuteEnd = minuteEnd;

            if (isNew) EditEnvir.NPCInfoList.Add(info);
        }
        public string ToText()
        {
            return string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18}", Index,
                FileName, EditEnvir.MapInfoList.Where(d => d.Index == MapIndex).FirstOrDefault().FileName, Location.X, Location.Y, Name, Image, Rate, ShowOnBigMap, BigMapIcon, CanTeleportTo, ConquestVisible,
                MinLev, MaxLev, TimeVisible, HourStart, MinuteStart, HourEnd, MinuteEnd);
        }

        public override string ToString()
        {
            return $" [{Index}] {FileName}: {Name}, {Functions.PointToString(Location)}";
        }

        public string GameName
        {
            get
            {
                string s = Name;
                if (s.Contains("_"))
                {
                    string[] splitName = s.Split('_');
                    s = splitName[splitName.Length - 1];
                }
                return s;
            }
        }

        public ClientNPCInfo ClientInformation
        {
            get
            {
                return new ClientNPCInfo
                {
                    ObjectID = 0,
                    Index = Index,
                    FileName = FileName,
                    Name = Name,
                    MapIndex = MapIndex,
                    Location = Location,
                    Image = Image,
                    Rate = Rate,
                    ShowOnBigMap = ShowOnBigMap,
                    BigMapIcon = BigMapIcon,
                    Icon = BigMapIcon,
                    CanTeleportTo = CanTeleportTo
                };
            }
        }
    }
}
