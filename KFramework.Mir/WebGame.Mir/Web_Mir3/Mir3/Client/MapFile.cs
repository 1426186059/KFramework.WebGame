using System.IO;

namespace MirClient.Assets;

/// <summary>地图格子的图层数据。</summary>
public struct MapCell
{
    public byte BackFile;
    public ushort BackImage;

    public byte MiddleFile;
    public ushort MiddleImage;

    public byte FrontFile;
    public ushort FrontImage;

    public byte MiddleAnimationFrame;
    public byte FrontAnimationFrame;

    public byte Light;
    public bool Flag;

    public int MiddleAnimationCount => MiddleAnimationFrame & 0x0F;
    public bool MiddleAnimationBlend => (MiddleAnimationFrame & 0x80) != 0;
    public int FrontAnimationCount => FrontAnimationFrame & 0x0F;
    public bool FrontAnimationBlend => (FrontAnimationFrame & 0x80) != 0;
}

/// <summary>
/// Mir3 .map 文件解析。
/// 布局（与 Zircon Client/Scenes/Views/MapControl.cs:513 LoadMap 一致）：
///   22: Width(int16)  24: Height(int16)
///   28: 背景块 3 字节 / 2x2 单元，列主序 (x * (H/2) + y)
///   之后: Cell 14 字节 / 格，列主序 (x * H + y)
/// </summary>
public sealed class MapFile
{
    public int Width;
    public int Height;
    public MapCell[] Cells = Array.Empty<MapCell>();

    public MapCell this[int x, int y] => Cells[x * Height + y];

    public static MapFile Load(byte[] data)
    {
        using MemoryStream ms = new(data, writable: false);
        using BinaryReader reader = new(ms);

        ms.Seek(22, SeekOrigin.Begin);
        MapFile map = new()
        {
            Width = reader.ReadInt16(),
            Height = reader.ReadInt16(),
        };

        if (map.Width <= 0 || map.Height <= 0)
            throw new InvalidDataException($"非法地图尺寸: {map.Width}x{map.Height}");

        long expected = 28L + (long)(map.Width / 2) * (map.Height / 2) * 3 + (long)map.Width * map.Height * 14;
        if (data.Length < expected)
            throw new InvalidDataException($"地图数据不完整：需要 {expected:N0} 字节，实际 {data.Length:N0}");

        map.Cells = new MapCell[map.Width * map.Height];

        // ---- 背景层：3 字节 / 2x2 单元 ----
        ms.Seek(28, SeekOrigin.Begin);
        for (int x = 0; x < map.Width / 2; x++)
        {
            for (int y = 0; y < map.Height / 2; y++)
            {
                byte file = reader.ReadByte();
                ushort image = reader.ReadUInt16();

                ref MapCell cell = ref map.Cells[(x * 2) * map.Height + (y * 2)];
                cell.BackFile = file;
                cell.BackImage = image;
            }
        }

        // ---- 前景/中景层：14 字节 / 格 ----
        for (int x = 0; x < map.Width; x++)
        {
            for (int y = 0; y < map.Height; y++)
            {
                ref MapCell cell = ref map.Cells[x * map.Height + y];

                // 与 Zircon MapControl.cs:564 一致：
                //   可通行要求低两位都是 1，即 (flag & 0x03) == 0x03
                //   Flag 为 true 表示阻挡
                byte flag = reader.ReadByte();
                cell.Flag = (flag & 0x03) != 0x03;
                cell.MiddleAnimationFrame = reader.ReadByte();

                byte value = reader.ReadByte();
                cell.FrontAnimationFrame = (byte)(value == 255 ? 0 : value);
                cell.FrontAnimationFrame &= 0x8F; // 高位是混合标志

                cell.FrontFile = reader.ReadByte();
                cell.MiddleFile = reader.ReadByte();

                cell.MiddleImage = (ushort)(reader.ReadUInt16() + 1);
                cell.FrontImage = (ushort)(reader.ReadUInt16() + 1);

                ms.Seek(3, SeekOrigin.Current);
                cell.Light = (byte)((reader.ReadByte() & 0x0F) * 2);
                ms.Seek(1, SeekOrigin.Current);
            }
        }

        return map;
    }

    /// <summary>该格是否阻挡（Flag 位）。</summary>
    public bool IsBlocking(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return true;
        return Cells[x * Height + y].Flag;
    }

    /// <summary>仅阻挡判定用的轻量版本：不含 Flag 时也可用图块是否存在来推断。</summary>
    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
}
