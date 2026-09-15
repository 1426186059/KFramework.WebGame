using System.Security.Cryptography;

namespace KFramework.Content;

/// <summary>
/// 内容哈希唯一入口（对齐 WebLib.Hash）。当前固定使用 MD5（小写十六进制、无算法前缀）。
/// 切换算法只需改这里与 <see cref="Algorithm"/> 常量，打包端 / 清单 / 校验逻辑均无需变动。
/// 每个 AssetBundle 文件都带上这个哈希，运行时据此做精确的增量热更。
/// </summary>
public static class BundleHash
{
    /// <summary>算法名，写入总清单的 Hash 字段（无 "md5:" 之类前缀）。</summary>
    public const string Algorithm = "md5";

    /// <summary>对字节求内容哈希，返回小写十六进制字符串。</summary>
    public static string Hex(ReadOnlySpan<byte> data)
        => Convert.ToHexString(MD5.HashData(data)).ToLowerInvariant();

    /// <summary>取完整哈希的前 <paramref name="len"/> 位，用于包文件名（短哈希）。</summary>
    public static string Shorten(string fullHex, int len = 16)
        => fullHex.Length <= len ? fullHex : fullHex[..len];
}
