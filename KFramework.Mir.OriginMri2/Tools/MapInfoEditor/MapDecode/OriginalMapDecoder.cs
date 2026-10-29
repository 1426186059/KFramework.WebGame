using System.Collections.Generic;
using System.Linq;

namespace MapInfoEditor
{
    /// <summary>
    /// 原始传奇地图(.dat) 解码器：纯逻辑、无依赖，移植自服务器 Server.MirEnvir.OriginalMapDecoder。
    /// 格式：大端 [count:u16][ [id:i32][len:i32] … ][ 数据… ]
    /// 每个地图数据块前 4 字节丢弃；其后为 4-bit/格（高 nibble 在前），
    /// 单元格 bit3=墙(不可走) / bit2=遮挡 / bit1=安全区。网格 cols=ceil(宽/48) rows=ceil(高/32)。
    /// </summary>
    public static class OriginalMapDecoder
    {
        private static int ReadBEInt(byte[] d, int p)
            => (d[p] << 24) | (d[p + 1] << 16) | (d[p + 2] << 8) | d[p + 3];

        public static List<OriginalMapEntry> ParseIndex(byte[] data)
        {
            var list = new List<OriginalMapEntry>();
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
                list.Add(new OriginalMapEntry { Id = id, Len = len, Kb = System.Math.Round(len / 1024.0, 2) });
                pos += len;
            }
            return list;
        }

        public static byte[] ExtractMapBlock(byte[] data, int id, out int rawLen)
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

        public static OriginalMapData Decode(int id, byte[] block, int cfgWidth, int cfgHeight,
            string name, int gridWidth, int gridHeight)
        {
            var map = new OriginalMapData { Id = id, Name = name, Width = cfgWidth, Height = cfgHeight };
            int rawCells = block.Length * 2;
            map.RawCells = rawCells;

            int cfgCols = 0, cfgRows = 0, cfgCells = 0;
            bool fromConfig = cfgWidth > 0 && cfgHeight > 0;
            if (fromConfig)
            {
                cfgCols = (int)System.Math.Ceiling(cfgWidth / (double)gridWidth);
                cfgRows = (int)System.Math.Ceiling(cfgHeight / (double)gridHeight);
                cfgCells = cfgCols * cfgRows;
            }

            int cols, rows;
            if (cfgCells > 0 && rawCells >= cfgCells)
            {
                cols = cfgCols; rows = cfgRows;
            }
            else
            {
                double aspect = fromConfig
                    ? (cfgWidth * (double)gridHeight) / (cfgHeight * (double)gridWidth)
                    : 1.0;
                cols = (int)System.Math.Max(1, System.Math.Round(System.Math.Sqrt(rawCells * aspect)));
                rows = (int)System.Math.Max(1, System.Math.Round(rawCells / (double)cols));
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
                    if ((nib & 8) == 0) map.Walkable++;
                    if ((nib & 4) != 0) map.Cover++;
                    if ((nib & 2) != 0) map.Safe++;
                    idx++;
                }
            }
            return map;
        }
    }
}
