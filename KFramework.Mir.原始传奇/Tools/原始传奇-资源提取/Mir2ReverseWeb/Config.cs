namespace Mir2ReverseWeb;

/// <summary>
/// 原始传奇（移动版）线上资源地址与解析参数集中配置。
/// 新增地图包 / 配置分片，直接往下面的数组里加 URL 即可，程序会自动下载并直接放入内存（不写本地磁盘）。
/// </summary>
public static class Mir2Config
{
    /// <summary>CDN 基址</summary>
    public const string CdnBase = "https://cdn.yscq-wy.yscq.com/fugu_20201203";

    /// <summary>资源版本号（缓存破坏参数，用于 .dat 瓦片包的 ?ver=）</summary>
    public const string ResourceVersion = "1.0.2";

    /// <summary>
    /// 游戏版本号（配置 / 地图主程序均用此号）：
    /// 主程序 main10.25341.1.min.js、配置 0config10.25341.1.json、地图包 map10.25341.1.dat 都用它。
    /// 来自游戏运行时注入的 js_gameVars.resVersion。
    /// </summary>
    public const string GameVersion = "10.25341.1";

    /// <summary>
    /// 地图数据包（.dat，zlib 压缩）。
    /// 一个包里可含数百~上千张地图；如有多包继续追加。
    /// </summary>
    public static readonly string[] MapDataUrls = new[]
    {
        $"{CdnBase}/map10.25341.1.dat?ver={ResourceVersion}",
    };

    /// <summary>
    /// 配置 JSON 完整 URL。拼法与游戏主程序 JS 的 onloadCfgAgain 完全一致：
    ///   ResUrl.Data + "0config" + resVersion + ".json" + resver
    /// 注意：配置只有一个分片 <c>0config&lt;version&gt;.json</c>，不存在 1config/2config/3config。
    /// 该文件顶层每个键（map / global / level / conditions …）就是一张「配置表」。
    /// </summary>
    public static string ConfigUrl =>
        $"{CdnBase}/0config{GameVersion}.json?v={GameVersion}";

    /// <summary>兼容旧调用：地图加载也只需这一个配置分片。</summary>
    public static string[] MapConfigUrls => new[] { ConfigUrl };

    /// <summary>
    /// 客户端主程序 JS（含真实的地图包加载 / 单元解码逻辑，可对照逆向）。
    /// 例：main10.25341.1.min.js 中的 readData：每个地图块前 4 字节丢弃，之后
    /// 4-bit/格（高 nibble 在前），bit3=可走 / bit2=遮挡 / bit1=安全区。
    /// </summary>
    public static readonly string[] MapJsUrls = new[]
    {
        $"{CdnBase}/main10.25341.1.min.js",
    };

    /// <summary>
    /// 资源清单 manifest（图集 + UI + 配置 总索引，1641 条）。
    /// 每条形如 { "逻辑路径key": { "v": "8位hex哈希", "s": 字节数 } }。
    /// 实际 CDN 文件 URL = {CdnBase}/resource/{v前2位}/{v}_{s}.{key扩展名}（见 URL.txt 加载规则）。
    /// </summary>
    public static readonly string ManifestUrl =
        $"{CdnBase}/allmanifest10.25341.1.json?v=20260922111844&qufu_version=229";

    /// <summary>manifest 资源下载落地根目录（相对程序运行目录）</summary>
    public const string DownloadDir = "downloads";

    /// <summary>单元格像素尺寸（来自 JS 的 GameDefine.MAP_GRID_WIDTH / MAP_GRID_HEIGHT）</summary>
    public const int GridWidth = 48;
    public const int GridHeight = 32;

    /// <summary>本地资源目录（仅手动加载本地文件时使用；URL 下载默认不落盘）</summary>
    public const string CacheDir = "cache";
}
