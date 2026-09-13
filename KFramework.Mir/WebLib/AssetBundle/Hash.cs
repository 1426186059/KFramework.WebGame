using System.Security.Cryptography;

namespace WebLib.AssetBundle;

/// <summary>
/// 内容哈希的唯一入口。当前固定使用 MD5（小写十六进制、无算法前缀）。
/// 将来要切换哈希算法，只改这里（<see cref="Hex"/> 的实现与 <see cref="Algorithm"/> 常量）即可，
/// 打包端、清单、校验逻辑都无需改动。
/// </summary>
public static class Hash
{
    /// <summary>算法名，写入总清单的 Hash 字段（无 "md5:" 之类前缀）。</summary>
    public const string Algorithm = "md5";

    /// <summary>对字节求内容哈希，返回小写十六进制字符串。</summary>
    public static string Hex(ReadOnlySpan<byte> data) =>
        Convert.ToHexString(MD5.HashData(data)).ToLowerInvariant();

    /// <summary>取完整哈希的前 <paramref name="len"/> 位，用于包文件名（短哈希）。</summary>
    public static string Shorten(string fullHex, int len = 8) =>
        fullHex.Length <= len ? fullHex : fullHex[..len];
}
