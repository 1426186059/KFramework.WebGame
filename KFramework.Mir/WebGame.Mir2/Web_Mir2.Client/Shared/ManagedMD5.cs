// 自包含托管 MD5 实现（RFC 1321）。
// 浏览器 WASM 的 System.Security.Cryptography 底层走 SubtleCrypto，不支持 MD5，
// MD5.Create() 会抛 Cryptography_UnknownHashAlgorithm。此实现不依赖任何平台加密后端，
// 在桌面与浏览器下均可用，且与 OS 提供的 MD5 产生完全相同的哈希值。
public static class ManagedMD5
{
    private static readonly uint[] K =
    {
        0xd76aa478, 0xe8c7b756, 0x242070db, 0xc1bdceee,
        0xf57c0faf, 0x4787c62a, 0xa8304613, 0xfd469501,
        0x698098d8, 0x8b44f7af, 0xffff5bb1, 0x895cd7be,
        0x6b901122, 0xfd987193, 0xa679438e, 0x49b40821,
        0xf61e2562, 0xc040b340, 0x265e5a51, 0xe9b6c7aa,
        0xd62f105d, 0x02441453, 0xd8a1e681, 0xe7d3fbc8,
        0x21e1cde6, 0xc33707d6, 0xf4d50d87, 0x455a14ed,
        0xa9e3e905, 0xfcefa3f8, 0x676f02d9, 0x8d2a4c8a,
        0xfffa3942, 0x8771f681, 0x6d9d6122, 0xfde5380c,
        0xa4beea44, 0x4bdecfa9, 0xf6bb4b60, 0xbebfbc70,
        0x289b7ec6, 0xeaa127fa, 0xd4ef3085, 0x04881d05,
        0xd9d4d039, 0xe6db99e5, 0x1fa27cf8, 0xc4ac5665,
        0xf4292244, 0x432aff97, 0xab9423a7, 0xfc93a039,
        0x655b59c3, 0x8f0ccc92, 0xffeff47d, 0x85845dd1,
        0x6fa87e4f, 0xfe2ce6e0, 0xa3014314, 0x4e0811a1,
        0xf7537e82, 0xbd3af235, 0x2ad7d2bb, 0xeb86d391
    };

    private static readonly int[] S =
    {
        7, 12, 17, 22, 7, 12, 17, 22, 7, 12, 17, 22, 7, 12, 17, 22,
        5, 9, 14, 20, 5, 9, 14, 20, 5, 9, 14, 20, 5, 9, 14, 20,
        4, 11, 16, 23, 4, 11, 16, 23, 4, 11, 16, 23, 4, 11, 16, 23,
        6, 10, 15, 21, 6, 10, 15, 21, 6, 10, 15, 21, 6, 10, 15, 21
    };

    public static byte[] ComputeHash(byte[] input)
    {
        int padLen = (56 - (input.Length + 1) % 64 + 64) % 64;
        byte[] msg = new byte[input.Length + 1 + padLen + 8];
        System.Buffer.BlockCopy(input, 0, msg, 0, input.Length);
        msg[input.Length] = 0x80;

        ulong bitLen = (ulong)input.Length * 8;
        for (int i = 0; i < 8; i++)
            msg[msg.Length - 8 + i] = (byte)((bitLen >> (8 * i)) & 0xff);

        uint a0 = 0x67452301;
        uint b0 = 0xefcdab89;
        uint c0 = 0x98badcfe;
        uint d0 = 0x10325476;

        uint[] m = new uint[16];
        for (int chunk = 0; chunk < msg.Length; chunk += 64)
        {
            for (int i = 0; i < 16; i++)
                m[i] = msg[chunk + i * 4] | ((uint)msg[chunk + i * 4 + 1] << 8)
                       | ((uint)msg[chunk + i * 4 + 2] << 16) | ((uint)msg[chunk + i * 4 + 3] << 24);

            uint a = a0, b = b0, c = c0, d = d0;
            for (int i = 0; i < 64; i++)
            {
                uint f;
                int g;
                if (i < 16) { f = (b & c) | (~b & d); g = i; }
                else if (i < 32) { f = (d & b) | (~d & c); g = (5 * i + 1) % 16; }
                else if (i < 48) { f = b ^ c ^ d; g = (3 * i + 5) % 16; }
                else { f = c ^ (b | ~d); g = (7 * i) % 16; }

                f = f + a + K[i] + m[g];
                a = d;
                d = c;
                c = b;
                b = b + LeftRotate(f, S[i]);
            }

            a0 += a; b0 += b; c0 += c; d0 += d;
        }

        byte[] digest = new byte[16];
        WriteUInt(digest, 0, a0);
        WriteUInt(digest, 4, b0);
        WriteUInt(digest, 8, c0);
        WriteUInt(digest, 12, d0);
        return digest;
    }

    public static byte[] ComputeHash(System.IO.Stream stream)
    {
        using (var ms = new System.IO.MemoryStream())
        {
            stream.CopyTo(ms);
            return ComputeHash(ms.ToArray());
        }
    }

    private static uint LeftRotate(uint x, int c) => (x << c) | (x >> (32 - c));

    private static void WriteUInt(byte[] buf, int offset, uint value)
    {
        buf[offset] = (byte)(value & 0xff);
        buf[offset + 1] = (byte)((value >> 8) & 0xff);
        buf[offset + 2] = (byte)((value >> 16) & 0xff);
        buf[offset + 3] = (byte)((value >> 24) & 0xff);
    }
}
