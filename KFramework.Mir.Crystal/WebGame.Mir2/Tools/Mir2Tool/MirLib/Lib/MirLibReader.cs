using System.IO;

namespace Mir.Lib;

/// <summary>
/// 轻量、可复用的 Mir Library (.Lib) 读取器：只解析「版本 / 图数量 / 帧表偏移 / 索引偏移表」，
/// 并支持按索引读取单张图的原始字节（17 字节头 + FBytes + 可选遮罩），不做 GZip 解压。
///
/// <para>把 Lib 二进制解析集中到 MirLib，供蒸馏、校验等多个工程复用，不再各自重写一遍解析逻辑。</para>
/// <para>与客户端 <c>MLibrary</c> 读取格式逐字节一致（17 字节头 + FBytes + 可选遮罩）。</para>
/// </summary>
public sealed class MirLibReader : IDisposable
{
    private readonly FileStream _stream;
    private readonly BinaryReader _reader;

    public string FileName { get; }

    /// <summary>Lib 版本：2 = 旧版（无帧表），>=3 = 含 frameSeek + 帧表。</summary>
    public int Version { get; }

    /// <summary>图片（帧）总数。</summary>
    public int Count { get; }

    /// <summary>version>=3 才有帧表。</summary>
    public bool HasFrames => Version >= 3;

    /// <summary>帧表在文件中的绝对偏移（version<3 时为 0）。</summary>
    public int FrameSeek { get; }

    /// <summary>每张图 blob 的 17 字节头在文件中的绝对偏移（与客户端 _indexList 对应）。</summary>
    public int[] IndexList { get; }

    /// <summary>version>=3 时的帧表原始字节（位于 FrameSeek 处直到文件尾）；version<3 为空。</summary>
    public byte[] FrameTable { get; }

    public MirLibReader(string fileName)
    {
        FileName = fileName;
        _stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20);
        _reader = new BinaryReader(_stream);

        Version = _reader.ReadInt32();
        if (Version < 2)
            throw new InvalidDataException($"Lib 版本过低: {Version}（期望 >= 2），{fileName}");
        Count = _reader.ReadInt32();

        int frameSeek = 0;
        if (HasFrames) frameSeek = _reader.ReadInt32();
        FrameSeek = frameSeek;

        IndexList = new int[Count];
        for (int i = 0; i < Count; i++) IndexList[i] = _reader.ReadInt32();

        byte[] frames = Array.Empty<byte>();
        if (HasFrames && frameSeek > 0 && frameSeek < _stream.Length)
        {
            _stream.Seek(frameSeek, SeekOrigin.Begin);
            frames = _reader.ReadBytes((int)(_stream.Length - frameSeek));
        }
        FrameTable = frames;
    }

    public void Dispose()
    {
        _reader?.Dispose();
        _stream?.Dispose();
    }

    /// <summary>读取第 index 张图的完整 blob 原始字节（17 字节头 + FBytes + 可选遮罩）。</summary>
    public byte[] ReadImageBytes(int index)
    {
        if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
        _stream.Seek(IndexList[index], SeekOrigin.Begin);
        byte[] header = _reader.ReadBytes(17);
        int len = System.BitConverter.ToInt32(header, 13);
        byte[] fBytes = _reader.ReadBytes(len);

        if ((header[12] & 0x80) != 0)
        {
            byte[] maskHeader = _reader.ReadBytes(12); // 8 字节几何 + 4 字节 MaskLength
            int maskLen = System.BitConverter.ToInt32(maskHeader, 8);
            byte[] maskF = _reader.ReadBytes(maskLen);
            var blob = new byte[17 + len + 12 + maskLen];
            System.Buffer.BlockCopy(header, 0, blob, 0, 17);
            System.Buffer.BlockCopy(fBytes, 0, blob, 17, len);
            System.Buffer.BlockCopy(maskHeader, 0, blob, 17 + len, 12);
            System.Buffer.BlockCopy(maskF, 0, blob, 17 + len + 12, maskLen);
            return blob;
        }

        var outBlob = new byte[17 + len];
        System.Buffer.BlockCopy(header, 0, outBlob, 0, 17);
        System.Buffer.BlockCopy(fBytes, 0, outBlob, 17, len);
        return outBlob;
    }

    /// <summary>第 index 张图 blob 的总字节长度（含 17 字节头 + FBytes + 可选遮罩）。</summary>
    public int GetImageLength(int index)
    {
        if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
        _stream.Seek(IndexList[index], SeekOrigin.Begin);
        byte[] header = _reader.ReadBytes(17);
        int len = System.BitConverter.ToInt32(header, 13);
        int size = 17 + len;
        if ((header[12] & 0x80) != 0)
        {
            byte[] maskHeader = _reader.ReadBytes(12);
            int maskLen = System.BitConverter.ToInt32(maskHeader, 8);
            size += 12 + maskLen;
        }
        return size;
    }

    /// <summary>
    /// 第 index 张图在源文件中声明的总长度是否“装得下”（IndexList[index] + 声明长度 &lt;= 文件尾）。
    /// 源 Lib 被截断时该图实际数据比声明长度短，<see cref="ReadImageBytes"/> 会返回偏短的 blob；
    /// 蒸馏器据此把该图降级为 17 字节占位，避免偏移表错位导致产物越界。
    /// </summary>
    public bool ImageFits(int index)
    {
        if (index < 0 || index >= Count) return false;
        long end = (long)IndexList[index] + GetImageLength(index);
        return end <= _stream.Length;
    }
}
