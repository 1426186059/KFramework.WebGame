namespace MapExtract.Models
{
    /// <summary>从「客户端资源根目录」自动探测出来的相对路径结果</summary>
    public class DetectResultDto
    {
        public bool Ok { get; set; }
        public string Message { get; set; } = "";

        public string ClientRootPath { get; set; } = "";
        public string MapDir { get; set; } = "";
        public string SourcePath { get; set; } = "";
        /// <summary>小地图素材源 .Lib 文件（{根}\Data\mmap.Lib）</summary>
        public string MinimapLibPath { get; set; } = "";
        /// <summary>由 Lib 推导出的小地图 PNG 目录（{根}\Data\mmap）</summary>
        public string MinimapDir { get; set; } = "";
        public string MirDBPath { get; set; } = "";

        public bool MapDirFound { get; set; }
        public bool SourcePathFound { get; set; }
        /// <summary>小地图素材源 .Lib 已找到</summary>
        public bool MinimapLibFound { get; set; }
        /// <summary>由 Lib 推导的目录 {根}\Data\mmap 下已有 PNG（可选复用，非必需）</summary>
        public bool MinimapReady { get; set; }
        public bool MirDBFound { get; set; }
    }
}
