using System.IO;
using Mir.Lib;

namespace MapExtract2;

/// <summary>
/// 把一个“完整 Lib”蒸馏成“仅包含本图用到的图”的 Lib。
///
/// 设计要点（这是整个方案不破坏现有客户端代码的关键）：
///  1. 保持图数量(count)与索引顺序完全不变 —— 用到的图整段字节原样拷贝，
///     未用到的图写成 17 字节零尺寸占位（Width=Height=Length=0，无遮罩）。
///     这样客户端按 (库名, 索引) 命中时，偏移表 _indexList 依然正确，零改动即可读取。
///  2. 版本无关：version==2 无 frameSeek / 无帧表；version>=3 有，两种都按原样搬运（含帧表）。
///  3. 流式读写：源 Lib 可能数百 MB（如 Tiles.Lib 450MB / 15 万图），绝不整体载入内存。
///
/// Lib 的二进制解析（版本/索引表/帧表/单图 blob）已抽到 <see cref="MirLibReader"/>（MirLib 库），
/// 本处只负责“蒸馏”算法本身，保证输出与客户端 <c>MLibrary</c> 读取格式逐字节一致。
/// </summary>
public static class LibDistiller
{
    private static readonly byte[] Zeros17 = new byte[17];

    /// <summary>蒸馏源 Lib，返回蒸馏后 Lib 的字节。</summary>
    /// <param name="srcPath">完整 Lib 路径（如 .../Data/Map/WemadeMir2/Tiles.Lib）</param>
    /// <param name="usedIndices">本图用到的图索引集合（其余写成占位）</param>
    public static byte[] Distill(string srcPath, HashSet<int> usedIndices)
    {
        using var lib = new MirLibReader(srcPath);
        int count = lib.Count;
        bool hasFrameSeek = lib.HasFrames;

        // 计算输出布局
        int headerSize = 4 + 4 + (hasFrameSeek ? 4 : 0) + count * 4;
        int[] outIndex = new int[count];
        int pos = headerSize;
        for (int i = 0; i < count; i++)
        {
            outIndex[i] = pos;
            pos += usedIndices.Contains(i) ? lib.GetImageLength(i) : 17;
        }

        using var outMs = new MemoryStream(pos + lib.FrameTable.Length + 16);
        using var w = new BinaryWriter(outMs);

        w.Write(lib.Version);
        w.Write(count);
        long frameSeekPos = outMs.Position;
        if (hasFrameSeek) w.Write(0); // 占位，稍后回填

        for (int i = 0; i < count; i++) w.Write(outIndex[i]);

        for (int i = 0; i < count; i++)
        {
            if (usedIndices.Contains(i))
                w.Write(lib.ReadImageBytes(i));
            else
                w.Write(Zeros17);
        }

        int frameSeekValue = (int)outMs.Position;
        if (lib.FrameTable.Length > 0) w.Write(lib.FrameTable);

        // 回填 frameSeek
        outMs.Position = frameSeekPos;
        w.Write(frameSeekValue);
        w.Flush();

        return outMs.ToArray();
    }
}
