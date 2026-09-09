using System.Buffers.Binary;
using System.IO.Compression;

namespace KFramework.Content.Pipeline;

/// <summary>
/// 最小可用的 PNG 解码器：支持 8 位色深、非隔行的 灰度 / RGB / 调色板 / 灰度+Alpha / RGBA。
/// 不依赖任何图形库，可在打包工具与浏览器里共用。
/// </summary>
public static class PngDecoder
{
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static Bitmap Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length < 8 || !data[..8].SequenceEqual(Signature))
            throw new InvalidDataException("不是有效的 PNG 文件。");

        int width = 0, height = 0, colorType = 0, bitDepth = 0, interlace = 0;
        byte[]? palette = null;
        byte[]? transparency = null;

        using var idat = new MemoryStream();
        int offset = 8;

        while (offset + 8 <= data.Length)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset, 4));
            string type = System.Text.Encoding.ASCII.GetString(data.Slice(offset + 4, 4));
            ReadOnlySpan<byte> chunk = data.Slice(offset + 8, length);
            offset += 12 + length;

            switch (type)
            {
                case "IHDR":
                    width = BinaryPrimitives.ReadInt32BigEndian(chunk[..4]);
                    height = BinaryPrimitives.ReadInt32BigEndian(chunk.Slice(4, 4));
                    bitDepth = chunk[8];
                    colorType = chunk[9];
                    interlace = chunk[12];
                    break;
                case "PLTE":
                    palette = chunk.ToArray();
                    break;
                case "tRNS":
                    transparency = chunk.ToArray();
                    break;
                case "IDAT":
                    idat.Write(chunk);
                    break;
                case "IEND":
                    offset = data.Length;
                    break;
            }
        }

        if (width <= 0 || height <= 0) throw new InvalidDataException("PNG 尺寸无效。");
        if (bitDepth != 8) throw new NotSupportedException($"暂不支持 {bitDepth} 位色深的 PNG，请导出为 8 位。");
        if (interlace != 0) throw new NotSupportedException("暂不支持隔行扫描的 PNG。");

        int channels = ChannelsOf(colorType);
        byte[] raw = Decompress(idat.ToArray(), height * (width * channels + 1));
        byte[] pixels = Unfilter(raw, width, height, channels);
        return ConvertToRgba(pixels, width, height, colorType, palette, transparency);
    }

    private static int ChannelsOf(int colorType) => colorType switch
    {
        0 => 1,   // 灰度
        2 => 3,   // RGB
        3 => 1,   // 调色板索引
        4 => 2,   // 灰度 + Alpha
        6 => 4,   // RGBA
        _ => throw new NotSupportedException($"不支持的 PNG 颜色类型 {colorType}。"),
    };

    private static byte[] Decompress(byte[] compressed, int expectedLength)
    {
        var output = new byte[expectedLength];
        using var source = new MemoryStream(compressed, writable: false);
        using var zlib = new ZLibStream(source, CompressionMode.Decompress);
        int read = zlib.ReadAtLeast(output, expectedLength, throwOnEndOfStream: false);
        if (read != expectedLength) throw new InvalidDataException("PNG 数据不完整。");
        return output;
    }

    private static byte[] Unfilter(byte[] raw, int width, int height, int channels)
    {
        int stride = width * channels;
        var result = new byte[stride * height];

        for (int y = 0; y < height; y++)
        {
            int filter = raw[y * (stride + 1)];
            int source = y * (stride + 1) + 1;
            int destination = y * stride;
            int previous = destination - stride;

            for (int x = 0; x < stride; x++)
            {
                byte value = raw[source + x];
                byte a = x >= channels ? result[destination + x - channels] : (byte)0;
                byte b = y > 0 ? result[previous + x] : (byte)0;
                byte c = (x >= channels && y > 0) ? result[previous + x - channels] : (byte)0;

                result[destination + x] = filter switch
                {
                    0 => value,
                    1 => (byte)(value + a),
                    2 => (byte)(value + b),
                    3 => (byte)(value + (a + b) / 2),
                    4 => (byte)(value + Paeth(a, b, c)),
                    _ => throw new InvalidDataException($"未知的 PNG 过滤器 {filter}。"),
                };
            }
        }

        return result;
    }

    private static byte Paeth(byte a, byte b, byte c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static Bitmap ConvertToRgba(byte[] pixels, int width, int height, int colorType,
                                        byte[]? palette, byte[]? transparency)
    {
        var bitmap = new Bitmap(width, height);
        int index = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Rgba color = colorType switch
                {
                    0 => Gray(pixels[index++], transparency),
                    2 => new Rgba(pixels[index], pixels[index + 1], pixels[index + 2], 255),
                    3 => Palette(pixels[index++], palette, transparency),
                    4 => new Rgba(pixels[index], pixels[index + 1], pixels[index + 2], pixels[index + 3]),
                    6 => new Rgba(pixels[index], pixels[index + 1], pixels[index + 2], pixels[index + 3]),
                    _ => Rgba.Transparent,
                };

                if (colorType is 2) index += 3;
                else if (colorType is 4 or 6) index += 4;

                bitmap.SetPixel(x, y, color);
            }
        }

        return bitmap;

        static Rgba Gray(byte value, byte[]? trns)
            => new(value, value, value, trns is { Length: > 0 } ? trns[0] : (byte)255);

        static Rgba Palette(byte idx, byte[]? pal, byte[]? trns)
        {
            if (pal is null || idx * 3 + 2 >= pal.Length) return Rgba.Transparent;
            byte alpha = trns is not null && idx < trns.Length ? trns[idx] : (byte)255;
            return new Rgba(pal[idx * 3], pal[idx * 3 + 1], pal[idx * 3 + 2], alpha);
        }
    }
}
