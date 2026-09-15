namespace KFramework.MonoGame;

/// <summary>
/// 单个资源在 <see cref="AssetBundle"/> 包内的条目（写入包内 manifest.json）。
/// 对应 Unity 内部 AssetBundle 的清单条目；图集类资源额外携带 Page/X/Y 以便运行时切片。
/// </summary>
/// <param name="Path">包内相对路径（即资源名，如 myres/atlas/characters_0）</param>
/// <param name="Type">MIME / 资源类型（如 atlas / audio/wav / json / text）</param>
/// <param name="Bytes">字节长度</param>
/// <param name="Crc">ZIP CRC32（8 位十六进制），快速完整性校验</param>
/// <param name="Hash">内容哈希（小写十六进制，无算法前缀）；算法见总清单 Hash 字段</param>
/// <param name="Width">原始像素宽（仅 atlas / 纹理类资源有意义，用于直接上传 GPU）</param>
/// <param name="Height">原始像素高</param>
/// <param name="Page">所属图集页索引（≥0 表示该资源是某图集页上的子图，运行时据此切片；-1 表示独立纹理/数据）</param>
/// <param name="X">子图在图集页中的 X 偏移（Page≥0 时有效）</param>
/// <param name="Y">子图在图集页中的 Y 偏移</param>
public sealed record AssetBundleEntry(
    string Path,
    string Type,
    long Bytes,
    string Crc,
    string Hash,
    int Width = 0,
    int Height = 0,
    int Page = -1,
    int X = 0,
    int Y = 0);
