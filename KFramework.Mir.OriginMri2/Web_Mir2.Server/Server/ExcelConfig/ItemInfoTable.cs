// 原始传奇/运行时物品配置表 DTO，统一放 Server/ExcelConfig 目录管理。
// 从 Library/Data/ItemData.cs 迁出（类体未改动，仅换文件位置）。

using System.Text.RegularExpressions;

namespace Server.ExcelConfig
{

public class ItemInfoTable
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    // 原始传奇品质（1config.item[col9]），客户端物品格品质框用；服务器不参与逻辑，仅保持配置表同构。
    public int Quality { get; set; }
    public ItemType Type { get; set; }
    public ItemGrade Grade { get; set; }
    public RequiredType RequiredType { get; set; } = RequiredType.Level;
    public RequiredClass RequiredClass { get; set; } = RequiredClass.None;
    public RequiredGender RequiredGender { get; set; } = RequiredGender.None;
    public ItemSet Set { get; set; }

    public short Shape { get; set; }
    public byte Light { get; set; }
    public byte RequiredAmount { get; set; }

    public uint Image { get; set; }
    public ushort Durability { get; set; }

    public uint Price { get; set; }
    // 原始传奇 col16 堆叠上限可达 9999999，配置与网络协议均为 uint（4 字节）。
    public uint StackSize { get; set; } = 1;

    public bool StartItem { get; set; }
    // 新角色赠送数量；默认 1，回城卷等可通过配置表改成 100。
    public int StartItemCount { get; set; } = 1;
    public byte Effect { get; set; }

    public bool NeedIdentify { get; set; }
    public bool ShowGroupPickup { get; set; }
    public bool GlobalDropNotify { get; set; }
    public bool ClassBased { get; set; }
    public bool LevelBased { get; set; }
    public bool CanMine { get; set; }
    public bool CanFastRun { get; set; }
    public bool CanAwakening { get; set; }

    public BindMode Bind { get; set; } = BindMode.None;

    public SpecialItemMode Unique { get; set; } = SpecialItemMode.None;
    public byte RandomStatsId { get; set; }
    public RandomItemStat RandomStats { get; set; }
    public string ToolTip { get; set; } = string.Empty;

    public byte Slots { get; set; }

    public Stats Stats { get; set; }

    public bool IsConsumable
    {
        get { return Type == ItemType.Potion || Type == ItemType.Scroll || Type == ItemType.Food || Type == ItemType.Transform || Type == ItemType.Script || Type == ItemType.SealedHero; }
    }
    public bool IsFishingRod
    {
        get { return Globals.FishingRodShapes.Contains(Shape); }
    }

    public string FriendlyName
    {
        get
        {
            string temp = Name;
            temp = Regex.Replace(temp, @"\d+$", string.Empty); //hides end numbers
            temp = Regex.Replace(temp, @"\[[^\]]*\]", string.Empty); //hides square brackets

            return temp;
        }
    }

    public ItemInfoTable()
    {
        Stats = new Stats();
    }

    public ItemInfoTable(BinaryReader reader, int version = int.MaxValue, int customVersion = int.MaxValue)
    {
        Index = reader.ReadInt32();
        Name = reader.ReadString();
        Type = (ItemType)reader.ReadByte();
        Grade = (ItemGrade)reader.ReadByte();
        RequiredType = (RequiredType)reader.ReadByte();
        RequiredClass = (RequiredClass)reader.ReadByte();
        RequiredGender = (RequiredGender)reader.ReadByte();
        Set = (ItemSet)reader.ReadByte();

        Shape = reader.ReadInt16();
        Light = reader.ReadByte();
        RequiredAmount = reader.ReadByte();

        Image = reader.ReadUInt32();
        Durability = reader.ReadUInt16();

        StackSize = reader.ReadUInt32();

        Price = reader.ReadUInt32();

        if (version <= 84)
        {
            Stats = new Stats();
            Stats[Stat.MinAC] = reader.ReadByte();
            Stats[Stat.MaxAC] = reader.ReadByte();
            Stats[Stat.MinMAC] = reader.ReadByte();
            Stats[Stat.MaxMAC] = reader.ReadByte();
            Stats[Stat.MinDC] = reader.ReadByte();
            Stats[Stat.MaxDC] = reader.ReadByte();
            Stats[Stat.MinMC] = reader.ReadByte();
            Stats[Stat.MaxMC] = reader.ReadByte();
            Stats[Stat.MinSC] = reader.ReadByte();
            Stats[Stat.MaxSC] = reader.ReadByte();
            Stats[Stat.HP] = reader.ReadUInt16();
            Stats[Stat.MP] = reader.ReadUInt16();
            Stats[Stat.Accuracy] = reader.ReadByte();
            Stats[Stat.Agility] = reader.ReadByte();

            Stats[Stat.Luck] = reader.ReadSByte();
            Stats[Stat.AttackSpeed] = reader.ReadSByte();
        }

        StartItem = reader.ReadBoolean();

        if (version <= 84)
        {
        }

        Effect = reader.ReadByte();

        if (version <= 84)
        {
            Stats[Stat.Strong] = reader.ReadByte();
            Stats[Stat.MagicResist] = reader.ReadByte();
            Stats[Stat.PoisonResist] = reader.ReadByte();
            Stats[Stat.HealthRecovery] = reader.ReadByte();
            Stats[Stat.SpellRecovery] = reader.ReadByte();
            Stats[Stat.PoisonRecovery] = reader.ReadByte();
            Stats[Stat.HPRatePercent] = reader.ReadByte();
            Stats[Stat.MPRatePercent] = reader.ReadByte();
            Stats[Stat.CriticalRate] = reader.ReadByte();
            Stats[Stat.CriticalDamage] = reader.ReadByte();
        }


        byte bools = reader.ReadByte();
        NeedIdentify = (bools & 0x01) == 0x01;
        ShowGroupPickup = (bools & 0x02) == 0x02;
        ClassBased = (bools & 0x04) == 0x04;
        LevelBased = (bools & 0x08) == 0x08;
        CanMine = (bools & 0x10) == 0x10;

        if (version >= 77)
        {
            GlobalDropNotify = (bools & 0x20) == 0x20;
        }

        if (version <= 84)
        {
            Stats[Stat.MaxACRatePercent] = reader.ReadByte();
            Stats[Stat.MaxMACRatePercent] = reader.ReadByte();
            Stats[Stat.Holy] = reader.ReadByte();
            Stats[Stat.Freezing] = reader.ReadByte();
            Stats[Stat.PoisonAttack] = reader.ReadByte();
        }

        Bind = (BindMode)reader.ReadInt16();

        if (version <= 84)
        {
            Stats[Stat.Reflect] = reader.ReadByte();
            Stats[Stat.HPDrainRatePercent] = reader.ReadByte();
        }

        Unique = (SpecialItemMode)reader.ReadInt16();
        RandomStatsId = reader.ReadByte();

        CanFastRun = reader.ReadBoolean();

        CanAwakening = reader.ReadBoolean();

        if (version > 83)
        {
            Slots = reader.ReadByte();
        }

        if (version > 84)
        {
            Stats = new Stats(reader, version, customVersion);
        }

        bool isTooltip = reader.ReadBoolean();
        if (isTooltip)
        {
            ToolTip = reader.ReadString();
        }

        if (version < 70) //before db version 70 all specialitems had wedding rings disabled, after that it became a server option
        {
            if ((Type == ItemType.Ring) && (Unique != SpecialItemMode.None))
                Bind |= BindMode.NoWeddingRing;
        }
    }



    public void Save(BinaryWriter writer)
    {
        writer.Write(Index);
        writer.Write(Name);
        writer.Write((byte)Type);
        writer.Write((byte)Grade);
        writer.Write((byte)RequiredType);
        writer.Write((byte)RequiredClass);
        writer.Write((byte)RequiredGender);
        writer.Write((byte)Set);

        writer.Write(Shape);
        writer.Write(Light);
        writer.Write(RequiredAmount);

        // Image/StackSize 两端均为 uint，协议写 4 字节；Durability 为 ushort，不能漏写（漏写会让客户端 ReadUInt16 错位到 StackSize，最终 EOF）。
        writer.Write(Image);
        writer.Write(Durability);

        writer.Write(StackSize);
        writer.Write(Price);

        writer.Write(StartItem);

        writer.Write(Effect);

        byte bools = 0;
        if (NeedIdentify) bools |= 0x01;
        if (ShowGroupPickup) bools |= 0x02;
        if (ClassBased) bools |= 0x04;
        if (LevelBased) bools |= 0x08;
        if (CanMine) bools |= 0x10;
        if (GlobalDropNotify) bools |= 0x20;
        writer.Write(bools);

        writer.Write((short)Bind);
        writer.Write((short)Unique);

        writer.Write(RandomStatsId);

        writer.Write(CanFastRun);
        writer.Write(CanAwakening);
        writer.Write(Slots);

        Stats.Save(writer);

        writer.Write(ToolTip != null);
        if (ToolTip != null)
            writer.Write(ToolTip);

    }

    public static ItemInfoTable FromText(string text)
    {
        return null;
    }

    public string ToText()
    {
        return null;
    }

    public override string ToString()
    {
        return string.Format("{0}: {1}", Index, Name);
    }

}

}