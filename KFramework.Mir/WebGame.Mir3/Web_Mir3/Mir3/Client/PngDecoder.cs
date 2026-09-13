using System.IO;
using System.IO.Compression;

namespace MirClient.Assets;

/// <summary>
/// PNG 解码器（纯托管，可在 browser-wasm 运行）：
/// 1) InflateDeflate：解 ZL2 容器外层的 raw-deflate（RFC1951，无 zlib 头）。
/// 2) Decode：解析 PNG 字节 -> RGBA32（与 DxtDecoder 输出顺序一致：r,g,b,a）。
/// 注意 PNG 的 IDAT 内部是 zlib 包装（RFC1950），用 ZLibStream 解，与外层不同。
/// </summary>
internal static class PngDecoder
{
    private static readonly byte[] PngSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };

    /// <summary>解 raw-deflate（RFC1951，无 zlib 头）。用于 ZL2 外层压缩。</summary>
    public static byte[]? InflateDeflate(ReadOnlySpan<byte> deflate)
    {
        try
        {
            using var input = new MemoryStream(deflate.ToArray());
            using var ds = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            ds.CopyTo(output);
            return output.ToArray();
        }
        catch
        {
            return null;
        }
    }

    public static byte[]? Decode(byte[] data, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (data == null || data.Length < 8) return null;
        for (int i = 0; i < 8; i++)
            if (data[i] != PngSignature[i]) return null;

        int bitDepth = 8, colorType = 6;
        byte[]? idat = null;
        byte[]? palette = null;

        int pos = 8;
        while (pos + 8 <= data.Length)
        {
            int len = (data[pos] << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3];
            string type = System.Text.Encoding.ASCII.GetString(data, pos + 4, 4);
            int dataPos = pos + 8;
            if (len < 0 || dataPos + len + 4 > data.Length) break;

            if (type == "IHDR")
            {
                width = (data[dataPos] << 24) | (data[dataPos + 1] << 16) | (data[dataPos + 2] << 8) | data[dataPos + 3];
                height = (data[dataPos + 4] << 24) | (data[dataPos + 5] << 16) | (data[dataPos + 6] << 8) | data[dataPos + 7];
                bitDepth = data[dataPos + 8];
                colorType = data[dataPos + 9];
            }
            else if (type == "PLTE")
            {
                palette = new byte[len];
                Buffer.BlockCopy(data, dataPos, palette, 0, len);
            }
            else if (type == "IDAT")
            {
                byte[] chunk = new byte[len];
                Buffer.BlockCopy(data, dataPos, chunk, 0, len);
                idat = idat == null ? chunk : Concat(idat, chunk);
            }
            else if (type == "IEND")
            {
                break;
            }

            pos = dataPos + len + 4; // 跳过 crc
        }

        if (width <= 0 || height <= 0 || idat == null) return null;
        if (bitDepth != 8) return null; // 仅支持 8-bit

        byte[]? raw = InflateZlib(idat);
        if (raw == null) return null;

        return Unfilter(raw, width, height, colorType, palette);
    }

    private static byte[]? InflateZlib(byte[] zlib)
    {
        try
        {
            using var input = new MemoryStream(zlib);
            using var zs = new ZLibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            zs.CopyTo(output);
            return output.ToArray();
        }
        catch
        {
            return null;
        }
    }

    private static byte[] Concat(byte[] a, byte[] b)
    {
        byte[] r = new byte[a.Length + b.Length];
        Buffer.BlockCopy(a, 0, r, 0, a.Length);
        Buffer.BlockCopy(b, 0, r, a.Length, b.Length);
        return r;
    }

    private static byte[]? Unfilter(byte[] raw, int w, int h, int colorType, byte[]? palette)
    {
        int channels = colorType switch
        {
            0 => 1, // Gray
            2 => 3, // RGB
            3 => 1, // Palette
            4 => 2, // Gray+Alpha
            6 => 4, // RGBA
            _ => -1
        };
        if (channels < 0) return null;

        int bpp = channels; // bitDepth == 8
        int stride = w * bpp;
        byte[] result = new byte[w * h * 4];

        byte[] cur = new byte[stride];
        byte[] prev = new byte[stride];

        for (int y = 0; y < h; y++)
        {
            int lineStart = y * (stride + 1);
            int filter = raw[lineStart];
            int dataStart = lineStart + 1;

            for (int i = 0; i < stride; i++)
            {
                int x = raw[dataStart + i];
                int a = i >= bpp ? cur[i - bpp] : 0;
                int b = prev[i];
                int c = i >= bpp ? prev[i - bpp] : 0;

                int recon = filter switch
                {
                    0 => x,
                    1 => x + a,
                    2 => x + b,
                    3 => x + ((a + b) >> 1),
                    4 => x + Paeth(a, b, c),
                    _ => x
                };
                cur[i] = (byte)(recon & 0xFF);
            }

            WriteRow(result, y, w, colorType, cur, bpp, palette);
            (prev, cur) = (cur, prev);
        }

        return result;
    }

    private static void WriteRow(byte[] result, int y, int w, int colorType, byte[] cur, int bpp, byte[]? palette)
    {
        for (int x = 0; x < w; x++)
        {
            int s = x * bpp;
            int d = (y * w + x) * 4;
            switch (colorType)
            {
                case 6:
                    result[d] = cur[s]; result[d + 1] = cur[s + 1]; result[d + 2] = cur[s + 2]; result[d + 3] = cur[s + 3];
                    break;
                case 2:
                    result[d] = cur[s]; result[d + 1] = cur[s + 1]; result[d + 2] = cur[s + 2]; result[d + 3] = 255;
                    break;
                case 0:
                    result[d] = cur[s]; result[d + 1] = cur[s]; result[d + 2] = cur[s]; result[d + 3] = 255;
                    break;
                case 4:
                    result[d] = cur[s]; result[d + 1] = cur[s]; result[d + 2] = cur[s]; result[d + 3] = cur[s + 1];
                    break;
                case 3:
                    int idx = cur[s];
                    if (palette != null && idx * 3 + 2 < palette.Length)
                    {
                        result[d] = palette[idx * 3]; result[d + 1] = palette[idx * 3 + 1]; result[d + 2] = palette[idx * 3 + 2]; result[d + 3] = 255;
                    }
                    else
                    {
                        result[d] = result[d + 1] = result[d + 2] = 0; result[d + 3] = 255;
                    }
                    break;
            }
        }
    }

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a);
        int pb = Math.Abs(p - b);
        int pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc) return a;
        if (pb <= pc) return b;
        return c;
    }
}
