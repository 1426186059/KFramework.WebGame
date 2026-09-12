namespace LibraryExtract.Models
{
    /// <summary>Lib 导出配置（对应 WinForms 面板上的 Lib 路径 / 输出文件夹）</summary>
    public class LibraryExtractConfig
    {
        public string LibPath { get; set; } = "";
        /// <summary>true=文件夹（递归扫描 *.Lib），false=单个 .Lib 文件</summary>
        public bool LibIsFolder { get; set; } = true;
        public string OutputFolder { get; set; } = "";
    }

    /// <summary>转换进度快照（供前端轮询）</summary>
    public class ConvertStatusDto
    {
        public bool Running { get; set; }
        public bool CancelRequested { get; set; }
        public bool Cancelled { get; set; }
        public int Percent { get; set; }
        public string Message { get; set; } = "";
        public int TotalFiles { get; set; }
        public int CompletedFiles { get; set; }
        public int TotalImages { get; set; }
        public string? Result { get; set; }
        public string? Error { get; set; }
        public List<string> Log { get; set; } = new();
        public bool LogTruncated { get; set; }
    }

    public class StartResultDto
    {
        public bool Ok { get; set; }
        public string Message { get; set; } = "";
    }

    public class ScanResultDto
    {
        public bool Ok { get; set; }
        public string Message { get; set; } = "";
        public int Count { get; set; }
        public List<string> Files { get; set; } = new();
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
