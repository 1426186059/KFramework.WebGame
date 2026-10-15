namespace Mir2ReverseWeb;

/// <summary>
/// 原始传奇（移动版）线上资源地址与解析参数集中配置。
/// 新增地图包 / 配置分片，直接往下面的数组里加 URL 即可，程序会自动下载并直接放入内存（不写本地磁盘）。
/// </summary>
public static class Mir2Config
{
    /// <summary>CDN 基址</summary>
    public const string CdnBase = "https://cdn.yscq-wy.yscq.com/fugu_20201203";

    /// <summary>资源版本号（缓存破坏参数）</summary>
    public const string ResourceVersion = "1.0.2";

    /// <summary>
    /// 地图数据包（.dat，zlib 压缩）。
    /// 一个包里可含数百~上千张地图；如有多包继续追加。
    /// </summary>
    public static readonly string[] MapDataUrls = new[]
    {
        $"{CdnBase}/map10.25341.1.dat?ver={ResourceVersion}",
    };

    /// <summary>
    /// 地图配置（含地图名 / 像素宽高），通常分片存储。
    /// 程序会自动合并所有分片并据此给每张地图补上名字与真实网格尺寸。
    /// </summary>
    public static readonly string[] MapConfigUrls = new[]
    {
        $"{CdnBase}/0config10.25341.1.json",
        $"{CdnBase}/1config10.25341.1.json",
        $"{CdnBase}/2config10.25341.1.json",
        $"{CdnBase}/3config10.25341.1.json",
    };

    /// <summary>
    /// 客户端主程序 JS（含真实的地图包加载 / 单元解码逻辑，可对照逆向）。
    /// 例：main10.25341.1.min.js 中的 readData：每个地图块前 4 字节丢弃，之后
    /// 4-bit/格（高 nibble 在前），bit3=可走 / bit2=遮挡 / bit1=安全区。
    /// </summary>
    public static readonly string[] MapJsUrls = new[]
    {
        $"{CdnBase}/main10.25341.1.min.js",
    };

    /// <summary>单元格像素尺寸（来自 JS 的 GameDefine.MAP_GRID_WIDTH / MAP_GRID_HEIGHT）</summary>
    public const int GridWidth = 48;
    public const int GridHeight = 32;

    /// <summary>本地资源目录（仅手动加载本地文件时使用；URL 下载默认不落盘）</summary>
    public const string CacheDir = "cache";
}
