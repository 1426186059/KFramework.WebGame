using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace KFramework.Content.Pipeline;

/// <summary>把 RGBA8 位图编码为 8 位 RGBA PNG，仅用于输出"发布 Content"里的人眼预览图。</summary>
public static class PngEncoder
{
    public static byte[] Encode(Bitmap bitmap)
    {
        using var output = new MemoryStream();

        output.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr[..4], bitmap.Width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.Slice(4, 4), bitmap.Height);
        ihdr[8] = 8;    // 色深
        ihdr[9] = 6;    // RGBA
        ihdr[10] = 0;
        ihdr[11] = 0;
        ihdr[12] = 0;
        WriteChunk(output, "IHDR", ihdr);

        byte[] scanlines = BuildScanlines(bitmap);
        WriteChunk(output, "IDAT", Deflate(scanlines));
        WriteChunk(output, "IEND", ReadOnlySpan<byte>.Empty);

        return output.ToArray();
    }

    private static byte[] BuildScanlines(Bitmap bitmap)
    {
        int stride = bitmap.Width * 4;
        var buffer = new byte[(stride + 1) * bitmap.Height];
        for (int y = 0; y < bitmap.Height; y++)
        {
            buffer[y * (stride + 1)] = 0;   // 过滤器：None
            Buffer.BlockCopy(bitmap.Pixels, y * stride, buffer, y * (stride + 1) + 1, stride);
        }
        return buffer;
    }

    private static byte[] Deflate(byte[] data)
    {
        using var output = new MemoryStream();
        output.WriteByte(0x78);   // zlib: CMF
        output.WriteByte(0x9C);   // zlib: FLG

        using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
            deflate.Write(data, 0, data.Length);

        uint adler = Adler32(data);
        Span<byte> tail = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(tail, adler);
        output.Write(tail);

        return output.ToArray();
    }

    private static uint Adler32(byte[] data)
    {
        uint a = 1, b = 0;
        foreach (byte value in data)
        {
            a = (a + value) % 65521;
            b = (b + a) % 65521;
        }
        return (b << 16) | a;
    }

    private static void WriteChunk(Stream stream, string type, ReadOnlySpan<byte> payload)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, payload.Length);
        stream.Write(length);
        stream.Write(Encoding.ASCII.GetBytes(type));
        stream.Write(payload);

        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(type, payload));
        stream.Write(crc);
    }

    private static uint Crc32(string type, ReadOnlySpan<byte> payload)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in Encoding.ASCII.GetBytes(type)) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        foreach (byte b in payload) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFF;
    }

    private static readonly uint[] CrcTable = CreateCrcTable();

    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }
}
