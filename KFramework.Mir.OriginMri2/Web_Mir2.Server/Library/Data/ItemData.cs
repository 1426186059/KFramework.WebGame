public class UserItem
{
    public ulong UniqueID;
    public int ItemIndex;

    public ItemInfoTable Info;
    public ushort CurrentDura, MaxDura;
    public ushort Count = 1,
        GemCount = 0;

    public RefinedValue RefinedValue = RefinedValue.None;
    public byte RefineAdded = 0;
    public int RefineSuccessChance = 0;

    public bool DuraChanged;
    public int SoulBoundId = -1;
    public bool Identified = false;
    public bool Cursed = false;

    public int WeddingRing = -1;

    public UserItem[] Slots = new UserItem[0];

    public DateTime BuybackExpiryDate;

    public ExpireInfo ExpireInfo;
    public RentalInformation RentalInformation;
    public SealedInfo SealedInfo;

    public bool IsShopItem;

    // 原始传奇商店价格（内存字段，不进存档）：0=金币/1=元宝/2=传奇币；Price=最终价格
    public byte ShopCurrencyType;
    public uint ShopPrice;

    public Awake Awake = new Awake();

    public Stats AddedStats;

    public bool IsAdded
    {
        get { return AddedStats.Count > 0 || Slots.Length > Info.Slots; }
    }


    public string FriendlyName
    {
        get { return Count > 1 ? string.Format("{0} ({1})", Info.FriendlyName, Count) : Info.FriendlyName; }
    }

    public bool GMMade { get; set; }

    public UserItem(ItemInfoTable info)
    {
        SoulBoundId = -1;
        ItemIndex = info.Index;
        Info = info;
        AddedStats = new Stats();
        
        SetSlotSize();
    }
    public UserItem(BinaryReader reader, int version = int.MaxValue, int customVersion = int.MaxValue)
    {
        UniqueID = reader.ReadUInt64();
        ItemIndex = reader.ReadInt32();

        CurrentDura = reader.ReadUInt16();
        MaxDura = reader.ReadUInt16();

        if (version <= 84)
        {
            Count = (ushort)reader.ReadUInt32();
        }
        else
        {
            Count = reader.ReadUInt16();
        }

        if (version <= 84)
        {
            AddedStats = new Stats();

            AddedStats[Stat.MaxAC] = reader.ReadByte();
            AddedStats[Stat.MaxMAC] = reader.ReadByte();
            AddedStats[Stat.MaxDC] = reader.ReadByte();
            AddedStats[Stat.MaxMC] = reader.ReadByte();
            AddedStats[Stat.MaxSC] = reader.ReadByte();

            AddedStats[Stat.Accuracy] = reader.ReadByte();
            AddedStats[Stat.Agility] = reader.ReadByte();
            AddedStats[Stat.HP] = reader.ReadByte();
            AddedStats[Stat.MP] = reader.ReadByte();

            AddedStats[Stat.AttackSpeed] = reader.ReadSByte();
            AddedStats[Stat.Luck] = reader.ReadSByte();
        }

        SoulBoundId = reader.ReadInt32();
        byte Bools = reader.ReadByte();
        Identified = (Bools & 0x01) == 0x01;
        Cursed = (Bools & 0x02) == 0x02;

        if (version <= 84)
        {
            AddedStats[Stat.Strong] = reader.ReadByte();
            AddedStats[Stat.MagicResist] = reader.ReadByte();
            AddedStats[Stat.PoisonResist] = reader.ReadByte();
            AddedStats[Stat.HealthRecovery] = reader.ReadByte();
            AddedStats[Stat.SpellRecovery] = reader.ReadByte();
            AddedStats[Stat.PoisonRecovery] = reader.ReadByte();
            AddedStats[Stat.CriticalRate] = reader.ReadByte();
            AddedStats[Stat.CriticalDamage] = reader.ReadByte();
            AddedStats[Stat.Freezing] = reader.ReadByte();
            AddedStats[Stat.PoisonAttack] = reader.ReadByte();
        }

        int count = reader.ReadInt32();

        SetSlotSize(count);

        for (int i = 0; i < count; i++)
        {
            if (reader.ReadBoolean()) continue;
            UserItem item = new UserItem(reader, version, customVersion);
            Slots[i] = item;
        }

        if (version <= 84)
        {
            GemCount = (ushort)reader.ReadUInt32();
        }
        else
        {
            GemCount = reader.ReadUInt16();
        }

        if (version > 84)
        {
            AddedStats = new Stats(reader, version, customVersion);
        }

        Awake = new Awake(reader);

        RefinedValue = (RefinedValue)reader.ReadByte();
        RefineAdded = reader.ReadByte();

        if (version > 85)
        {
            RefineSuccessChance = reader.ReadInt32();
        }

        WeddingRing = reader.ReadInt32();

        if (version < 65) return;

        if (reader.ReadBoolean())
        {
            ExpireInfo = new ExpireInfo(reader, version, customVersion);
        }

        if (version < 76)
            return;

        if (reader.ReadBoolean())
            RentalInformation = new RentalInformation(reader, version, customVersion);

        if (version < 83) return;

        IsShopItem = reader.ReadBoolean();

        if (version < 92) return;

        if (reader.ReadBoolean())
        {
            SealedInfo = new SealedInfo(reader, version, customVersion);
        }

        if (version > 107)
        {
            GMMade = reader.ReadBoolean();
        }
    }

    public void Save(BinaryWriter writer)
    {
        writer.Write(UniqueID);
        writer.Write(ItemIndex);

        writer.Write(CurrentDura);
        writer.Write(MaxDura);

        writer.Write(Count);

        writer.Write(SoulBoundId);
        byte Bools = 0;
        if (Identified) Bools |= 0x01;
        if (Cursed) Bools |= 0x02;
        writer.Write(Bools);

        writer.Write(Slots.Length);
        for (int i = 0; i < Slots.Length; i++)
        {
            writer.Write(Slots[i] == null);
            if (Slots[i] == null) continue;

            Slots[i].Save(writer);
        }

        writer.Write(GemCount);


        AddedStats.Save(writer);
        Awake.Save(writer);

        writer.Write((byte)RefinedValue);
        writer.Write(RefineAdded);
        writer.Write(RefineSuccessChance);

        writer.Write(WeddingRing);

        writer.Write(ExpireInfo != null);
        ExpireInfo?.Save(writer);

        writer.Write(RentalInformation != null);
        RentalInformation?.Save(writer);

        writer.Write(IsShopItem);

        writer.Write(SealedInfo != null);
        SealedInfo?.Save(writer);

        writer.Write(GMMade);
    }

    public int GetTotal(Stat type)
    {
        return AddedStats[type] + Info.Stats[type];
    }

    public uint Price()
    {
        if (Info == null) return 0;

        uint p = Info.Price;


        if (Info.Durability > 0)
        {
            float r = ((Info.Price / 2F) / Info.Durability);

            p = (uint)(MaxDura * r);

            if (MaxDura > 0)
                r = CurrentDura / (float)MaxDura;
            else
                r = 0;

            p = (uint)Math.Floor(p / 2F + ((p / 2F) * r) + Info.Price / 2F);
        }


        p = (uint)(p * (AddedStats.Count * 0.1F + 1F));


        return p * Count;
    }
    public uint RepairPrice()
    {
        if (Info == null || Info.Durability == 0)
            return 0;

        var p = Info.Price;

        if (Info.Durability > 0)
        {
            p = (uint)Math.Floor(MaxDura * ((Info.Price / 2F) / Info.Durability) + Info.Price / 2F);
            p = (uint)(p * (AddedStats.Count * 0.1F + 1F));

        }

        var cost = p * Count - Price();

        if (RentalInformation == null)
            return cost;

        return cost * 2;
    }

    public uint Quality()
    {
        uint q = (uint)(AddedStats.Count + Awake.GetAwakeLevel() + 1);

        return q;
    }

    public uint AwakeningPrice()
    {
        if (Info == null) return 0;

        uint p = 1500;

        p = (uint)((p * (1 + Awake.GetAwakeLevel() * 2)) * (uint)Info.Grade);

        return p;
    }

    public uint DisassemblePrice()
    {
        if (Info == null) return 0;

        uint p = 1500 * (uint)Info.Grade;

        p = (uint)(p * ((AddedStats.Count + Awake.GetAwakeLevel()) * 0.1F + 1F));

        return p;
    }

    public uint DowngradePrice()
    {
        if (Info == null) return 0;

        uint p = 3000;

        p = (uint)((p * (1 + (Awake.GetAwakeLevel() + 1) * 2)) * (uint)Info.Grade);

        return p;
    }

    public uint ResetPrice()
    {
        if (Info == null) return 0;

        uint p = 3000 * (uint)Info.Grade;

        p = (uint)(p * (AddedStats.Count * 0.2F + 1F));

        return p;
    }
    public void SetSlotSize(int? size = null)
    {
        if (size == null)
        {
            switch (Info.Type)
            {
                case ItemType.Mount:
                    if (Info.Shape < 7)
                        size = 4;
                    else if (Info.Shape < 12)
                        size = 5;
                    break;
                case ItemType.Weapon:
                    if (Info.Shape == 49 || Info.Shape == 50)
                        size = 5;
                    break;
            }
        }

        if (size == null && Info == null) return;
        if (size != null && size == Slots.Length) return;
        if (size == null && Info != null && Info.Slots == Slots.Length) return;

        Array.Resize(ref Slots, size ?? Info.Slots);
    }

    public ushort Image
    {
        get
        {
            switch (Info.Type)
            {
                #region Amulet and Poison Stack Image changes
                case ItemType.Amulet:
                    if (Info.StackSize > 0)
                    {
                        switch (Info.Shape)
                        {
                            case 0: //Amulet
                                if (Count >= 300) return 3662;
                                if (Count >= 200) return 3661;
                                if (Count >= 100) return 3660;
                                return 3660;
                            case 1: //Grey Poison
                                if (Count >= 150) return 3675;
                                if (Count >= 100) return 2960;
                                if (Count >= 50) return 3674;
                                return 3673;
                            case 2: //Yellow Poison
                                if (Count >= 150) return 3672;
                                if (Count >= 100) return 2961;
                                if (Count >= 50) return 3671;
                                return 3670;
                        }
                    }
                    break;
            }

            #endregion

            // 原始传奇图标 ID 可能超过 ushort；数据层(ImageInfo.Image=uint)已完整保留，
            // 运行时/客户端图标仍走 ushort 截断（客户端尚未接入原始图标资源，截断不影响）。
            return (ushort)Info.Image;
        }
    }

    public UserItem Clone()
    {
        UserItem item = new UserItem(Info)
        {
            UniqueID = UniqueID,
            CurrentDura = CurrentDura,
            MaxDura = MaxDura,
            Count = Count,
            GemCount = GemCount,
            DuraChanged = DuraChanged,
            SoulBoundId = SoulBoundId,
            Identified = Identified,
            Cursed = Cursed,
            Slots = Slots,
            AddedStats = new Stats(AddedStats),
            Awake = Awake,

            RefineAdded = RefineAdded,

            ExpireInfo = ExpireInfo,
            RentalInformation = RentalInformation,
            SealedInfo = SealedInfo,

            IsShopItem = IsShopItem,
            GMMade = GMMade
        };

        return item;
    }

}

public class ExpireInfo
{
    public DateTime ExpiryDate;

    public ExpireInfo() { }

    public ExpireInfo(BinaryReader reader, int version = int.MaxValue, int Customversion = int.MaxValue)
    {
        ExpiryDate = DateTime.FromBinary(reader.ReadInt64());
    }

    public void Save(BinaryWriter writer)
    {
        writer.Write(ExpiryDate.ToBinary());
    }
}

public class SealedInfo
{
    public DateTime ExpiryDate;
    public DateTime NextSealDate;

    public SealedInfo() { }

    public SealedInfo(BinaryReader reader, int version = int.MaxValue, int Customversion = int.MaxValue)
    {
        ExpiryDate = DateTime.FromBinary(reader.ReadInt64());

        if (version > 92)
        {
            NextSealDate = DateTime.FromBinary(reader.ReadInt64());
        }
    }

    public void Save(BinaryWriter writer)
    {
        writer.Write(ExpiryDate.ToBinary());
        writer.Write(NextSealDate.ToBinary());
    }
}

public class RentalInformation
{
    public string OwnerName;
    public BindMode BindingFlags = BindMode.None;
    public DateTime ExpiryDate;
    public bool RentalLocked;

    public RentalInformation() { }

    public RentalInformation(BinaryReader reader, int version = int.MaxValue, int CustomVersion = int.MaxValue)
    {
        OwnerName = reader.ReadString();
        BindingFlags = (BindMode)reader.ReadInt16();
        ExpiryDate = DateTime.FromBinary(reader.ReadInt64());
        RentalLocked = reader.ReadBoolean();
    }

    public void Save(BinaryWriter writer)
    {
        writer.Write(OwnerName);
        writer.Write((short)BindingFlags);
        writer.Write(ExpiryDate.ToBinary());
        writer.Write(RentalLocked);
    }
}

public class Awake
{
    //Awake Option
    public static byte AwakeSuccessRate = 70;
    public static byte AwakeHitRate = 70;
    public static int MaxAwakeLevel = 5;
    public static byte Awake_WeaponRate = 1;
    public static byte Awake_HelmetRate = 1;
    public static byte Awake_ArmorRate = 5;
    public static byte AwakeChanceMin = 1;
    public static float[] AwakeMaterialRate = new float[5] { 1.0F, 1.0F, 1.0F, 1.0F, 1.0F };
    public static byte[] AwakeChanceMax = new byte[5] { 1, 2, 3, 4, 5 };
    public static List<List<byte>[]> AwakeMaterials = new List<List<byte>[]>();

    public AwakeType Type = AwakeType.None;
    readonly List<byte> listAwake = new List<byte>();

    public Awake() { }

    public Awake(BinaryReader reader)
    {
        Type = (AwakeType)reader.ReadByte();
        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            listAwake.Add(reader.ReadByte());
        }
    }

    public void Save(BinaryWriter writer)
    {
        writer.Write((byte)Type);
        writer.Write(listAwake.Count);
        foreach (byte value in listAwake)
        {
            writer.Write(value);
        }
    }
    public bool IsMaxLevel() { return listAwake.Count == Awake.MaxAwakeLevel; }

    public int GetAwakeLevel() { return listAwake.Count; }

    public byte GetAwakeValue()
    {
        byte total = 0;

        foreach (byte value in listAwake)
        {
            total += value;
        }

        return total;
    }

    public bool CheckAwakening(UserItem item, AwakeType type)
    {
        if (item.Info.Bind.HasFlag(BindMode.DontUpgrade))
            return false;

        if (item.Info.CanAwakening != true)
            return false;

        if (item.Info.Grade == ItemGrade.None)
            return false;

        if (IsMaxLevel()) return false;

        if (this.Type == AwakeType.None)
        {
            if (item.Info.Type == ItemType.Weapon)
            {
                if (type == AwakeType.DC ||
                    type == AwakeType.MC ||
                    type == AwakeType.SC)
                {
                    this.Type = type;
                    return true;
                }
                else
                    return false;
            }
            else if (item.Info.Type == ItemType.Helmet)
            {
                if (type == AwakeType.AC ||
                    type == AwakeType.MAC)
                {
                    this.Type = type;
                    return true;
                }
                else
                    return false;
            }
            else if (item.Info.Type == ItemType.Armour)
            {
                if (type == AwakeType.HPMP)
                {
                    this.Type = type;
                    return true;
                }
                else
                    return false;
            }
            else
                return false;
        }
        else
        {
            if (this.Type == type)
                return true;
            else
                return false;
        }
    }

    public int UpgradeAwake(UserItem item, AwakeType type, out bool[] isHit)
    {
        //return -1 condition error, -1 = dont upgrade, 0 = failed, 1 = Succeed,  
        isHit = null;
        if (CheckAwakening(item, type) != true)
            return -1;

        Random rand = new Random(DateTime.Now.Millisecond);

        if (rand.Next(0, 100) <= AwakeSuccessRate)
        {
            isHit = Awakening(item);
            return 1;
        }
        else
        {
            isHit = MakeHit(1, out _);
            return 0;
        }
    }

    public int RemoveAwake()
    {
        if (listAwake.Count > 0)
        {
            listAwake.Remove(listAwake[listAwake.Count - 1]);

            if (listAwake.Count == 0)
                Type = AwakeType.None;

            return 1;
        }
        else
        {
            Type = AwakeType.None;
            return 0;
        }
    }

    public int GetAwakeLevelValue(int i) { return listAwake[i]; }

    public byte GetDC() { return (Type == AwakeType.DC ? GetAwakeValue() : (byte)0); }
    public byte GetMC() { return (Type == AwakeType.MC ? GetAwakeValue() : (byte)0); }
    public byte GetSC() { return (Type == AwakeType.SC ? GetAwakeValue() : (byte)0); }
    public byte GetAC() { return (Type == AwakeType.AC ? GetAwakeValue() : (byte)0); }
    public byte GetMAC() { return (Type == AwakeType.MAC ? GetAwakeValue() : (byte)0); }
    public byte GetHPMP() { return (Type == AwakeType.HPMP ? GetAwakeValue() : (byte)0); }

    private bool[] MakeHit(int maxValue, out int makeValue)
    {
        float stepValue = (float)maxValue / 5.0f;
        float totalValue = 0.0f;
        bool[] isHit = new bool[5];
        Random rand = new Random(DateTime.Now.Millisecond);

        for (int i = 0; i < 5; i++)
        {
            if (rand.Next(0, 100) < AwakeHitRate)
            {
                totalValue += stepValue;
                isHit[i] = true;
            }
            else
            {
                isHit[i] = false;
            }
        }

        makeValue = totalValue <= 1.0f ? 1 : (int)totalValue;
        return isHit;
    }

    private bool[] Awakening(UserItem item)
    {
        int minValue = AwakeChanceMin;
        int maxValue = (AwakeChanceMax[(int)item.Info.Grade - 1] < minValue) ? minValue : AwakeChanceMax[(int)item.Info.Grade - 1];

        bool[] returnValue = MakeHit(maxValue, out int result);

        switch (item.Info.Type)
        {
            case ItemType.Weapon:
                result *= (int)Awake_WeaponRate;
                break;
            case ItemType.Armour:
                result *= (int)Awake_ArmorRate;
                break;
            case ItemType.Helmet:
                result *= (int)Awake_HelmetRate;
                break;
            default:
                result = 0;
                break;
        }

        listAwake.Add((byte)result);

        return returnValue;
    }
}


public class ItemRentalInformation
{
    public ulong ItemId;
    public string ItemName;
    public string RentingPlayerName;
    public DateTime ItemReturnDate;

    public ItemRentalInformation() { }

    public ItemRentalInformation(BinaryReader reader, int version = int.MaxValue, int customVersion = int.MaxValue)
    {
        ItemId = reader.ReadUInt64();
        ItemName = reader.ReadString();
        RentingPlayerName = reader.ReadString();
        ItemReturnDate = DateTime.FromBinary(reader.ReadInt64());
    }

    public void Save(BinaryWriter writer)
    {
        writer.Write(ItemId);
        writer.Write(ItemName);
        writer.Write(RentingPlayerName);
        writer.Write(ItemReturnDate.ToBinary());
    }
}


public class ItemSets
{
    public ItemSet Set;
    public List<ItemType> Type;

    private byte Amount
    {
        get
        {
            switch (Set)
            {
                case ItemSet.Mundane:
                case ItemSet.NokChi:
                case ItemSet.TaoProtect:
                case ItemSet.Whisker1:
                case ItemSet.Whisker2:
                case ItemSet.Whisker3:
                case ItemSet.Whisker4:
                case ItemSet.Whisker5:
                    return 2;
                case ItemSet.RedOrchid:
                case ItemSet.RedFlower:
                case ItemSet.Smash:
                case ItemSet.HwanDevil:
                case ItemSet.Purity:
                case ItemSet.FiveString:
                case ItemSet.Bone:
                case ItemSet.Bug:
                case ItemSet.DarkGhost:
                    return 3;
                case ItemSet.Recall:
                    return 4;
                case ItemSet.Spirit:
                case ItemSet.WhiteGold:
                case ItemSet.WhiteGoldH:
                case ItemSet.RedJade:
                case ItemSet.RedJadeH:
                case ItemSet.Nephrite:
                case ItemSet.NephriteH:
                case ItemSet.Hyeolryong:
                case ItemSet.Monitor:
                case ItemSet.Oppressive:
                case ItemSet.Paeok:
                case ItemSet.Sulgwan:
                case ItemSet.BlueFrostH:
                case ItemSet.BlueFrost:
                    return 5;
                default:
                    return 0;
            }
        }
    }
    public byte Count;
    public bool SetComplete
    {
        get
        {
            return Count >= Amount;
    }
}
}


public class RandomItemStat
{
    public byte MaxDuraChance { get; set; }
    public byte MaxDuraStatChance { get; set; }
    public byte MaxDuraMaxStat { get; set; }
    public byte MaxAcChance { get; set; }
    public byte MaxAcStatChance { get; set; }
    public byte MaxAcMaxStat { get; set; }
    public byte MaxMacChance { get; set; }
    public byte MaxMacStatChance { get; set; }
    public byte MaxMacMaxStat { get; set; }
    public byte MaxDcChance { get; set; }
    public byte MaxDcStatChance { get; set; }
    public byte MaxDcMaxStat { get; set; }
    public byte MaxMcChance { get; set; }
    public byte MaxMcStatChance { get; set; }
    public byte MaxMcMaxStat { get; set; }
    public byte MaxScChance { get; set; }
    public byte MaxScStatChance { get; set; }
    public byte MaxScMaxStat { get; set; }
    public byte AccuracyChance { get; set; }
    public byte AccuracyStatChance { get; set; }
    public byte AccuracyMaxStat { get; set; }
    public byte AgilityChance { get; set; }
    public byte AgilityStatChance { get; set; }
    public byte AgilityMaxStat { get; set; }
    public byte HpChance { get; set; }
    public byte HpStatChance { get; set; }
    public byte HpMaxStat { get; set; }
    public byte MpChance { get; set; }
    public byte MpStatChance { get; set; }
    public byte MpMaxStat { get; set; }
    public byte StrongChance { get; set; }
    public byte StrongStatChance { get; set; }
    public byte StrongMaxStat { get; set; }
    public byte MagicResistChance { get; set; }
    public byte MagicResistStatChance { get; set; }
    public byte MagicResistMaxStat { get; set; }
    public byte PoisonResistChance { get; set; }
    public byte PoisonResistStatChance { get; set; }
    public byte PoisonResistMaxStat { get; set; }
    public byte HpRecovChance { get; set; }
    public byte HpRecovStatChance { get; set; }
    public byte HpRecovMaxStat { get; set; }
    public byte MpRecovChance { get; set; }
    public byte MpRecovStatChance { get; set; }
    public byte MpRecovMaxStat { get; set; }
    public byte PoisonRecovChance { get; set; }
    public byte PoisonRecovStatChance { get; set; }
    public byte PoisonRecovMaxStat { get; set; }
    public byte CriticalRateChance { get; set; }
    public byte CriticalRateStatChance { get; set; }
    public byte CriticalRateMaxStat { get; set; }
    public byte CriticalDamageChance { get; set; }
    public byte CriticalDamageStatChance { get; set; }
    public byte CriticalDamageMaxStat { get; set; }
    public byte FreezeChance { get; set; }
    public byte FreezeStatChance { get; set; }
    public byte FreezeMaxStat { get; set; }
    public byte PoisonAttackChance { get; set; }
    public byte PoisonAttackStatChance { get; set; }
    public byte PoisonAttackMaxStat { get; set; }
    public byte AttackSpeedChance { get; set; }
    public byte AttackSpeedStatChance { get; set; }
    public byte AttackSpeedMaxStat { get; set; }
    public byte LuckChance { get; set; }
    public byte LuckStatChance { get; set; }
    public byte LuckMaxStat { get; set; }
    public byte CurseChance { get; set; }
    public byte SlotChance { get; set; }
    public byte SlotStatChance { get; set; }
    public byte SlotMaxStat { get; set; }

    public RandomItemStat(ItemType Type = ItemType.Book)
    {
        switch (Type)
        {
            case ItemType.Weapon:
                SetWeapon();
                break;
            case ItemType.Armour:
                SetArmour();
                break;
            case ItemType.Helmet:
                SetHelmet();
                break;
            case ItemType.Belt:
            case ItemType.Boots:
                SetBeltBoots();
                break;
            case ItemType.Necklace:
                SetNecklace();
                break;
            case ItemType.Bracelet:
                SetBracelet();
                break;
            case ItemType.Ring:
                SetRing();
                break;
            case ItemType.Mount:
                SetMount();
                break;
        }
    }

    public void SetWeapon()
    {
        MaxDuraChance = 2;
        MaxDuraStatChance = 13;
        MaxDuraMaxStat = 13;

        MaxDcChance = 15;
        MaxDcStatChance = 15;
        MaxDcMaxStat = 13;

        MaxMcChance = 20;
        MaxMcStatChance = 15;
        MaxMcMaxStat = 13;

        MaxScChance = 20;
        MaxScStatChance = 15;
        MaxScMaxStat = 13;

        AttackSpeedChance = 60;
        AttackSpeedStatChance = 30;
        AttackSpeedMaxStat = 3;

        StrongChance = 24;
        StrongStatChance = 20;
        StrongMaxStat = 2;

        AccuracyChance = 30;
        AccuracyStatChance = 20;
        AccuracyMaxStat = 2;
    }

    public void SetArmour()
    {
        MaxDuraChance = 2;
        MaxDuraStatChance = 10;
        MaxDuraMaxStat = 3;

        MaxAcChance = 30;
        MaxAcStatChance = 15;
        MaxAcMaxStat = 7;

        MaxMacChance = 30;
        MaxMacStatChance = 15;
        MaxMacMaxStat = 7;

        MaxDcChance = 40;
        MaxDcStatChance = 20;
        MaxDcMaxStat = 7;

        MaxMcChance = 40;
        MaxMcStatChance = 20;
        MaxMcMaxStat = 7;

        MaxScChance = 40;
        MaxScStatChance = 20;
        MaxScMaxStat = 7;

    }
    public void SetHelmet()
    {
        MaxDuraChance = 2;
        MaxDuraStatChance = 10;
        MaxDuraMaxStat = 3;

        MaxAcChance = 30;
        MaxAcStatChance = 15;
        MaxAcMaxStat = 7;

        MaxMacChance = 30;
        MaxMacStatChance = 15;
        MaxMacMaxStat = 7;

        MaxDcChance = 40;
        MaxDcStatChance = 20;
        MaxDcMaxStat = 7;

        MaxMcChance = 40;
        MaxMcStatChance = 20;
        MaxMcMaxStat = 7;

        MaxScChance = 40;
        MaxScStatChance = 20;
        MaxScMaxStat = 7;
    }

    public void SetBeltBoots()
    {
        MaxDuraChance = 2;
        MaxDuraStatChance = 10;
        MaxDuraMaxStat = 3;

        MaxAcChance = 30;
        MaxAcStatChance = 30;
        MaxAcMaxStat = 3;

        MaxMacChance = 30;
        MaxMacStatChance = 30;
        MaxMacMaxStat = 3;

        MaxDcChance = 30;
        MaxDcStatChance = 30;
        MaxDcMaxStat = 3;

        MaxMcChance = 30;
        MaxMcStatChance = 30;
        MaxMcMaxStat = 3;

        MaxScChance = 30;
        MaxScStatChance = 30;
        MaxScMaxStat = 3;

        AgilityChance = 60;
        AgilityStatChance = 30;
        AgilityMaxStat = 3;
    }

    public void SetNecklace()
    {
        MaxDuraChance = 2;
        MaxDuraStatChance = 10;
        MaxDuraMaxStat = 3;

        MaxDcChance = 15;
        MaxDcStatChance = 30;
        MaxDcMaxStat = 7;

        MaxMcChance = 15;
        MaxMcStatChance = 30;
        MaxMcMaxStat = 7;

        MaxScChance = 15;
        MaxScStatChance = 30;
        MaxScMaxStat = 7;

        AccuracyChance = 60;
        AccuracyStatChance = 30;
        AccuracyMaxStat = 7;

        AgilityChance = 60;
        AgilityStatChance = 30;
        AgilityMaxStat = 7;
    }

    public void SetBracelet()
    {
        MaxDuraChance = 2;
        MaxDuraStatChance = 10;
        MaxDuraMaxStat = 3;

        MaxAcChance = 20;
        MaxAcStatChance = 30;
        MaxAcMaxStat = 6;

        MaxMacChance = 20;
        MaxMacStatChance = 30;
        MaxMacMaxStat = 6;

        MaxDcChance = 30;
        MaxDcStatChance = 30;
        MaxDcMaxStat = 6;

        MaxMcChance = 30;
        MaxMcStatChance = 30;
        MaxMcMaxStat = 6;

        MaxScChance = 30;
        MaxScStatChance = 30;
        MaxScMaxStat = 6;
    }

    public void SetRing()
    {
        MaxDuraChance = 2;
        MaxDuraStatChance = 10;
        MaxDuraMaxStat = 3;

        MaxAcChance = 25;
        MaxAcStatChance = 20;
        MaxAcMaxStat = 6;

        MaxMacChance = 25;
        MaxMacStatChance = 20;
        MaxMacMaxStat = 6;

        MaxDcChance = 15;
        MaxDcStatChance = 30;
        MaxDcMaxStat = 6;

        MaxMcChance = 15;
        MaxMcStatChance = 30;
        MaxMcMaxStat = 6;

        MaxScChance = 15;
        MaxScStatChance = 30;
        MaxScMaxStat = 6;
    }

    public void SetMount()
    {
        SetRing();
    }
}

public class ChatItem
{
    public ulong UniqueID;
    public string Title;
    public MirGridType Grid;

    public string RegexInternalName
    {
        get { return $"<{Title.Replace("(", "\\(").Replace(")", "\\)")}>"; }
    }

    public string InternalName
    {
        get { return $"<{Title}/{UniqueID}>"; }
    }

    public ChatItem() { }

    public ChatItem(BinaryReader reader)
    {
        UniqueID = reader.ReadUInt64();
        Title = reader.ReadString();
        Grid = (MirGridType)reader.ReadByte();
    }

    public void Save(BinaryWriter writer)
    {
        writer.Write(UniqueID);
        writer.Write(Title);
        writer.Write((byte)Grid);
    }
}
