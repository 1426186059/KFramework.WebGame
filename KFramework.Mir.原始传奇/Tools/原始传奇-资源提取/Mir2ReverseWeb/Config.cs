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
    /// 配置 JSON 分片完整 URL 列表。游戏主程序 <c>ConfigManager</c> 实际加载 6 个分片：
    /// <c>0config</c> ~ <c>5config</c>（见 main10.25341.1.min.js 的 onLoadCfgComplete2 循环：
    /// totalCfgFileNum=6，每片按 ResUrl.Data + (i + "config" + resVersion) + ".json" + resver 拼接，
    /// 并合并进同一个配置对象）。各分片顶层每个键（map / global / item …）就是一张「配置表」，
    /// 分布在各分片里（如物品表就不在 0config，而在后续分片）。
    /// baseUrl / version 可覆盖（默认见 CdnBase / GameVersion）。
    /// </summary>
    public const int ConfigShardCount = 6;

    public static string[] ConfigShardUrls(string? baseUrl = null, string? version = null)
    {
        var root = (baseUrl ?? CdnBase).TrimEnd('/');
        var ver = version ?? GameVersion;
        var list = new List<string>();
        for (int i = 0; i < ConfigShardCount; i++)
            list.Add($"{root}/{i}config{ver}.json?v={ver}");
        return list.ToArray();
    }

    /// <summary>单分片 0config（地图表 map 所在分片，供地图加载使用）</summary>
    public static string ConfigUrl =>
        $"{CdnBase}/0config{GameVersion}.json?v={GameVersion}";

    /// <summary>地图加载用的配置分片：地图表只在 0config 中，故只需这一个分片。</summary>
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

    /// <summary>
    /// 资源版本映射表（原版 JS 的 cm.version 来源）：全量「资源名 -> 版本号」映射，
    /// 含所有角色/NPC/怪物动画精灵表条目（键形如 model/{模型号}/{sourceId}.png/.json，值如 "10.1393.1"）。
    /// 逆向自 main10.25341.1.min.js 的 CommonUtils.setGlobalConfigValue：先拉 resourceVersion.json，
    /// 再 posJsonComp 里把整份赋给 cm.version。完整动画 URL =
    ///   {CdnBase}/assets/resource/{版本号}/model/{模型号}/{sourceId}.png?ver={ResourceVersion}
    /// </summary>
    public static string ResourceVersionManifestUrl => $"{CdnBase}/resourceVersion.json";

    /// <summary>动画精灵表下载落地根目录（默认系统下载目录下的 Mir2Anim 子目录）</summary>
    public const string DownloadDir = "downloads";

    /// <summary>
    /// 模型帧资源基址（客户端运行时由 js_gameVars.resUrl / cdnServer 注入，并非 resourceVersion.json 所在主机）。
    /// 逆向自 main10.25341.1.min.js：帧 URL = resUrl + 版本号 + "/model/{模型号}/{sourceId}.png"，其中 resUrl 以
    /// "/resource/"（或 MiniGame/H5 构建含 "/assets/resource/"）结尾，defaultVersion 为空（无 ?ver 参数）。
    /// 默认 CdnBase+"/assets/resource" 与旧行为一致；若该地址下取帧 404（资源在另一 CDN 主机），用环境变量
    /// MIR2_FRAME_BASE 覆盖为真实 resUrl 基址（含 /resource 或 /assets/resource 段）。
    /// </summary>
    public static string FrameBase =>
        Environment.GetEnvironmentVariable("MIR2_FRAME_BASE") ?? (CdnBase + "/assets/resource");

    /// <summary>帧 URL 附加查询串（客户端 defaultVersion 为空，默认空串；个别 CDN 若需 ?ver= 再经 MIR2_FRAME_QUERY 设置）。</summary>
    public static string FrameQuery =>
        Environment.GetEnvironmentVariable("MIR2_FRAME_QUERY") ?? "";

    /// <summary>单元格像素尺寸（来自 JS 的 GameDefine.MAP_GRID_WIDTH / MAP_GRID_HEIGHT）</summary>
    public const int GridWidth = 48;
    public const int GridHeight = 32;

    /// <summary>本地资源目录（仅手动加载本地文件时使用；URL 下载默认不落盘）</summary>
    public const string CacheDir = "cache";
}
