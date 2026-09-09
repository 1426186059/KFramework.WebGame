using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace KFramework.Content.Pak;

/// <summary>把若干资源写成一个 .pak 文件。</summary>
public sealed class PakWriter
{
    private struct PendingEntry
    {
        public ulong Id;
        public string Name;
        public int NameOffset;
        public AssetType Type;
        public CompressionMode Compression;
        public int BlobOffset;
        public int BlobSize;
        public int RawSize;
        public uint Checksum;
        public int Width;
        public int Height;
    }

    private readonly List<PendingEntry> _entries = new();
    private readonly MemoryStream _blobs = new();
    private readonly StringBuilder _stringTable = new();
    private readonly HashSet<ulong> _ids = new();

    /// <summary>添加一条资源。同名会覆盖前一条。</summary>
    public void Add(string name, AssetType type, ReadOnlySpan<byte> raw,
                    int width = 0, int height = 0, CompressionMode? compression = null)
    {
        string normalized = PakFormat.NormalizeName(name);
        ulong id = PakFormat.HashId(normalized);

        CompressionMode mode = compression ?? (raw.Length >= PakFormat.CompressionThreshold
            ? CompressionMode.Deflate
            : CompressionMode.None);

        byte[] payload = Compress(raw, mode);

        int nameOffset = _stringTable.Length;
        _stringTable.Append(normalized);

        // 已有同名：复用其 blob 位置前先记录，简单起见直接追加新条目并在写出时以最后一条为准
        _entries.Add(new PendingEntry
        {
            Id = id,
            Name = normalized,
            NameOffset = nameOffset,
            Type = type,
            Compression = mode,
            BlobOffset = (int)_blobs.Length,
            BlobSize = payload.Length,
            RawSize = raw.Length,
            Checksum = PakFormat.Checksum(raw),
            Width = width,
            Height = height,
        });
        _ids.Add(id);

        _blobs.Write(payload, 0, payload.Length);
    }

    /// <summary>添加一个 RGBA8 位图资源。</summary>
    public void AddTexture(string name, Pipeline.Bitmap bitmap, AssetType type = AssetType.Texture)
        => Add(name, type, bitmap.Pixels, bitmap.Width, bitmap.Height);

    public void AddText(string name, string text, AssetType type = AssetType.Text)
        => Add(name, type, Encoding.UTF8.GetBytes(text));

    private static byte[] Compress(ReadOnlySpan<byte> raw, CompressionMode mode)
    {
        if (mode == CompressionMode.None || raw.Length == 0) return raw.ToArray();

        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            zlib.Write(raw);
        return output.ToArray();
    }

    /// <summary>写出到流。条目按 ID 升序排列，便于读取端二分查找。</summary>
    public void SaveTo(Stream destination)
    {
        _entries.Sort(static (a, b) => a.Id.CompareTo(b.Id));

        byte[] stringTable = Encoding.UTF8.GetBytes(_stringTable.ToString());
        int entryTableSize = _entries.Count * PakFormat.EntrySize;

        int stringTableOffset = PakFormat.HeaderSize + entryTableSize;
        long blobOffset = stringTableOffset + stringTable.Length;

        Span<byte> header = stackalloc byte[PakFormat.HeaderSize];
        BinaryPrimitives.WriteUInt32LittleEndian(header[..4], PakFormat.Magic);
        BinaryPrimitives.WriteUInt16LittleEndian(header.Slice(4, 2), PakFormat.CurrentVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(header.Slice(6, 2), 0);
        BinaryPrimitives.WriteInt32LittleEndian(header.Slice(8, 4), _entries.Count);
        BinaryPrimitives.WriteInt32LittleEndian(header.Slice(12, 4), stringTableOffset);
        BinaryPrimitives.WriteInt32LittleEndian(header.Slice(16, 4), stringTable.Length);
        BinaryPrimitives.WriteInt64LittleEndian(header.Slice(20, 8), blobOffset);
        BinaryPrimitives.WriteInt32LittleEndian(header.Slice(28, 4), 0);

        destination.Write(header);

        Span<byte> entry = stackalloc byte[PakFormat.EntrySize];
        foreach (PendingEntry item in _entries)
        {
            entry.Clear();
            BinaryPrimitives.WriteUInt64LittleEndian(entry[..8], item.Id);
            BinaryPrimitives.WriteInt32LittleEndian(entry.Slice(8, 4), item.NameOffset);
            BinaryPrimitives.WriteUInt16LittleEndian(entry.Slice(12, 2), (ushort)item.Name.Length);
            entry[14] = (byte)item.Type;
            entry[15] = (byte)item.Compression;
            BinaryPrimitives.WriteInt32LittleEndian(entry.Slice(16, 4), item.BlobOffset);
            BinaryPrimitives.WriteInt32LittleEndian(entry.Slice(20, 4), item.BlobSize);
            BinaryPrimitives.WriteInt32LittleEndian(entry.Slice(24, 4), item.RawSize);
            BinaryPrimitives.WriteUInt32LittleEndian(entry.Slice(28, 4), item.Checksum);
            BinaryPrimitives.WriteUInt16LittleEndian(entry.Slice(32, 2), (ushort)item.Width);
            BinaryPrimitives.WriteUInt16LittleEndian(entry.Slice(34, 2), (ushort)item.Height);
            destination.Write(entry);
        }

        destination.Write(stringTable);
        _blobs.Position = 0;
        _blobs.CopyTo(destination);
    }
}
