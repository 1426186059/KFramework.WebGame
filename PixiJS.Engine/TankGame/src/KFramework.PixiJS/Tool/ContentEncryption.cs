using System;
using System.Text;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 轻量混淆：XOR 逐字节变换 + Base64 输出。
    /// 仅用于防止内容被肉眼直接看到，密钥随程序发布，并非安全加密。
    /// </summary>
    public static class ContentEncryption
    {
        // 固定混淆密钥（随包发布，知道即可逆向，仅防一般用户）
        private static readonly byte[] Key = {
            0x3C, 0x7A, 0x91, 0x55, 0xE2, 0x14, 0x6B, 0x09,
            0xA7, 0x42, 0xC3, 0x5F, 0x18, 0xD9, 0x70, 0x2E
        };

        public static string Encode(string data)
        {
            byte[] raw = Encoding.UTF8.GetBytes(data);
            byte[] xored = Xor(raw);
            return Convert.ToBase64String(xored);
        }

        public static string Decode(string data)
        {
            byte[] xored = Convert.FromBase64String(data);
            byte[] raw = Xor(xored);
            return Encoding.UTF8.GetString(raw);
        }

        private static byte[] Xor(byte[] data)
        {
            byte[] result = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                result[i] = (byte)(data[i] ^ Key[i % Key.Length]);
            }
            return result;
        }
    }
}