namespace TextureToWebP.Models
{
    public class TextureConfig
    {
        /// <summary>要转换的源文件夹（会遍历其所有子目录）</summary>
        public string SourceFolder { get; set; } = "";
        /// <summary>输出文件夹；留空 = 在源文件同目录生成 .webp</summary>
        public string OutputFolder { get; set; } = "";
        /// <summary>WebP 无损编码（默认 true）：像素 100% 还原，避免有损压缩的振铃/透明边失真</summary>
        public bool Lossless { get; set; } = true;
        /// <summary>WebP 质量/压缩力度 1~100；无损模式下表示压缩力度，有损模式下表示画质</summary>
        public int Quality { get; set; } = 90;
        /// <summary>是否包含所有子目录</summary>
        public bool Recursive { get; set; } = true;
        /// <summary>转换成功后删除源文件</summary>
        public bool DeleteSource { get; set; }
        /// <summary>已存在同名 .webp 时覆盖</summary>
        public bool Overwrite { get; set; } = true;
        /// <summary>要处理的文件扩展名，逗号分隔</summary>
        public string Extensions { get; set; } = ".png,.jpg,.jpeg,.bmp,.gif";
    }

    public class ScanResultDto
    {
        public bool Ok { get; set; }
        public string Message { get; set; } = "";
        public int Count { get; set; }
        public long TotalBytes { get; set; }
        public List<string> Files { get; set; } = new();
    }

    public class ConvertStatusDto
    {
        public bool Running { get; set; }
        public bool Cancelled { get; set; }
        public int Percent { get; set; }
        public string Message { get; set; } = "";
        public string Current { get; set; } = "";
        public int Done { get; set; }
        public int Total { get; set; }
        public long SourceBytes { get; set; }
        public long WebPBytes { get; set; }
        public string? Result { get; set; }
        public string? Error { get; set; }
        public List<string> Log { get; set; } = new();
        public bool LogTruncated { get; set; }
    }

    public class OpResultDto
    {
        public bool Ok { get; set; }
        public string Message { get; set; } = "";
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
        public List<FsEntry> Drives { get; set; } = new();
    }
}
