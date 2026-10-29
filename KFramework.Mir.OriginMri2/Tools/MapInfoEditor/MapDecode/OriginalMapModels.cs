namespace MapInfoEditor
{
    /// <summary>单张地图在 .dat 索引中的条目。</summary>
    public class OriginalMapEntry
    {
        public int Id { get; set; }
        public int Len { get; set; }
        public double Kb { get; set; }
    }

    /// <summary>
    /// 单张地图的完整解码数据（行优先 CellFlags，每格 1 个 nibble，长度 = Cols*Rows）。
    /// 直接移植自服务器 Server.MirEnvir.OriginalMapModels。
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
