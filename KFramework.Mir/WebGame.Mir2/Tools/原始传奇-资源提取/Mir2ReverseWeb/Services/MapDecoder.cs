using System.Collections.Generic;
using Mir2ReverseWeb.Models;

namespace Mir2ReverseWeb.Services;

/// <summary>
/// 地图(.dat) 解码器：纯逻辑、无 ASP/HTTP/Skia 依赖，便于将来合并进 MonoGame.Client。
/// 负责从已下载的 .dat 字节中解析索引、提取单张地图块、把 4-bit/格 解码为 MirMapData。
/// 格式（已逆向）：大端 [count:u16][ [id:i32][len:i32] … ][ 数据… ]
/// 每个地图数据块前 4 字节丢弃；其后为 4-bit/格（两格压一字节，高 nibble 在前），
/// 单元格 bit3=可走 / bit2=遮挡 / bit1=安全区。
/// </summary>
public static class MapDecoder
{
    private static int ReadBEInt(byte[] d, int p)
        => (d[p] << 24) | (d[p + 1] << 16) | (d[p + 2] << 8) | d[p + 3];

    /// <summary>解析 .dat 索引，返回每张地图的 id / 长度</summary>
    public static List<MapEntry> ParseIndex(byte[] data)
    {
        var list = new List<MapEntry>();
        if (data == null || data.Length < 2) return list;
        int count = (data[0] << 8) | data[1];
        int pos = 2;
        for (int i = 0; i < count && pos < data.Length; i++)
        {
            if (pos + 8 > data.Length) break;
            int id = ReadBEInt(data, pos);
            int len = ReadBEInt(data, pos + 4);
            pos += 8;
            if (len < 0 || len > data.Length - pos) break;
            list.Add(new MapEntry { Id = id, Len = len, Kb = Math.Round(len / 1024.0, 2) });
            pos += len;
        }
        return list;
    }

    /// <summary>从 .dat 中提取指定 id 的地图数据块（已跳过块首 4 字节）</summary>
    public static byte[]? ExtractMapBlock(byte[] data, int id, out int rawLen)
    {
        rawLen = 0;
        if (data == null || data.Length < 2) return null;
        int count = (data[0] << 8) | data[1];
        int pos = 2;
        for (int i = 0; i < count && pos < data.Length; i++)
        {
            if (pos + 8 > data.Length) break;
            int mid = ReadBEInt(data, pos);
            int len = ReadBEInt(data, pos + 4);
            pos += 8;
            if (len < 0 || len > data.Length - pos) break;
            if (mid == id)
            {
                rawLen = len;
                if (len <= 4) return null;
                return data.Skip(pos + 4).Take(len - 4).ToArray();
            }
            pos += len;
        }
        return null;
    }

    /// <summary>
    /// 解码单张地图：推算网格、逐格解析标志，返回 MirMapData。
    /// gridWidth/gridHeight 取 GameDefine.MAP_GRID_*（48/32），用于由配置宽高反推网格。
    /// </summary>
    public static MirMapData Decode(int id, byte[] block, int cfgWidth, int cfgHeight,
        string name, int gridWidth, int gridHeight)
    {
        var map = new MirMapData { Id = id, Name = name, Width = cfgWidth, Height = cfgHeight };
        int rawCells = block.Length * 2;
        map.RawCells = rawCells;

        int cfgCols = 0, cfgRows = 0, cfgCells = 0;
        bool fromConfig = cfgWidth > 0 && cfgHeight > 0;
        if (fromConfig)
        {
            cfgCols = (int)Math.Ceiling(cfgWidth / (double)gridWidth);
            cfgRows = (int)Math.Ceiling(cfgHeight / (double)gridHeight);
            cfgCells = cfgCols * cfgRows;
        }

        int cols, rows;
        if (cfgCells > 0 && rawCells >= cfgCells)
        {
            // 配置与数据匹配，直接用客户端同款网格
            cols = cfgCols; rows = cfgRows;
        }
        else
        {
            // 配置缺失 / 不匹配：用真实单元格数 + 配置宽高比反推网格
            double aspect = fromConfig
                ? (cfgWidth * (double)gridHeight) / (cfgHeight * (double)gridWidth)
                : 1.0;
            cols = (int)Math.Max(1, Math.Round(Math.Sqrt(rawCells * aspect)));
            rows = (int)Math.Max(1, Math.Round(rawCells / (double)cols));
            map.Warn = cfgCells > 0
                ? $"配置格数({cfgCells})与数据块格数({rawCells})不符，已按数据长度+宽高比重算网格 {cols}x{rows}"
                : $"未找到配置宽高，已按数据块长度估算网格 {cols}x{rows}（正方形假设）";
        }

        int maxCells = cols * rows;
        map.Cols = cols;
        map.Rows = rows;
        map.CellFlags = new byte[maxCells];

        int idx = 0;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                int nib = 0;
                int bi = idx / 2;
                if (bi < block.Length)
                    nib = (idx % 2 == 0) ? ((block[bi] >> 4) & 0xF) : (block[bi] & 0xF);
                map.CellFlags[idx] = (byte)nib;
                if ((nib & 8) != 0) map.Walkable++;
                if ((nib & 4) != 0) map.Cover++;
                if ((nib & 2) != 0) map.Safe++;
                idx++;
            }
        }
        return map;
    }
}
