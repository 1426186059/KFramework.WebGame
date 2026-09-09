using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace KFramework.Content.Pak;

/// <summary>只读的 .pak 解析器，浏览器端与工具端共用。</summary>
public sealed class PakReader
{
    private readonly byte[] _data;
    private readonly Dictionary<ulong, PakEntry> _entries;
    private readonly long _blobOffset;

    public int Version { get; }

    public int Count => _entries.Count;

    public IEnumerable<string> Names => _entries.Values.Select(static e => e.Name).OrderBy(static n => n, StringComparer.Ordinal);

    private PakReader(byte[] data)
    {
        _data = data;

        if (data.Length < PakFormat.HeaderSize)
            throw new InvalidDataException("pak 文件过短。");

        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0, 4));
        if (magic != PakFormat.Magic)
            throw new InvalidDataException("不是有效的 pak 文件（magic 不匹配）。");

        Version = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(4, 2));
        if (Version != PakFormat.CurrentVersion)
            throw new InvalidDataException($"不支持的 pak 版本 {Version}。");

        int entryCount = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(8, 4));
        int stringTableOffset = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(12, 4));
        int stringTableSize = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(16, 4));
        _blobOffset = BinaryPrimitives.ReadInt64LittleEndian(data.AsSpan(20, 8));

        _entries = new Dictionary<ulong, PakEntry>(entryCount);
        ReadOnlySpan<byte> stringTable = data.AsSpan(stringTableOffset, stringTableSize);

        for (int i = 0; i < entryCount; i++)
        {
            int off = PakFormat.HeaderSize + i * PakFormat.EntrySize;
            ReadOnlySpan<byte> slot = data.AsSpan(off, PakFormat.EntrySize);

            ulong id = BinaryPrimitives.ReadUInt64LittleEndian(slot[..8]);
            int nameOffset = BinaryPrimitives.ReadInt32LittleEndian(slot.Slice(8, 4));
            int nameLength = BinaryPrimitives.ReadUInt16LittleEndian(slot.Slice(12, 2));
            var type = (AssetType)slot[14];
            var compression = (CompressionMode)slot[15];
            int blobOffset = BinaryPrimitives.ReadInt32LittleEndian(slot.Slice(16, 4));
            int blobSize = BinaryPrimitives.ReadInt32LittleEndian(slot.Slice(20, 4));
            int rawSize = BinaryPrimitives.ReadInt32LittleEndian(slot.Slice(24, 4));
            uint checksum = BinaryPrimitives.ReadUInt32LittleEndian(slot.Slice(28, 4));
            int width = BinaryPrimitives.ReadUInt16LittleEndian(slot.Slice(32, 2));
            int height = BinaryPrimitives.ReadUInt16LittleEndian(slot.Slice(34, 2));

            string name = Encoding.UTF8.GetString(stringTable.Slice(nameOffset, nameLength));

            _entries[id] = new PakEntry(id, name, type, compression, blobOffset, blobSize, rawSize, checksum, width, height);
        }
    }

    public static PakReader FromBytes(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return new PakReader(data);
    }

    public bool Contains(string name) => _entries.ContainsKey(PakFormat.HashId(PakFormat.NormalizeName(name)));

    public bool TryGet(string name, out PakEntry entry)
        => _entries.TryGetValue(PakFormat.HashId(PakFormat.NormalizeName(name)), out entry);

    public PakEntry Get(string name)
        => TryGet(name, out PakEntry entry)
            ? entry
            : throw new KeyNotFoundException($"pak 中不存在资源 “{name}”。");

    /// <summary>读取并解压一条资源的原始数据。</summary>
    public byte[] Read(in PakEntry entry)
    {
        ReadOnlySpan<byte> blob = _data.AsSpan((int)(_blobOffset + entry.BlobOffset), entry.BlobSize);

        byte[] raw;
        if (entry.Compression == CompressionMode.Brotli)
        {
            raw = new byte[entry.RawSize];
            using var source = new MemoryStream(blob.ToArray(), writable: false);
            using var brotli = new BrotliStream(source, System.IO.Compression.CompressionMode.Decompress);
            int read = brotli.ReadAtLeast(raw, raw.Length, throwOnEndOfStream: false);
            if (read != entry.RawSize)
                throw new InvalidDataException($"资源 “{entry.Name}” 解压后长度不符。");
        }
        else
        {
            raw = blob.ToArray();
        }

        return raw;
    }

    public byte[] Read(string name) => Read(Get(name));

    /// <summary>读取文本/JSON 资源。</summary>
    public string ReadText(string name) => Encoding.UTF8.GetString(Read(name));

    /// <summary>列出包内所有条目（按资源名排序）。</summary>
    public IReadOnlyList<PakEntry> Entries
        => _entries.Values.OrderBy(static e => e.Name, StringComparer.Ordinal).ToArray();
}
