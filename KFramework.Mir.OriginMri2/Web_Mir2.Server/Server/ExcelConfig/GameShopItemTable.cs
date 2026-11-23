// 元宝商城物品配置表 DTO，统一放 Server/ExcelConfig 目录管理。
// 从 Library/Data/ItemData.cs 迁出（类体未改动，仅换文件位置）。

public class GameShopItemTable
{
    public int ItemIndex { get; set; }
    public int GIndex { get; set; }
    public ItemInfoTable Info { get; set; }
    public uint GoldPrice { get; set; } = 0;
    public uint CreditPrice { get; set; } = 0;
    public ushort Count { get; set; } = 1;
    public string Class { get; set; } = "";
    public string Category { get; set; } = "";
    public int Stock { get; set; } = 0;
    public bool iStock { get; set; } = false;
    public bool Deal { get; set; } = false;
    public bool TopItem { get; set; } = false;
    public DateTime Date { get; set; }
    public bool CanBuyGold { get; set; } = false;
    public bool CanBuyCredit { get; set; } = false;

    public GameShopItemTable()
    {
    }

    public GameShopItemTable(BinaryReader reader, int version = int.MaxValue, int Customversion = int.MaxValue)
    {
        ItemIndex = reader.ReadInt32();
        GIndex = reader.ReadInt32();
        GoldPrice = reader.ReadUInt32();
        CreditPrice = reader.ReadUInt32();
        if (version <= 84)
        {
            Count = (ushort)reader.ReadUInt32();
        }
        else
        {
            Count = reader.ReadUInt16();
        }
        Class = reader.ReadString();
        Category = reader.ReadString();
        Stock = reader.ReadInt32();
        iStock = reader.ReadBoolean();
        Deal = reader.ReadBoolean();
        TopItem = reader.ReadBoolean();
        Date = DateTime.FromBinary(reader.ReadInt64());
        if (version > 105)
        {
            CanBuyCredit = reader.ReadBoolean();
            CanBuyGold = reader.ReadBoolean();
        }

    }

    public GameShopItemTable(BinaryReader reader, bool packet = false)
    {
        ItemIndex = reader.ReadInt32();
        GIndex = reader.ReadInt32();
        Info = new ItemInfoTable(reader);
        GoldPrice = reader.ReadUInt32();
        CreditPrice = reader.ReadUInt32();
        Count = reader.ReadUInt16();
        Class = reader.ReadString();
        Category = reader.ReadString();
        Stock = reader.ReadInt32();
        iStock = reader.ReadBoolean();
        Deal = reader.ReadBoolean();
        TopItem = reader.ReadBoolean();
        Date = DateTime.FromBinary(reader.ReadInt64());
        CanBuyCredit = reader.ReadBoolean();
        CanBuyGold = reader.ReadBoolean();
    }

    public void Save(BinaryWriter writer, bool packet = false)
    {
        writer.Write(ItemIndex);
        writer.Write(GIndex);
        if (packet) Info.Save(writer);
        writer.Write(GoldPrice);
        writer.Write(CreditPrice);
        writer.Write(Count);
        writer.Write(Class);
        writer.Write(Category);
        writer.Write(Stock);
        writer.Write(iStock);
        writer.Write(Deal);
        writer.Write(TopItem);
        writer.Write(Date.ToBinary());
        writer.Write(CanBuyCredit);
        writer.Write(CanBuyGold);
    }

    public override string ToString()
    {
        return string.Format("{0}: {1}", GIndex, Info.Name);
    }

}
