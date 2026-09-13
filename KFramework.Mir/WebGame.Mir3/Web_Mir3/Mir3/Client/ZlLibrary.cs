using System.IO;

namespace MirClient.Assets;

public enum ZlCodec : byte
{
    Dxt1,
    Dxt5,
    Bgra32,
    Bc7,
    Png,
}

/// <summary>ZL2 容器外层压缩方式（对应 Zircon 的 ZlContainerCompression）。</summary>
public enum ZlContainerCompression : byte
{
    None,
    DeflateFast,
    DeflateBest,
}

/// <summary>
/// 原版 Mir3 .Zl 资源库中的一张图（索引条目）。
/// 与 Zircon 的 ZlImageMetadata 二进制兼容，但不依赖 System.Drawing / 原生纹理。
/// </summary>
public sealed class ZlImage
{
    public int Index;
    public int Position;
    public short Width;
    public short Height;
    public short OffSetX;
    public short OffSetY;
    public byte ShadowType;
    public short ShadowWidth;
    public short ShadowHeight;
    public short ShadowOffSetX;
    public short ShadowOffSetY;
    public short OverlayWidth;
    public short OverlayHeight;

    public ZlCodec Codec;
    public int StoredDataSize;

    /// <summary>外层压缩方式（ZL2 容器）。旧格式恒为 None。</summary>
    public ZlContainerCompression Compression;

    /// <summary>从文件读取的 payload 字节数（压缩时为 CompressedSize）。</summary>
    public int PayloadSize;

    public bool HasShadow => ShadowWidth > 0 && ShadowHeight > 0;
    public bool IsDxt => Codec is ZlCodec.Dxt1 or ZlCodec.Dxt5;

    public int DataSize => StoredDataSize > 0 ? StoredDataSize : ComputeSize(Width, Height, Codec);

    public static int ComputeSize(int width, int height, ZlCodec codec) => codec switch
    {
        ZlCodec.Dxt1 => BlockCount(width, height) * 8,
        ZlCodec.Dxt5 => BlockCount(width, height) * 16,
        ZlCodec.Bc7 => BlockCount(width, height) * 16,
        ZlCodec.Bgra32 => Math.Max(0, width) * Math.Max(0, height) * 4,
        _ => 0,
    };

    public static int BlockCount(int width, int height)
        => ((Math.Max(0, width) + 3) / 4) * ((Math.Max(0, height) + 3) / 4);
}

/// <summary>
/// .Zl 资源库。索引区在文件头部，图片数据按 Position 随机访问——
/// 因此 Web 端可只下载索引，再按需拉取图片数据（HTTP Range）。
/// </summary>
public sealed class ZlLibrary
{
    public string Name = string.Empty;
    public int Version;
    public ZlImage?[] Images = Array.Empty<ZlImage>();

    /// <summary>索引区字节数，Web 端首次只需拉取这一段。</summary>
    public int IndexRegionSize { get; private set; }

    public int ValidCount { get; private set; }

    public static ZlLibrary ReadIndex(ReadOnlySpan<byte> fileHeader)
    {
        // 新格式：ZL2 容器（version>=2），图片为 PNG/BC7 等且可能压缩。
        if (fileHeader.Length >= 3 && fileHeader[0] == (byte)'Z' && fileHeader[1] == (byte)'L' && fileHeader[2] == (byte)'2')
            return ReadIndexZL2(fileHeader);
        return ReadIndexLegacy(fileHeader);
    }

    private static ZlLibrary ReadIndexLegacy(ReadOnlySpan<byte> fileHeader)
    {
        ZlLibrary lib = new();
        if (fileHeader.Length < 4)
            throw new InvalidDataException("文件过短，无法读取索引长度");

        int indexSize = BitConverter.ToInt32(fileHeader);
        if (indexSize <= 4 || indexSize > fileHeader.Length - 4)
            throw new InvalidDataException($"非法的索引长度: {indexSize}");

        lib.IndexRegionSize = 4 + indexSize;

        using MemoryStream ms = new(fileHeader.Slice(4, indexSize).ToArray(), writable: false);
        using BinaryReader reader = new(ms);

        int value = reader.ReadInt32();
        int count = value & 0x1FFFFFF;
        int version = (value >> 25) & 0x7F;
        if (version == 0)
            count = value;

        if (count < 0 || count > 10_000_000)
            throw new InvalidDataException($"非法的图片数量: {count}");

        lib.Version = version;
        lib.Images = new ZlImage[count];

        for (int i = 0; i < count; i++)
        {
            if (!reader.ReadBoolean())
                continue;

            ZlImage img = new()
            {
                Index = i,
                Position = reader.ReadInt32(),
                Width = reader.ReadInt16(),
                Height = reader.ReadInt16(),
                OffSetX = reader.ReadInt16(),
                OffSetY = reader.ReadInt16(),
                ShadowType = reader.ReadByte(),
                ShadowWidth = reader.ReadInt16(),
                ShadowHeight = reader.ReadInt16(),
                ShadowOffSetX = reader.ReadInt16(),
                ShadowOffSetY = reader.ReadInt16(),
                OverlayWidth = reader.ReadInt16(),
                OverlayHeight = reader.ReadInt16(),
            };

            if (version >= 2)
            {
                reader.ReadInt32();    // AtlasPage
                reader.ReadBytes(8);   // SourceRectangle
                reader.ReadBytes(8);   // VisibleBounds
                img.Codec = (ZlCodec)reader.ReadByte();
                reader.ReadBytes(3);   // Shadow/Overlay Codec
                reader.ReadBytes(3);   // RuntimePreference
                img.StoredDataSize = reader.ReadInt32();
                reader.ReadBytes(36);  // 其余 BC7/Fallback 尺寸字段
            }
            else
            {
                // 原版 Mir3：version 0 = DXT1，其余 = DXT5
                img.Codec = version == 0 ? ZlCodec.Dxt1 : ZlCodec.Dxt5;
            }

            img.Compression = ZlContainerCompression.None;
            img.PayloadSize = img.DataSize;

            lib.Images[i] = img;
            lib.ValidCount++;
        }

        return lib;
    }

    /// <summary>
    /// 解析 ZL2 新格式容器（对应 Zircon 的 TryReadCompressedContainer）。
    /// 以 metadata 为主构建 Images（按 metadata 索引，含 Width/Height/OffSet），
    /// 用 index 的 Zl2Entry 按"数据偏移"补全外层压缩方式与压缩后大小。
    /// </summary>
    private static ZlLibrary ReadIndexZL2(ReadOnlySpan<byte> fileHeader)
    {
        ZlLibrary lib = new();
        byte[] all = fileHeader.ToArray();
        using var ms = new MemoryStream(all, writable: false);
        using var r = new BinaryReader(ms);

        r.ReadBytes(3); // "ZL2" 签名
        int version = r.ReadInt32();
        int imageCount = r.ReadInt32();
        int atlasCount = r.ReadInt32();
        r.ReadByte();   // 默认压缩
        r.ReadByte();   // flags
        r.ReadInt16();  // reserved
        long metaOffset = r.ReadInt64();
        int metaSize = r.ReadInt32();
        long idxOffset = r.ReadInt64();
        int idxSize = r.ReadInt32();

        // index -> 按 Id 定位真实数据偏移与压缩信息（新格式中 metadata.Position 实为 Zl2Entry.Id）
        var byId = new Dictionary<int, (long offset, ZlContainerCompression comp, int compSize)>();
        r.BaseStream.Seek(idxOffset, SeekOrigin.Begin);
        byte[] idx = r.ReadBytes(idxSize);
        using (var ir = new BinaryReader(new MemoryStream(idx, writable: false)))
        {
            int entryCount = ir.ReadInt32();
            for (int i = 0; i < entryCount; i++)
            {
                byte type = ir.ReadByte();
                int id = ir.ReadInt32();
                int uncomp = ir.ReadInt32();
                int comp = ir.ReadInt32();
                long off = ir.ReadInt64();
                byte compression = ir.ReadByte();
                byte codec = ir.ReadByte();
                if (type == 1 && !byId.ContainsKey(id))
                    byId[id] = (off, (ZlContainerCompression)compression, comp);
            }
        }

        // metadata -> Images（按 metadata 索引）
        r.BaseStream.Seek(metaOffset, SeekOrigin.Begin);
        byte[] meta = r.ReadBytes(metaSize);
        using (var mr = new BinaryReader(new MemoryStream(meta, writable: false)))
        {
            int mver = mr.ReadInt32();
            int mcount = mr.ReadInt32();
            mr.ReadInt32(); // atlasGroupImageCount
            mr.ReadInt32(); // atlasPageSize
            lib.Version = mver;
            lib.Images = new ZlImage[mcount];

            for (int i = 0; i < mcount; i++)
            {
                if (!mr.ReadBoolean()) continue;

                int pos = mr.ReadInt32();
                short w = mr.ReadInt16();
                short h = mr.ReadInt16();
                short ox = mr.ReadInt16();
                short oy = mr.ReadInt16();
                mr.ReadByte(); // shadowType
                mr.ReadInt16(); mr.ReadInt16(); // shadow w,h
                mr.ReadInt16(); mr.ReadInt16(); // shadow ox,oy
                mr.ReadInt16(); mr.ReadInt16(); // overlay w,h

                ZlCodec codec = ZlCodec.Dxt1;
                int stored = 0;
                if (mver >= 2)
                {
                    mr.ReadInt32(); // atlasPage
                    mr.ReadBytes(8); // sourceRect
                    mr.ReadBytes(8); // visibleBounds
                    codec = (ZlCodec)mr.ReadByte();
                    mr.ReadBytes(2); // shadowCodec, overlayCodec
                    mr.ReadBytes(3); // runtime preference
                    stored = mr.ReadInt32();   // StoredImageDataSize
                    mr.ReadBytes(32);          // 其余 8 个 i32（BC7/Fallback/Shadow/Overlay 尺寸字段）
                }

                byId.TryGetValue(pos, out var e);
                ZlImage img = new ZlImage
                {
                    Index = i,
                    Position = e.offset > 0 ? (int)e.offset : pos,
                    Width = w,
                    Height = h,
                    OffSetX = ox,
                    OffSetY = oy,
                    Codec = codec,
                    Compression = e.comp,
                    PayloadSize = e.compSize > 0 ? e.compSize : (stored > 0 ? stored : ZlImage.ComputeSize(w, h, codec)),
                };
                lib.Images[i] = img;
                lib.ValidCount++;
            }
        }

        return lib;
    }

    /// <summary>解码主图层，返回 RGBA32。payload 需从 Position 起、至少 PayloadSize 长。</summary>
    public byte[]? DecodeImage(ZlImage image, ReadOnlySpan<byte> payload)
    {
        if (image.PayloadSize <= 0 || payload.Length < image.PayloadSize)
            return null;

        ReadOnlySpan<byte> src = payload.Slice(0, image.PayloadSize);

        byte[] uncompressed;
        if (image.Compression != ZlContainerCompression.None)
        {
            byte[]? inflated = PngDecoder.InflateDeflate(src);
            if (inflated == null) return null;
            uncompressed = inflated;
        }
        else
        {
            uncompressed = src.ToArray();
        }

        switch (image.Codec)
        {
            case ZlCodec.Dxt1:
                if (image.Width <= 0 || image.Height <= 0) return null;
                return DxtDecoder.Decode(uncompressed, image.Width, image.Height, dxt1: true);
            case ZlCodec.Dxt5:
                if (image.Width <= 0 || image.Height <= 0) return null;
                return DxtDecoder.Decode(uncompressed, image.Width, image.Height, dxt1: false);
            case ZlCodec.Bgra32:
                return BgraToRgba(uncompressed);
            case ZlCodec.Png:
                byte[]? png = PngDecoder.Decode(uncompressed, out int w, out int h);
                if (png != null)
                {
                    image.Width = (short)w;
                    image.Height = (short)h;
                }
                return png;
            default:
                return null;
        }
    }

    private static byte[] BgraToRgba(ReadOnlySpan<byte> src)
    {
        byte[] dst = new byte[src.Length];
        for (int i = 0; i + 3 < src.Length; i += 4)
        {
            dst[i] = src[i + 2];
            dst[i + 1] = src[i + 1];
            dst[i + 2] = src[i];
            dst[i + 3] = src[i + 3];
        }
        return dst;
    }
}
