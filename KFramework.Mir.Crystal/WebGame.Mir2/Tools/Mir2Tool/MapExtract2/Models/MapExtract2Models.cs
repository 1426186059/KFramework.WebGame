namespace MapExtract2.Models
{
    /// <summary>MapExtract2 路径配置（与 MapExtract 页面一致，新增蒸馏相关参数）。</summary>
    public class MapExtract2Config
    {
        /// <summary>
        /// 客户端资源根目录（父目录）。给出这个目录后，下面的相对路径会自动探测：
        ///   MapDir        = {根}\Map
        ///   SourcePath    = {根}\Data\Map        （素材 Lib 所在；蒸馏时按 Data/Map/... 相对路径读取）
        ///   MinimapLibPath= {根}\Data\mmap.Lib   （小地图素材源，页面兼容保留，蒸馏未使用）
        ///   MirDBPath     = 向上几层查找 Server\Debug\Server.MirDB
        /// 蒸馏产物的根（AssetBundle 输出目录）由 DestinationPath 指定。
        /// </summary>
        public string ClientRootPath { get; set; } = @"D:\OpenSource\Crystal\Build\Client\Debug";

        /// <summary>.map 文件所在目录（探测：{根}\Map）</summary>
        public string MapDir { get; set; } = @"D:\OpenSource\Crystal\Build\Client\Debug\Map";

        /// <summary>素材 Lib 源目录（探测：{根}\Data\Map），仅用于展示与路径自检</summary>
        public string SourcePath { get; set; } = @"D:\OpenSource\Crystal\Build\Client\Debug\Data\Map";

        /// <summary>热更新产物目录（仅供参考/UI 展示，实际由 kfc 默认输出到 &lt;PackRootPath&gt;\hot_update_res）。
        /// kfc 的 outDir 默认即相对内容工程根的 "hot_update_res"，与 raw 子目录同级，所以打包工具默认就在
        /// &lt;PackRootPath&gt;/raw 的同级生成 &lt;PackRootPath&gt;/hot_update_res，无需在此配置绝对路径。
        /// 客户端从 &lt;baseUrl&gt;/hot_update_res 拉取 version.manifest 与 *.web.lib（见 WebGame.Mir2.MonoGame.Client 的 MirGame.cs / Shims/BrowserResource.cs）。</summary>
        public string DestinationPath { get; set; } =
            @"D:\OpenSource\KFramework.WebGame\KFramework.Mir\WebGame.Mir2\Mir2Res\hot_update_res";

        public string MinimapLibPath { get; set; } = @"D:\OpenSource\Crystal\Build\Client\Debug\Data\mmap.Lib";
        public string MirDBPath { get; set; } = @"D:\OpenSource\Crystal\Build\Server\Debug\Server.MirDB";
        public bool RecursiveScan { get; set; }

        /// <summary>门动画安全帧数（放宽门索引区间，避免缺帧）</summary>
        public int DoorFrameSafety { get; set; } = 8;

        /// <summary>最多处理地图数（0 = 全部）。调试用，避免一次蒸馏太多</summary>
        public int MaxMaps { get; set; }

        /// <summary>把 .map 本身也打进 AssetBundle（客户端按需取用）</summary>
        public bool WriteRawMap { get; set; } = true;

        /// <summary>未用到的图保留为 17 字节零占位（保持索引表对齐，客户端零改动）</summary>
        public bool IncludeUnusedAsPlaceholder { get; set; } = true;

        /// <summary>
        /// kfc (KFramework.Content.Cli) 可执行文件(.exe) 或 .csproj 路径。
        /// 提取时先蒸馏 Lib 到临时内容工程，再调用 kfc 打包成 AssetBundle（输出 version.manifest 清单）。
        /// 若指向 .csproj，则用 `dotnet run --project` 启动；若指向 .exe，则直接运行。
        /// </summary>
        public string KfcPath { get; set; } =
            @"D:\OpenSource\KFramework.WebGame\KFramework.WebGame\KFramework.Content.Cli\KFramework.Content.Cli.csproj";

        /// <summary>
        /// 打包根目录（即 kfc 的 --root）。蒸馏出的文件写入 &lt;PackRootPath&gt;/raw/&lt;地图名&gt;/Data/Map/...，
        /// kfc 读取该目录下的 build.config.json 进行打包。默认指向 WebGame.Mir2\Mir2Res（稳定目录，便于检查/手动重打包）。
        /// 留空则回退到系统临时目录（用后删除）。
        /// </summary>
        public string PackRootPath { get; set; } =
            @"D:\OpenSource\KFramework.WebGame\KFramework.Mir\WebGame.Mir2\Mir2Res";
    }

    /// <summary>扫描到的一张地图（含 MirDB 匹配信息）。</summary>
    public class MapItemDto
    {
        public int Index { get; set; }
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public string Format { get; set; } = "?";
        public int TypeId { get; set; } = -1;
        public string TypeDescription { get; set; } = "";
        public int MiniMap { get; set; } = -1;
        public int BigMap { get; set; } = -1;
        public string Title { get; set; } = "";
        public bool InMirDB { get; set; }
    }

    public class ScanResultDto
    {
        public bool Ok { get; set; }
        public string Message { get; set; } = "";
        public int Total { get; set; }
        public bool MirDBLoaded { get; set; }
        public int MirDBCount { get; set; }
        public List<string> FormatSummary { get; set; } = new();
        public List<MapItemDto> Maps { get; set; } = new();
    }

    public class MirDbResultDto
    {
        public bool Ok { get; set; }
        public string Message { get; set; } = "";
        public int Count { get; set; }
    }

    public class ExtractRequest
    {
        public List<string> Maps { get; set; } = new();
    }

    public class ExtractResultDto
    {
        public bool Ok { get; set; }
        public string Message { get; set; } = "";
        public int Success { get; set; }
        public int Fail { get; set; }
        public List<string> Failed { get; set; } = new();
        public string DestinationPath { get; set; } = "";
        public List<string> Log { get; set; } = new();
        public bool LogTruncated { get; set; }
    }

    /// <summary>客户端资源根目录 → 相对路径自动探测结果。</summary>
    public class DetectResultDto
    {
        public bool Ok { get; set; }
        public string Message { get; set; } = "";
        public string ClientRootPath { get; set; } = "";
        public string MapDir { get; set; } = "";
        public bool MapDirFound { get; set; }
        public string SourcePath { get; set; } = "";
        public bool SourcePathFound { get; set; }
        public string MinimapLibPath { get; set; } = "";
        public bool MinimapLibFound { get; set; }
        public string MinimapDir { get; set; } = "";
        public bool MinimapReady { get; set; }
        public string MirDBPath { get; set; } = "";
        public bool MirDBFound { get; set; }
    }

    public class PathCheckDto
    {
        public bool MapDirExists { get; set; }
        public bool SourceDirExists { get; set; }
        public bool TilesExists { get; set; }
        public bool SmTilesExists { get; set; }
        public bool ObjectsExists { get; set; }
        public bool MinimapExists { get; set; }
        public bool MinimapLibExists { get; set; }
        public bool MirDBExists { get; set; }
    }

    /// <summary>单张地图蒸馏产物的信息（供页面「产物信息」卡片展示）。</summary>
    public class MapResultDto
    {
        public string Map { get; set; } = "";
        public string OutputDir { get; set; } = "";
        public bool Exists { get; set; }
        public bool Manifest { get; set; }
        public int LibCount { get; set; }
        public int ImageCount { get; set; }
        public string Skipped { get; set; } = "";
        public List<BundleInfo> Bundles { get; set; } = new();
    }

    public class BundleInfo
    {
        public string Name { get; set; } = "";
        public long Size { get; set; }
    }

    public class FsEntry
    {
        public string Name { get; set; } = "";
        public string FullPath { get; set; } = "";
    }

    public class FsListDto
    {
        public string Path { get; set; } = "";
        public string Parent { get; set; } = "";
        public List<FsEntry> Directories { get; set; } = new();
        public List<FsEntry> Files { get; set; } = new();
        public List<FsEntry> Drives { get; set; } = new();
    }
}
