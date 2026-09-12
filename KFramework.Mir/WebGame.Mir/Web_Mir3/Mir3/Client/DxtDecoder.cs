namespace MirClient.Assets;

/// <summary>
/// BC1(DXT1) / BC3(DXT5) 解码器 -> RGBA32。
/// 移植自 .ai/zlprobe 验证版本，纯托管、零依赖，可在 browser-wasm 上运行。
/// </summary>
public static class DxtDecoder
{
    public static byte[] Decode(ReadOnlySpan<byte> data, int width, int height, bool dxt1)
    {
        byte[] output = new byte[width * height * 4];
        if (width <= 0 || height <= 0 || data.Length == 0)
            return output;

        int blockSize = dxt1 ? 8 : 16;
        int blocksX = (width + 3) / 4;
        int blocksY = (height + 3) / 4;

        Span<byte> block = stackalloc byte[16 * 4];

        for (int by = 0; by < blocksY; by++)
        {
            for (int bx = 0; bx < blocksX; bx++)
            {
                int off = (by * blocksX + bx) * blockSize;
                if (off + blockSize > data.Length)
                    goto done;

                if (dxt1)
                    DecodeBlockDxt1(data.Slice(off, 8), block);
                else
                    DecodeBlockDxt5(data.Slice(off, 16), block);

                int px = bx * 4;
                int py = by * 4;
                int copyW = Math.Min(4, width - px);
                int copyH = Math.Min(4, height - py);

                for (int y = 0; y < copyH; y++)
                {
                    int dstRow = ((py + y) * width + px) * 4;
                    int srcRow = y * 16;
                    for (int x = 0; x < copyW * 4; x++)
                        output[dstRow + x] = block[srcRow + x];
                }
            }
        }

    done:
        return output;
    }

    private static void DecodeBlockDxt1(ReadOnlySpan<byte> src, Span<byte> dst)
    {
        int c0 = src[0] | (src[1] << 8);
        int c1 = src[2] | (src[3] << 8);
        uint bits = (uint)(src[4] | (src[5] << 8) | (src[6] << 16) | (src[7] << 24));

        Rgb565(c0, out int r0, out int g0, out int b0);
        Rgb565(c1, out int r1, out int g1, out int b1);

        int r2, g2, b2, a2, r3, g3, b3, a3;
        if (c0 > c1)
        {
            r2 = (2 * r0 + r1) / 3; g2 = (2 * g0 + g1) / 3; b2 = (2 * b0 + b1) / 3; a2 = 255;
            r3 = (r0 + 2 * r1) / 3; g3 = (g0 + 2 * g1) / 3; b3 = (b0 + 2 * b1) / 3; a3 = 255;
        }
        else
        {
            r2 = (r0 + r1) / 2; g2 = (g0 + g1) / 2; b2 = (b0 + b1) / 2; a2 = 255;
            r3 = 0; g3 = 0; b3 = 0; a3 = 0;
        }

        for (int i = 0; i < 16; i++)
        {
            int code = (int)((bits >> (2 * i)) & 3);
            int r, g, b, a;
            switch (code)
            {
                case 0: r = r0; g = g0; b = b0; a = 255; break;
                case 1: r = r1; g = g1; b = b1; a = 255; break;
                case 2: r = r2; g = g2; b = b2; a = a2; break;
                default: r = r3; g = g3; b = b3; a = a3; break;
            }
            int o = i * 4;
            dst[o] = (byte)r; dst[o + 1] = (byte)g; dst[o + 2] = (byte)b; dst[o + 3] = (byte)a;
        }
    }

    private static void DecodeBlockDxt5(ReadOnlySpan<byte> src, Span<byte> dst)
    {
        int a0 = src[0];
        int a1 = src[1];
        ulong abits = 0;
        for (int i = 0; i < 6; i++)
            abits |= (ulong)src[2 + i] << (8 * i);

        Span<byte> alphas = stackalloc byte[8];
        alphas[0] = (byte)a0;
        alphas[1] = (byte)a1;
        if (a0 > a1)
        {
            for (int i = 1; i < 7; i++)
                alphas[i + 1] = (byte)(((7 - i) * a0 + i * a1) / 7);
        }
        else
        {
            for (int i = 1; i < 5; i++)
                alphas[i + 1] = (byte)(((5 - i) * a0 + i * a1) / 5);
            alphas[6] = 0;
            alphas[7] = 255;
        }

        int c0 = src[8] | (src[9] << 8);
        int c1 = src[10] | (src[11] << 8);
        uint bits = (uint)(src[12] | (src[13] << 8) | (src[14] << 16) | (src[15] << 24));

        Rgb565(c0, out int r0, out int g0, out int b0);
        Rgb565(c1, out int r1, out int g1, out int b1);

        for (int i = 0; i < 16; i++)
        {
            int code = (int)((bits >> (2 * i)) & 3);
            int r, g, b;
            switch (code)
            {
                case 0: r = r0; g = g0; b = b0; break;
                case 1: r = r1; g = g1; b = b1; break;
                case 2: r = (2 * r0 + r1) / 3; g = (2 * g0 + g1) / 3; b = (2 * b0 + b1) / 3; break;
                default: r = (r0 + 2 * r1) / 3; g = (g0 + 2 * g1) / 3; b = (b0 + 2 * b1) / 3; break;
            }
            int acode = (int)((abits >> (3 * i)) & 7);
            int o = i * 4;
            dst[o] = (byte)r; dst[o + 1] = (byte)g; dst[o + 2] = (byte)b; dst[o + 3] = alphas[acode];
        }
    }

    private static void Rgb565(int c, out int r, out int g, out int b)
    {
        r = ((c >> 11) & 0x1F) * 255 / 31;
        g = ((c >> 5) & 0x3F) * 255 / 63;
        b = (c & 0x1F) * 255 / 31;
    }
}
