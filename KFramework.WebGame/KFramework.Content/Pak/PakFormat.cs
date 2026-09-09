namespace KFramework.Content.Pak;

/// <summary>资源类型。</summary>
public enum AssetType : byte
{
    Unknown = 0,
    /// <summary>原始二进制。</summary>
    Bytes = 1,
    /// <summary>纯文本。</summary>
    Text = 2,
    /// <summary>JSON 文档（配置、关卡、图集描述）。</summary>
    Json = 3,
    /// <summary>RGBA8 像素数据。</summary>
    Texture = 4,
    /// <summary>图集页的 RGBA8 像素数据。</summary>
    Atlas = 5,
}

/// <summary>单条记录的压缩方式。</summary>
public enum CompressionMode : byte
{
    None = 0,
    /// <summary>
    /// ZLib（Deflate + 校验头）。
    /// 注意：不能用 Brotli —— .NET 的 BrotliStream 在 WebAssembly 运行时上会抛
    /// PlatformNotSupported，而运行时必须能解压，因此统一选 ZLib。
    /// </summary>
    Deflate = 1,
}

/// <summary>
/// KFPak 容器格式。
/// 布局：<c>[Header 32B][EntryTable][StringTable][Blob]</c>。
/// 设计取舍：索引表在文件头部且定长，便于二分/哈希定位；每条记录独立压缩，
/// 因此可以只解压需要的那一条，适合"几千张小图里只取一张"的 2D 游戏场景。
/// </summary>
public static class PakFormat
{
    /// <summary>"KFPK"</summary>
    public const uint Magic = 0x4B46504B;

    public const ushort CurrentVersion = 1;

    public const int HeaderSize = 32;
    public const int EntrySize = 48;

    /// <summary>小于该字节数的数据不压缩（压缩收益低于解压成本）。</summary>
    public const int CompressionThreshold = 256;

    /// <summary>资源名统一为小写、反斜杠转正斜杠，保证跨平台一致。</summary>
    public static string NormalizeName(string name)
        => name.Replace('\\', '/').Trim().ToLowerInvariant();

    /// <summary>FNV-1a 64 位哈希，把资源名映射为定长 ID。</summary>
    public static ulong HashId(string name)
    {
        const ulong offsetBasis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        ulong hash = offsetBasis;
        foreach (char c in name)
        {
            hash ^= c;
            hash *= prime;
        }
        // 0 保留给"无效"
        return hash == 0 ? 1UL : hash;
    }

    /// <summary>FNV-1a 32 位校验和，用于校验解压后的数据。</summary>
    public static uint Checksum(ReadOnlySpan<byte> data)
    {
        const uint offsetBasis = 2166136261u;
        const uint prime = 16777619u;

        uint hash = offsetBasis;
        foreach (byte b in data)
        {
            hash ^= b;
            hash *= prime;
        }
        return hash;
    }
}

/// <summary>包内一条记录的索引信息。</summary>
public readonly struct PakEntry
{
    public readonly ulong Id;
    public readonly string Name;
    public readonly AssetType Type;
    public readonly CompressionMode Compression;
    public readonly int BlobOffset;
    public readonly int BlobSize;
    public readonly int RawSize;
    public readonly uint Checksum;
    public readonly int Width;
    public readonly int Height;

    internal PakEntry(ulong id, string name, AssetType type, CompressionMode compression,
                      int blobOffset, int blobSize, int rawSize, uint checksum, int width, int height)
    {
        Id = id;
        Name = name;
        Type = type;
        Compression = compression;
        BlobOffset = blobOffset;
        BlobSize = blobSize;
        RawSize = rawSize;
        Checksum = checksum;
        Width = width;
        Height = height;
    }
}
