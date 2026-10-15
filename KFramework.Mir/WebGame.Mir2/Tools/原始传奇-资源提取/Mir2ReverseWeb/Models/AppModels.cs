namespace Mir2ReverseWeb.Models;

/// <summary>文件系统条目</summary>
public class FsEntry
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public bool IsDir { get; set; }
    public long Size { get; set; }
}

/// <summary>仅带路径的请求</summary>
public class PathReq
{
    public string Path { get; set; } = "";
}

/// <summary>扫描某目录下指定扩展名的文件</summary>
public class ScanReq
{
    public string Root { get; set; } = "";
    public List<string> Exts { get; set; } = new();
}

public class ScanResult
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public List<FsEntry> Files { get; set; } = new();
}

/// <summary>地图数据包(.dat)索引中的单条</summary>
public class MapEntry
{
    public int Id { get; set; }
    public int Len { get; set; }
    public double Kb { get; set; }
    /// <summary>地图名（来自 config，GBK 解码）</summary>
    public string Name { get; set; } = "";
    /// <summary>像素宽（来自 config），0 表示未知</summary>
    public int Width { get; set; }
    /// <summary>像素高（来自 config），0 表示未知</summary>
    public int Height { get; set; }
}

public class MapIndexResult
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public int Count { get; set; }
    public long TotalBytes { get; set; }
    /// <summary>加载后本地缓存的地图包路径（解码/导出时缺省使用）</summary>
    public string? SourceFile { get; set; }
    /// <summary>成功解析到的地图配置条目数</summary>
    public int ConfigCount { get; set; }
    public List<MapEntry> Entries { get; set; } = new();
    public List<string> Messages { get; set; } = new();
}

/// <summary>解码单张地图（nibble 单元格 → 可走性掩码）</summary>
public class MapDecodeRequest
{
    public string Path { get; set; } = "";
    public int Id { get; set; }
    /// <summary>像素宽（来自 config）。<=0 时后端自动用配置或按单元格数近方估算</summary>
    public int Width { get; set; }
    /// <summary>像素高（来自 config）。<=0 时后端自动用配置或按单元格数近方估算</summary>
    public int Height { get; set; }
    public string? Destination { get; set; }
}

public class MapDecodeResult
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public int Id { get; set; }
    public int Cells { get; set; }
    public int Walkable { get; set; }
    public int Cover { get; set; }
    public int Safe { get; set; }
    public string? PreviewPngBase64 { get; set; }
    public string? OutFile { get; set; }
}

/// <summary>批量导出多张地图为可走性 PNG</summary>
public class MapExportRequest
{
    public string Path { get; set; } = "";
    public List<int> Ids { get; set; } = new();
    public int Width { get; set; }
    public int Height { get; set; }
    public string Destination { get; set; } = "";
}

public class MapExportResult
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public int Exported { get; set; }
    public List<string> Files { get; set; } = new();
}

/// <summary>图集(WIL/WIX)文件信息</summary>
public class AtlasFile
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string Kind { get; set; } = "";
    public long Size { get; set; }
}

public class AtlasScanResult
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public List<AtlasFile> Files { get; set; } = new();
}

/// <summary>解析 WIL 头与图像索引</summary>
public class AtlasInfo
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public string? Title { get; set; }
    public int Version { get; set; }
    public int ImageCount { get; set; }
    public int ParsedImages { get; set; }
    public List<AtlasImage> Images { get; set; } = new();
}

public class AtlasImage
{
    public int Index { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int OffsetX { get; set; }
    public int OffsetY { get; set; }
    public bool HasData { get; set; }
}

public class FileInfoResult
{
    public bool Exists { get; set; }
    public string? Path { get; set; }
    public long Size { get; set; }
    public string? Kind { get; set; }
    public int Lines { get; set; }
    public string? Note { get; set; }
}
