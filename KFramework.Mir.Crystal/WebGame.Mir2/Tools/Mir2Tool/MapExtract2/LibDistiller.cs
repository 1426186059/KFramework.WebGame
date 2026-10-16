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

        // 第一遍：确定每个 index 实际写入长度。
        // 关键防御：源 Lib 若被截断（某图实际字节 < 头部声明长度），ReadImageBytes 返回的 blob 会偏短，
        // 若仍按 GetImageLength 推进偏移，会导致蒸馏产物偏移表错位、客户端读取越界崩溃。
        // 这里把“截断/越界图”降级为 17 字节零尺寸占位（与未用到的图一致），保持偏移表连续一致。
        int[] writeLen = new int[count];
        bool[] usedValid = new bool[count];
        for (int i = 0; i < count; i++)
        {
            if (usedIndices.Contains(i) && lib.ImageFits(i))
            {
                usedValid[i] = true;
                writeLen[i] = lib.GetImageLength(i);
            }
            else
            {
                writeLen[i] = 17; // 未用到 或 源图截断：占位置
            }
        }

        // 计算输出布局
        int headerSize = 4 + 4 + (hasFrameSeek ? 4 : 0) + count * 4;
        int[] outIndex = new int[count];
        int pos = headerSize;
        for (int i = 0; i < count; i++)
        {
            outIndex[i] = pos;
            pos += writeLen[i];
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
            if (usedValid[i])
            {
                byte[] blob = lib.ReadImageBytes(i);
                int expected = lib.GetImageLength(i);
                if (blob.Length >= expected)
                    w.Write(blob, 0, expected);
                else
                {
                    // 极端兜底：实际偏短也照写，并补零到声明长度，保持偏移表一致（不越界）。
                    w.Write(blob);
                    w.Write(new byte[expected - blob.Length]);
                }
            }
            else
                w.Write(Zeros17);
        }

        int frameSeekValue = (int)outMs.Position;
        if (lib.FrameTable.Length > 0) w.Write(lib.FrameTable);

        // 回填 frameSeek（仅 version>=3 有帧表/帧偏移字段）。
        // version==2 无此字段：frameSeekPos 恰等于 outIndex[0] 的位置，
        // 若照写会把“文件尾 EOF”覆盖到首张图偏移，导致客户端读 idx0 越界崩溃。
        if (hasFrameSeek)
        {
            outMs.Position = frameSeekPos;
            w.Write(frameSeekValue);
        }
        w.Flush();

        return outMs.ToArray();
    }
}
