using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace KFramework.MonoGame
{
    /// <summary>字形的轮廓点（字体单位，y 轴向上）。</summary>
    internal struct ShapePoint
    {
        /// <summary>字体单位 x。</summary>
        public float X;

        /// <summary>字体单位 y（向上为正）。</summary>
        public float Y;

        /// <summary>是否为曲线上的点（false 表示二次贝塞尔的控制点）。</summary>
        public bool OnCurve;
    }

    /// <summary>一个字形的轮廓（可能含多个闭合轮廓；复合字形已展开合并）。</summary>
    internal sealed class GlyphShape
    {
        /// <summary>所有轮廓点，按轮廓顺序排列。</summary>
        public ShapePoint[] Points = Array.Empty<ShapePoint>();

        /// <summary>每个轮廓的最后一个点在 <see cref="Points"/> 中的下标。</summary>
        public int[] ContourEnds = Array.Empty<int>();

        public float XMin;
        public float YMin;
        public float XMax;
        public float YMax;

        /// <summary>空字形（空格 / .notdef 无轮廓）。</summary>
        public bool IsEmpty => Points.Length == 0;
    }

    /// <summary>
    /// TrueType / OpenType 字体解析（纯托管，不借浏览器 Canvas）：从字体字节里取字符映射、度量与字形轮廓。
    /// 供 <see cref="KFont"/> 在 C# 侧自行栅格化，使文字绘制全程走自研 WebGL 渲染管线。
    ///
    /// 支持：ttf / otf（TrueType 轮廓 glyf）/ woff / ttc（取集合里第一个字体）。
    /// 不支持：OTTO（CFF 轮廓）与 woff2（Brotli + glyf 变换），读入时抛 <see cref="NotSupportedException"/>。
    /// </summary>
    internal sealed class TrueTypeFace
    {
        private readonly struct Table
        {
            public readonly int Offset;
            public readonly int Length;
            public Table(int offset, int length) { Offset = offset; Length = length; }
        }

        #region 常量

        private const uint TagTtcf = 0x74746366; // 'ttcf' 字体集合
        private const uint TagOtto = 0x4F54544F; // 'OTTO' CFF 轮廓
        private const uint TagWoff = 0x774F4646; // 'wOFF'

        private const uint TagHead = 0x68656164;
        private const uint TagMaxp = 0x6D617870;
        private const uint TagHhea = 0x68686561;
        private const uint TagHmtx = 0x686D7478;
        private const uint TagCmap = 0x636D6170;
        private const uint TagLoca = 0x6C6F6361;
        private const uint TagGlyf = 0x676C7966;
        private const uint TagName = 0x6E616D65;

        // glyf 复合字形标志位
        private const ushort ArgWords = 0x0001;
        private const ushort ArgsAreXy = 0x0002;
        private const ushort HaveScale = 0x0008;
        private const ushort MoreComponents = 0x0020;
        private const ushort HaveXYScale = 0x0040;
        private const ushort HaveTwoByTwo = 0x0080;
        private const ushort ScaledOffset = 0x0800;
        private const ushort UnscaledOffset = 0x1000;

        private const int MaxCompositeDepth = 5;

        #endregion

        private readonly byte[] _data;
        private readonly Dictionary<uint, Table> _tables = new();

        private readonly int _unitsPerEm;
        private readonly int _numGlyphs;
        private readonly int _numberOfHMetrics;
        private readonly int _indexToLocFormat;

        private readonly int _hmtxOffset;
        private readonly int _locaOffset;
        private readonly int _glyfOffset;

        private readonly int _cmapSubtable = -1;
        private readonly int _cmapFormat;

        private string? _familyName;

        /// <summary>每 em 的字体单位数（head.unitsPerEm）。</summary>
        public int UnitsPerEm => _unitsPerEm;

        /// <summary>字体全局 ascender（字体单位，基线到行顶）。</summary>
        public int Ascender { get; }

        /// <summary>字体全局 descender（字体单位，通常为负）。</summary>
        public int Descender { get; }

        /// <summary>行间距（字体单位）。</summary>
        public int LineGap { get; }

        /// <summary>字体名（name 表 nameID 1；取不到时为 "<unnamed>"）。</summary>
        public string FamilyName => _familyName ??= ReadFamilyName();

        private TrueTypeFace(byte[] sfnt)
        {
            _data = sfnt;
            if (_data.Length < 12) throw new ArgumentException("不是合法字体文件：字节过短。", nameof(sfnt));

            // sfnt 头的布局：version(4) numTables(2) searchRange(2) entrySelector(2) rangeShift(2)，其后才是表目录
            int headerOffset = 0;
            uint version = U32(0);
            if (version == TagTtcf)
            {
                // 字体集合：取集合里的第一个字体（sfnt 头偏移写在 ttc 头之后）
                if (_data.Length < 16) throw new ArgumentException("不是合法字体集合（ttc）：字节过短。", nameof(sfnt));
                headerOffset = (int)U32(12);
                version = U32(headerOffset);
            }

            if (version == TagOtto)
                throw new NotSupportedException("暂不支持 CFF 轮廓（OTTO）字体，请改用 TrueType 轮廓（glyf）的 ttf / otf / woff。");

            int tableCount = U16(headerOffset + 4);
            for (int i = 0; i < tableCount; i++)
            {
                int rec = headerOffset + 12 + i * 16;
                uint tag = U32(rec);
                int offset = (int)U32(rec + 8);
                int length = (int)U32(rec + 12);
                _tables[tag] = new Table(offset, length);
            }

            Table head = Require(TagHead, "head");
            Table maxp = Require(TagMaxp, "maxp");
            Table hhea = Require(TagHhea, "hhea");
            Table hmtx = Require(TagHmtx, "hmtx");
            Table loca = Require(TagLoca, "loca");
            Table glyf = Require(TagGlyf, "glyf");

            _unitsPerEm = U16(head.Offset + 18);
            if (_unitsPerEm <= 0) throw new ArgumentException("字体 head 表的 unitsPerEm 非法。", nameof(sfnt));
            _indexToLocFormat = I16(head.Offset + 50);
            _numGlyphs = U16(maxp.Offset + 4);
            Ascender = I16(hhea.Offset + 4);
            Descender = I16(hhea.Offset + 6);
            LineGap = I16(hhea.Offset + 8);
            _numberOfHMetrics = U16(hhea.Offset + 34);
            if (_numberOfHMetrics <= 0) _numberOfHMetrics = 1;

            _hmtxOffset = hmtx.Offset;
            _locaOffset = loca.Offset;
            _glyfOffset = glyf.Offset;

            PickCmapSubtable(out _cmapSubtable, out _cmapFormat);
        }

        /// <summary>解析字体字节（ttf / otf(glyf) / woff / ttc）。</summary>
        public static TrueTypeFace Load(byte[] fontBytes)
        {
            ArgumentNullException.ThrowIfNull(fontBytes);
            if (fontBytes.Length < 4) throw new ArgumentException("不是合法字体文件：字节过短。", nameof(fontBytes));

            uint signature = BinaryPrimitives.ReadUInt32BigEndian(fontBytes.AsSpan(0, 4));
            byte[] sfnt = signature == TagWoff ? UnpackWoff(fontBytes) : fontBytes;
            return new TrueTypeFace(sfnt);
        }

        #region 字符映射与度量

        /// <summary>取字符对应的字形索引；0 表示该字体没有这个字形。</summary>
        public int LookupGlyph(char c)
        {
            if (_cmapSubtable < 0) return 0;
            int cp = c;
            switch (_cmapFormat)
            {
                case 0: return LookupFormat0(cp);
                case 4: return LookupFormat4(cp);
                case 6: return LookupFormat6(cp);
                case 12: return LookupFormat12(cp);
                default: return 0;
            }
        }

        /// <summary>取字形的推进宽度（字体单位）。</summary>
        public int GetAdvance(int glyphIndex)
        {
            if (glyphIndex < 0 || glyphIndex >= _numGlyphs) return 0;
            int index = glyphIndex < _numberOfHMetrics ? glyphIndex : _numberOfHMetrics - 1;
            return U16(_hmtxOffset + index * 4);
        }

        #endregion

        #region 字形轮廓

        /// <summary>取字形轮廓（字体单位）；复合字形已展开，空字形返回 <see cref="GlyphShape.IsEmpty"/>。</summary>
        public GlyphShape GetShape(int glyphIndex)
        {
            if (glyphIndex <= 0 || glyphIndex >= _numGlyphs) return new GlyphShape();
            // loca 里记的是相对 glyf 表起始的偏移，要加上 glyf 表自身的位置才是文件内的绝对偏移
            int start = _glyfOffset + GlyphOffset(glyphIndex);
            int end = _glyfOffset + GlyphOffset(glyphIndex + 1);
            if (start < 0 || end <= start) return new GlyphShape();

            short contours = I16(start);
            if (contours == 0) return new GlyphShape();
            return contours > 0 ? ReadSimpleGlyph(start, contours) : ReadCompositeGlyph(start + 2, 0);
        }

        private int GlyphOffset(int glyphIndex)
        {
            if (glyphIndex < 0 || glyphIndex > _numGlyphs) return -1;
            return _indexToLocFormat == 0
                ? U16(_locaOffset + glyphIndex * 2) * 2
                : (int)U32(_locaOffset + glyphIndex * 4);
        }

        private GlyphShape ReadSimpleGlyph(int start, int contours)
        {
            int off = start + 10;
            var ends = new int[contours];
            for (int i = 0; i < contours; i++) { ends[i] = U16(off); off += 2; }

            int pointCount = ends[contours - 1] + 1;
            off += 2 + U16(off); // 跳过指令长度与指令

            var flags = new byte[pointCount];
            for (int i = 0; i < pointCount;)
            {
                byte f = _data[off++];
                flags[i++] = f;
                if ((f & 0x08) != 0)
                {
                    int repeat = _data[off++];
                    while (repeat-- > 0 && i < pointCount) flags[i++] = f;
                }
            }

            var points = new ShapePoint[pointCount];
            int x = 0;
            for (int i = 0; i < pointCount; i++)
            {
                byte f = flags[i];
                if ((f & 0x02) != 0)
                {
                    int dx = _data[off++];
                    if ((f & 0x10) == 0) dx = -dx;
                    x += dx;
                }
                else if ((f & 0x10) == 0)
                {
                    x += I16(off);
                    off += 2;
                }
                points[i].X = x;
            }

            int y = 0;
            for (int i = 0; i < pointCount; i++)
            {
                byte f = flags[i];
                if ((f & 0x04) != 0)
                {
                    int dy = _data[off++];
                    if ((f & 0x20) == 0) dy = -dy;
                    y += dy;
                }
                else if ((f & 0x20) == 0)
                {
                    y += I16(off);
                    off += 2;
                }
                points[i].Y = y;
                points[i].OnCurve = (f & 0x01) != 0;
            }

            var shape = new GlyphShape { Points = points, ContourEnds = ends };
            ComputeBounds(shape);
            return shape;
        }

        private GlyphShape ReadCompositeGlyph(int offset, int depth)
        {
            var points = new List<ShapePoint>(64);
            var ends = new List<int>(4);

            if (depth > MaxCompositeDepth)
            {
                PrintTool.Log("[KFont] 复合字形嵌套超过 5 层，已停止展开（字形可能缺笔画）。");
                return new GlyphShape();
            }

            int off = offset;
            ushort flags;
            do
            {
                flags = U16(off); off += 2;
                int glyphIndex = U16(off); off += 2;

                float dx;
                float dy;
                if ((flags & ArgWords) != 0) { dx = I16(off); dy = I16(off + 2); off += 4; }
                else { dx = (sbyte)_data[off]; dy = (sbyte)_data[off + 1]; off += 2; }

                // 2x2 变换：[a c; b d] 作用于 (x, y)
                float a = 1f, b = 0f, c = 0f, d = 1f;
                if ((flags & HaveTwoByTwo) != 0)
                {
                    a = F2Dot14(off); b = F2Dot14(off + 2); c = F2Dot14(off + 4); d = F2Dot14(off + 6);
                    off += 8;
                }
                else if ((flags & HaveXYScale) != 0)
                {
                    a = F2Dot14(off); d = F2Dot14(off + 2); off += 4;
                }
                else if ((flags & HaveScale) != 0)
                {
                    a = d = F2Dot14(off); off += 2;
                }

                // ARGS_ARE_XY_VALUES 未置位时是「按点对齐」，字体里极少出现，这里退化为按 (0,0) 偏移。
                if ((flags & ArgsAreXy) == 0) { dx = 0f; dy = 0f; }

                float ox = dx;
                float oy = dy;
                if ((flags & ScaledOffset) != 0 && (flags & UnscaledOffset) == 0)
                {
                    ox = a * dx + c * dy;
                    oy = b * dx + d * dy;
                }

                GlyphShape child = GetShape(glyphIndex);
                if (child.IsEmpty) continue;

                int baseIndex = points.Count;
                for (int i = 0; i < child.Points.Length; i++)
                {
                    ShapePoint p = child.Points[i];
                    points.Add(new ShapePoint
                    {
                        X = a * p.X + c * p.Y + ox,
                        Y = b * p.X + d * p.Y + oy,
                        OnCurve = p.OnCurve,
                    });
                }
                foreach (int end in child.ContourEnds) ends.Add(baseIndex + end);
            }
            while ((flags & MoreComponents) != 0);

            var shape = new GlyphShape { Points = points.ToArray(), ContourEnds = ends.ToArray() };
            ComputeBounds(shape);
            return shape;
        }

        private static void ComputeBounds(GlyphShape shape)
        {
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            foreach (ShapePoint p in shape.Points)
            {
                if (p.X < xMin) xMin = p.X;
                if (p.X > xMax) xMax = p.X;
                if (p.Y < yMin) yMin = p.Y;
                if (p.Y > yMax) yMax = p.Y;
            }
            shape.XMin = xMin;
            shape.YMin = yMin;
            shape.XMax = xMax;
            shape.YMax = yMax;
        }

        #endregion

        #region cmap

        private void PickCmapSubtable(out int offset, out int format)
        {
            offset = -1;
            format = 0;
            if (!_tables.TryGetValue(TagCmap, out Table cmap)) return;

            int count = U16(cmap.Offset + 2);
            int best = -1;
            int bestScore = -1;
            int bestFormat = 0;
            for (int i = 0; i < count; i++)
            {
                int rec = cmap.Offset + 4 + i * 8;
                int platform = U16(rec);
                int encoding = U16(rec + 2);
                int sub = cmap.Offset + (int)U32(rec + 4);
                int fmt = U16(sub);
                if (fmt != 0 && fmt != 4 && fmt != 6 && fmt != 12) continue;

                // 优先 UCS-4（能覆盖增补平面），其次 Windows BMP，其次 Unicode 通用，最后 Mac
                int score = fmt == 12 ? 5 : 0;
                if (platform == 3 && (encoding == 10 || encoding == 1)) score += 4;
                else if (platform == 0) score += 3;
                else if (platform == 1) score += 1;
                if (score > bestScore) { bestScore = score; best = sub; bestFormat = fmt; }
            }
            offset = best;
            format = bestFormat;
        }

        private int LookupFormat0(int cp)
        {
            if (cp > 0xFF) return 0;
            return _data[_cmapSubtable + 6 + cp];
        }

        private int LookupFormat6(int cp)
        {
            int first = U16(_cmapSubtable + 6);
            int count = U16(_cmapSubtable + 8);
            if (cp < first || cp >= first + count) return 0;
            return U16(_cmapSubtable + 10 + (cp - first) * 2);
        }

        private int LookupFormat4(int cp)
        {
            int segCount = U16(_cmapSubtable + 6) / 2;
            int endBase = _cmapSubtable + 14;
            int startBase = endBase + segCount * 2 + 2;
            int deltaBase = startBase + segCount * 2;
            int rangeBase = deltaBase + segCount * 2;

            for (int i = 0; i < segCount; i++)
            {
                int end = U16(endBase + i * 2);
                if (cp > end) continue;
                int start = U16(startBase + i * 2);
                if (cp < start) return 0;

                int delta = I16(deltaBase + i * 2);
                int rangeOffset = U16(rangeBase + i * 2);
                if (rangeOffset == 0) return (cp + delta) & 0xFFFF;

                int addr = rangeBase + i * 2 + rangeOffset + (cp - start) * 2;
                if (addr + 1 >= _data.Length) return 0;
                int glyph = U16(addr);
                return glyph == 0 ? 0 : (glyph + delta) & 0xFFFF;
            }
            return 0;
        }

        private int LookupFormat12(int cp)
        {
            int groups = (int)U32(_cmapSubtable + 12);
            int lo = 0;
            int hi = groups - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                int rec = _cmapSubtable + 16 + mid * 12;
                int start = (int)U32(rec);
                int end = (int)U32(rec + 4);
                if (cp < start) { hi = mid - 1; continue; }
                if (cp > end) { lo = mid + 1; continue; }
                return (int)(U32(rec + 8) + (uint)(cp - start));
            }
            return 0;
        }

        #endregion

        #region name 表

        private string ReadFamilyName()
        {
            if (!_tables.TryGetValue(TagName, out Table name)) return "<unnamed>";
            try
            {
                int count = U16(name.Offset + 2);
                int storage = name.Offset + U16(name.Offset + 4);
                for (int i = 0; i < count; i++)
                {
                    int rec = name.Offset + 6 + i * 12;
                    if (U16(rec + 6) != 1) continue; // nameID 1 = Font Family
                    int length = U16(rec + 8);
                    int offset = U16(rec + 10);
                    int platform = U16(rec);
                    if (platform == 3 || platform == 0)
                    {
                        var sb = new StringBuilder(length / 2);
                        for (int k = 0; k + 1 < length; k += 2)
                            sb.Append((char)U16(storage + offset + k));
                        string value = sb.ToString();
                        if (value.Length > 0) return value;
                    }
                    else
                    {
                        var bytes = new byte[length];
                        Array.Copy(_data, storage + offset, bytes, 0, length);
                        string value = Encoding.UTF8.GetString(bytes);
                        if (value.Length > 0) return value;
                    }
                }
            }
            catch (Exception e)
            {
                PrintTool.Log($"[KFont] 读取字体名失败（不影响绘制）：{e.Message}");
            }
            return "<unnamed>";
        }

        #endregion

        #region WOFF 解包

        /// <summary>把 woff 还原成 sfnt：表目录重建 + 各表 zlib 解压（未压缩的表原样拷贝）。</summary>
        private static byte[] UnpackWoff(byte[] data)
        {
            if (data.Length < 44) throw new ArgumentException("不是合法的 woff：头部过短。", nameof(data));

            uint flavor = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(4, 4));
            int numTables = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(12, 2));
            uint totalSfntSize = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(16, 4));

            var records = new (uint Tag, int Offset, int CompLength, int OrigLength)[numTables];
            for (int i = 0; i < numTables; i++)
            {
                int rec = 44 + i * 20;
                if (rec + 20 > data.Length) throw new ArgumentException("不是合法的 woff：表目录不完整。", nameof(data));
                records[i] = (
                    BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(rec, 4)),
                    (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(rec + 4, 4)),
                    (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(rec + 8, 4)),
                    (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(rec + 12, 4)));
            }

            Array.Sort(records, (x, y) => x.Tag.CompareTo(y.Tag));

            int header = 12 + numTables * 16;
            int body = header;
            foreach (var r in records) body += (r.OrigLength + 3) & ~3;

            byte[] sfnt = new byte[Math.Max((int)totalSfntSize, body)];
            WriteU32(sfnt, 0, flavor);
            WriteU16(sfnt, 4, (ushort)numTables);

            int entrySelector = 0;
            while (1 << (entrySelector + 1) <= numTables) entrySelector++;
            int searchRange = (1 << entrySelector) * 16;
            WriteU16(sfnt, 6, (ushort)searchRange);
            WriteU16(sfnt, 8, (ushort)entrySelector);
            WriteU16(sfnt, 10, (ushort)(numTables * 16 - searchRange));

            for (int i = 0; i < numTables; i++)
            {
                var r = records[i];
                int rec = 12 + i * 16;
                WriteU32(sfnt, rec, r.Tag);
                WriteU32(sfnt, rec + 8, (uint)body);
                WriteU32(sfnt, rec + 12, (uint)r.OrigLength);

                if (r.CompLength == r.OrigLength)
                {
                    Array.Copy(data, r.Offset, sfnt, body, r.OrigLength);
                }
                else
                {
                    try
                    {
                        using var src = new MemoryStream(data, r.Offset, r.CompLength, false);
                        using var zlib = new ZLibStream(src, CompressionMode.Decompress);
                        int read = 0;
                        while (read < r.OrigLength)
                        {
                            int n = zlib.Read(sfnt, body + read, r.OrigLength - read);
                            if (n <= 0) break;
                            read += n;
                        }
                    }
                    catch (Exception e)
                    {
                        throw new NotSupportedException(
                            $"woff 表解压失败（{r.Tag:X8}）：{e.Message}。建议改用未压缩的 ttf / otf。", e);
                    }
                }
                body += (r.OrigLength + 3) & ~3;
            }

            return sfnt;
        }

        private static void WriteU16(byte[] buffer, int offset, ushort value)
            => BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset, 2), value);

        private static void WriteU32(byte[] buffer, int offset, uint value)
            => BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset, 4), value);

        #endregion

        #region 字节读取

        private Table Require(uint tag, string name)
        {
            if (!_tables.TryGetValue(tag, out Table table))
                throw new ArgumentException($"字体缺少必需的表：{name}。");
            return table;
        }

        private void Require(int offset, int size)
        {
            if (offset < 0 || offset + size > _data.Length)
                throw new ArgumentException("字体字节不完整或已损坏（读取越界）。");
        }

        private ushort U16(int offset)
        {
            Require(offset, 2);
            return BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(offset));
        }

        private short I16(int offset)
        {
            Require(offset, 2);
            return BinaryPrimitives.ReadInt16BigEndian(_data.AsSpan(offset));
        }

        private uint U32(int offset)
        {
            Require(offset, 4);
            return BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(offset));
        }

        private float F2Dot14(int offset) => I16(offset) / 16384f;

        #endregion
    }
}
