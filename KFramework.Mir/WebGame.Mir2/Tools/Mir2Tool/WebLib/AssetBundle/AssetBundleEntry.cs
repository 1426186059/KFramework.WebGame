namespace WebLib;

/// <summary>
/// 单个资源在 <see cref="AssetBundle"/> 包内的条目（写入包内 manifest.json）。
/// </summary>
/// <param name="Path">包内相对路径</param>
/// <param name="Type">MIME 类型</param>
/// <param name="Bytes">字节长度</param>
/// <param name="Crc">ZIP CRC32（8 位十六进制），快速完整性校验</param>
/// <param name="Hash">内容哈希（小写十六进制，无算法前缀）；算法见总清单的 Hash 字段</param>
public sealed record AssetBundleEntry(string Path, string Type, long Bytes, string Crc, string Hash);
