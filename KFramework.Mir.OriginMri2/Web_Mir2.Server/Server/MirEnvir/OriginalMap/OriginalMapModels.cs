namespace Server.MirEnvir
{
    /// <summary>
    /// 原始传奇（提取工具）地图单元格标志。来自 .dat 的 4-bit/格 解码。
    /// bit3=不可走(墙) / bit2=遮挡 / bit1=安全区。可走 = bit3 为 0。
    /// </summary>
    [System.Flags]
    public enum OriginalMapCellFlags : byte
    {
        None = 0,
        Safe = 1 << 1,
        Cover = 1 << 2,
        Blocked = 1 << 3
    }

    /// <summary>单张地图在 .dat 索引中的条目。</summary>
    public class OriginalMapEntry
    {
        public int Id { get; set; }
        public int Len { get; set; }
        public double Kb { get; set; }
    }

    /// <summary>
    /// 单张地图的完整解码数据（行优先 CellFlags，每格 1 个 nibble，长度 = Cols*Rows）。
    /// 直接移植自提取工具 Models/MapData.cs 的 MirMapData。
    /// </summary>
    public class OriginalMapData
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Width { get; set; }
        public int Height { get; set; }
        public int Cols { get; set; }
        public int Rows { get; set; }
        public int RawCells { get; set; }
        public int Walkable { get; set; }
        public int Cover { get; set; }
        public int Safe { get; set; }
        public string Warn { get; set; }
        public byte[] CellFlags { get; set; } = System.Array.Empty<byte>();
    }
}
