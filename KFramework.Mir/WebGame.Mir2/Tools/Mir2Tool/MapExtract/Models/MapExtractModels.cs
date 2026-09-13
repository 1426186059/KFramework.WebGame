namespace MapExtract.Models
{
    /// <summary>地图工具路径配置（对应 Unity 窗口顶部的路径配置区）。</summary>
    public class MapExtractConfig
    {
        /// <summary>
        /// 客户端资源根目录（父目录）。给出这个目录后，下面的相对路径会自动探测：
        ///   MapDir        = {根}\Map
        ///   SourcePath    = {根}\Data\Map        （素材 Lib 所在，需先解压为 PNG）
        ///   MinimapLibPath= {根}\Data\mmap.Lib   （小地图素材源，只读；按需抽到 目标路径\{地图}\MMap\）
        ///   MirDBPath     = 向上几层查找 Server\Debug\Server.MirDB
        /// </summary>
        public string ClientRootPath { get; set; } = @"D:\OpenSource\Crystal\Build\Client\Debug";

        public string MapDir { get; set; } = @"D:\OpenSource\Crystal\Build\Client\Debug\Map";
        public string SourcePath { get; set; } = @"D:\OpenSource\Crystal\Build\Client\Debug\Data\Map";
        public string DestinationPath { get; set; } = @"D:\OpenSource\2026Map";
        /// <summary>
        /// 小地图素材源 .Lib（整个客户端只有这一个：{根}\Data\mmap.Lib），可自动探测。
        /// 只读；按需抽出的图直接落到「目标路径」，不回写客户端素材目录。
        /// </summary>
        public string MinimapLibPath { get; set; } = @"D:\OpenSource\Crystal\Build\Client\Debug\Data\mmap.Lib";
        public string MirDBPath { get; set; } = @"D:\OpenSource\Crystal\Build\Server\Debug\Server.MirDB";
        public bool RecursiveScan { get; set; }

        /// <summary>导出地图时，顺手把产出的 PNG 也转成 WebP（网页游戏用）</summary>
        public bool ExportWebP { get; set; } = true;
        /// <summary>WebP 无损编码（默认 true）：像素 100% 还原，避免有损压缩的振铃/透明边失真</summary>
        public bool WebPLossless { get; set; } = true;
        /// <summary>WebP 质量/压缩力度 1~100；无损模式下表示压缩力度，有损模式下表示画质</summary>
        public int WebPQuality { get; set; } = 90;
        /// <summary>转成 WebP 后删除 PNG（更省磁盘；关闭后预览仍走 PNG）</summary>
        public bool DeletePngAfterWebP { get; set; }
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
