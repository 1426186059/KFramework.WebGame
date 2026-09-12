namespace MapTool.Models
{
    /// <summary>地图工具路径配置（对应 Unity 窗口顶部的路径配置区）。</summary>
    public class MapToolConfig
    {
        public string MapDir { get; set; } = @"D:\OpenSource\Crystal\Build\Client\Debug\Map";
        public string SourcePath { get; set; } = @"D:\OpenSource\Mir2Res\Data\Map";
        public string DestinationPath { get; set; } = @"D:\OpenSource\2026Map";
        public string MinimapPath { get; set; } = @"D:\OpenSource\Mir2Res\Data\mmap";
        public string MirDBPath { get; set; } = @"D:\OpenSource\Mir2_Unity_2026\Mir2Server\Build\Server\Debug\Server.MirDB";
        public bool RecursiveScan { get; set; }
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
