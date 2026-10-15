namespace Mir2ReverseWeb.Models;

/// <summary>
/// 地图单元格标志（来自 .dat 的 4-bit/格 解码）。
/// 与客户端 bit 位定义一致：bit3=可走 / bit2=遮挡 / bit1=安全区。
/// </summary>
[Flags]
public enum MapCellFlags : byte
{
    None = 0,
    Safe = 1 << 1,     // 安全区
    Cover = 1 << 2,    // 遮挡
    Walkable = 1 << 3  // 可走
}

/// <summary>
/// 单张地图的完整解码数据。纯数据模型，不依赖 ASP/HTTP/Skia，
/// 便于将来直接合并进 MonoGame.Client 作为运行时地图结构。
/// </summary>
public class MirMapData
{
    /// <summary>地图 ID（来自 .dat 索引）</summary>
    public int Id { get; set; }

    /// <summary>地图名（来自 config 合并，可能为空）</summary>
    public string Name { get; set; } = "";

    /// <summary>配置像素宽（0 表示未知）</summary>
    public int Width { get; set; }

    /// <summary>配置像素高（0 表示未知）</summary>
    public int Height { get; set; }

    /// <summary>解码后的网格列数</summary>
    public int Cols { get; set; }

    /// <summary>解码后的网格行数</summary>
    public int Rows { get; set; }

    /// <summary>由数据块长度推算的单元格总数（每个 4 bit）</summary>
    public int RawCells { get; set; }

    /// <summary>可走单元格数</summary>
    public int Walkable { get; set; }

    /// <summary>遮挡单元格数</summary>
    public int Cover { get; set; }

    /// <summary>安全区单元格数</summary>
    public int Safe { get; set; }

    /// <summary>非致命提示（如配置与数据不符时自动重算网格）</summary>
    public string? Warn { get; set; }

    /// <summary>行优先存储的每格原始 nibble（0-15），长度 = Cols*Rows</summary>
    public byte[] CellFlags { get; set; } = Array.Empty<byte>();
}
