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
    /// <summary>图像资源 key（来自 config 的 img 字段）。真实地图瓦片直链使用：zipmap/{Img}/{index}.jpg</summary>
    public string Img { get; set; } = "";
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
    /// <summary>本次加载用到的资源 URL（配置 / JS），供页面展示</summary>
    public List<string> MapConfigUrls { get; set; } = new();
    public List<string> MapJsUrls { get; set; } = new();
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
    /// <summary>导出时是否同时写出红绿可走性掩码 PNG（默认 true）。统一“提取选中地图资源”时设为 false，只产 JSON。</summary>
    public bool WritePng { get; set; } = true;
}

public class MapDecodeResult
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public int Id { get; set; }
    public int Cells { get; set; }
    /// <summary>由数据块长度推算的真实单元格总数（每个 4 bit）</summary>
    public int RawCells { get; set; }
    /// <summary>由配置宽高推算的网格列/行</summary>
    public int Cols { get; set; }
    public int Rows { get; set; }
    /// <summary>配置格数（Cols*Rows），用于与 RawCells 比对；0 表示无配置</summary>
    public int ConfigCells { get; set; }
    public int Walkable { get; set; }
    public int Cover { get; set; }
    public int Safe { get; set; }
    /// <summary>非致命提示（如配置与数据不符时自动重算网格）</summary>
    public string? Warn { get; set; }
    public string? PreviewPngBase64 { get; set; }
    public string? OutFile { get; set; }
    /// <summary>导出模式下同时写出的结构化地图数据(JSON)路径</summary>
    public string? OutDataFile { get; set; }
}

/// <summary>批量导出多张地图为可走性 PNG</summary>
public class MapExportRequest
{
    public string Path { get; set; } = "";
    public List<int> Ids { get; set; } = new();
    public int Width { get; set; }
    public int Height { get; set; }
    public string Destination { get; set; } = "";
    /// <summary>导出时是否同时写出红绿可走性掩码 PNG（默认 true）。统一“提取选中地图资源”时设为 false，只产 JSON。</summary>
    public bool WritePng { get; set; } = true;
}

public class MapExportResult
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public int Exported { get; set; }
    public List<string> Files { get; set; } = new();
    public List<string> DataFiles { get; set; } = new();
}

/// <summary>加载地图数据（可选自定义 map.data URL 覆盖 Config）</summary>
public class MapLoadRequest
{
    public string? DatUrl { get; set; }
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

/// <summary>manifest 条目：逻辑路径 key + 哈希(v) + 字节数(s) + 派生下载 URL</summary>
public class ManifestEntry
{
    public string Key { get; set; } = "";
    public string Hash { get; set; } = "";
    public long Size { get; set; }
    public string Ext { get; set; } = "";
    public string Url { get; set; } = "";
}

public class ManifestResult
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public string? ManifestUrl { get; set; }
    public int Count { get; set; }
    public List<ManifestEntry> Entries { get; set; } = new();
    public List<string> Messages { get; set; } = new();
}

public class AtlasDownloadRequest
{
    public string Key { get; set; } = "";
    /// <summary>输出目录；留空则默认系统下载目录</summary>
    public string? OutDir { get; set; }
}

public class AtlasDownloadResult
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public string? Key { get; set; }
    public string? Url { get; set; }
    public string? LocalPath { get; set; }
    public long Bytes { get; set; }
    public bool Skipped { get; set; }
}

public class AtlasDownloadAllResult
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public int Total { get; set; }
    public int Downloaded { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public long TotalBytes { get; set; }
    public List<string> Messages { get; set; } = new();
}

// ---------------- 单地图资源提取（相对路径平铺，与图集提取一致的 CDN 相对路径） ----------------
public class MapResourceRequest
{
    /// <summary>地图 ID（对应配置表 map 的键）</summary>
    public int MapId { get; set; }
    /// <summary>输出根目录；留空则默认系统下载目录。文件按 CDN 相对路径平铺（assets/resource/zipmap/...）</summary>
    public string? OutDir { get; set; }
    /// <summary>逻辑地图数据服务器根（.bin/.dat/.csv 所在）；留空默认 CdnBase（与图片 CDN 同根）。</summary>
    public string? DataUrl { get; set; }
    /// <summary>可选：直接给出 img（省去拉配置表）。不填则由配置表推导。</summary>
    public int? Img { get; set; }
    /// <summary>可选：直接给出像素宽/高（省去拉配置表）。两者都>0 时使用，否则回退配置表。</summary>
    public int? Width { get; set; }
    public int? Height { get; set; }
}

public class MapResourceResult
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public int MapId { get; set; }
    public int Img { get; set; }
    public int Data { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int Cols { get; set; }
    public int Rows { get; set; }
    public int TileCount { get; set; }
    public int TilesDownloaded { get; set; }
    public int TilesSkipped { get; set; }
    public int TilesFailed { get; set; }
    public string? MinimapLocal { get; set; }
    public bool MinimapOk { get; set; }
    public List<string> DataFiles { get; set; } = new();
    public List<string> Messages { get; set; } = new();
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

// ---------------- 真实地图瓦片下载（zipmap/{img}/{index}.jpg） ----------------
public class MapTileDownloadRequest
{
    /// <summary>要下载的地图（含 Id/Img/Name/Width/Height）</summary>
    public List<MapEntry> Maps { get; set; } = new();
    /// <summary>输出根目录；留空则默认系统下载目录</summary>
    public string Destination { get; set; } = "";
    /// <summary>下载完成后是否拼接一张 ≤4096 的总览 PNG（瓦片过多时自动跳过）</summary>
    public bool Stitch { get; set; } = true;
}

// ---------------- 小地图提取（minimaps.png + minimaps.json 图集） ----------------
public class MapMinimapRequest
{
    /// <summary>要提取小地图的地图（含 Id/Img/Name）</summary>
    public List<MapEntry> Maps { get; set; } = new();
    /// <summary>输出根目录；留空则默认系统下载目录</summary>
    public string Destination { get; set; } = "";
}

// 小地图预览请求（传入地图 id + img，按 {id}_png 帧裁出 base64）
public class MapMinimapPreviewRequest
{
    public int Id { get; set; }
    public string Img { get; set; } = "";
    public string Name { get; set; } = "";
}

public class MapTileJobResult
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public string? JobId { get; set; }
    public int TotalMaps { get; set; }
    public long TotalTiles { get; set; }
}

public class MapTileProgress
{
    public string JobId { get; set; } = "";
    /// <summary>running / done / error</summary>
    public string Status { get; set; } = "";
    public int TotalMaps { get; set; }
    public int DoneMaps { get; set; }
    public long TotalTiles { get; set; }
    public long DoneTiles { get; set; }
    public long Bytes { get; set; }
    public int Failed { get; set; }
    public int Missing { get; set; }
    public int Percent { get; set; }
    public string? CurrentMap { get; set; }
    public List<string> Messages { get; set; } = new();
}

// 真实地图瓦片预览请求（传入地图元信息 + 瓦片所在输出目录）
public class MapRealPreviewRequest
{
    public int Id { get; set; }
    public string Img { get; set; } = "";
    public string Name { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public string Destination { get; set; } = "";
}

// ---------------- 配置表提取（0config*.json 顶层每个键 = 一张配置表） ----------------
/// <summary>单字段的推断结果（用于生成 C# 类）</summary>
public class ConfigTableField
{
    /// <summary>映射回 JSON 的原始键名（用于 [JsonPropertyName]）。
    /// 一般等于 Name；对 item 表这类「数字键数组」表，这里是数字下标本身。</summary>
    public string JsonName { get; set; } = "";
    /// <summary>C# 属性名（语义化，如 Atk / DefMax；未知下标用 FieldN）。</summary>
    public string Name { get; set; } = "";
    /// <summary>C# 类型（string / int / long / double / bool / JsonElement）</summary>
    public string CsType { get; set; } = "string";
    /// <summary>是否在部分条目中缺失（缺失则值类型标记为可空）</summary>
    public bool Optional { get; set; }
    /// <summary>中文注释（item 表里每个数字下标对应的属性含义，从 attribute_score 配置反推）</summary>
    public string Comment { get; set; } = "";
}

/// <summary>一张配置表的元信息</summary>
public class ConfigTableInfo
{
    public bool Ok { get; set; } = true;
    public string? Error { get; set; }
    /// <summary>表名（配置 JSON 的顶层键）</summary>
    public string Name { get; set; } = "";
    /// <summary>条目形态：object（以 id 为键的对象映射）/ array（裸数组）</summary>
    public string Kind { get; set; } = "";
    /// <summary>条目数</summary>
    public int Count { get; set; }
    public List<ConfigTableField> Fields { get; set; } = new();
}

/// <summary>/config/load 请求：可覆盖默认的数据服务器地址与版本号（拼法同游戏 onloadCfgAgain）</summary>
public class ConfigLoadRequest
{
    /// <summary>数据服务器根地址（默认 Mir2Config.CdnBase），如 https://cdn.yscq-wy.yscq.com/fugu_20201203</summary>
    public string? BaseUrl { get; set; }
    /// <summary>游戏版本号（默认 Mir2Config.GameVersion），配置文件名 0config&lt;version&gt;.json 用它</summary>
    public string? Version { get; set; }
}

/// <summary>/config/load 与 /config/tables 的返回</summary>
public class ConfigTableListResult
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public int Count { get; set; }
    public List<ConfigTableInfo> Tables { get; set; } = new();
    /// <summary>本次加载用到的配置分片 URL，供页面展示</summary>
    public List<string> Urls { get; set; } = new();
    public List<string> Messages { get; set; } = new();
}

/// <summary>/config/extract 请求</summary>
public class ConfigExtractRequest
{
    /// <summary>要提取的表名；空或仅含 "*" 表示全部。</summary>
    public List<string> Tables { get; set; } = new();
    /// <summary>输出目录；留空则默认系统下载目录下的 Mir2Config 子目录。</summary>
    public string? OutDir { get; set; }
    /// <summary>写出 C# 类（默认 true）。</summary>
    public bool WriteClass { get; set; } = true;
    /// <summary>写出 JSON 数据（默认 true）。</summary>
    public bool WriteJson { get; set; } = true;
}

public class ConfigTableFile
{
    public string Table { get; set; } = "";
    public string? ClassFile { get; set; }
    public string? DataFile { get; set; }
    public int Entries { get; set; }
}

/// <summary>/config/extract 返回</summary>
public class ConfigExtractResult
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public int TableCount { get; set; }
    public List<ConfigTableFile> Files { get; set; } = new();
    public List<string> Messages { get; set; } = new();
}

// ---------------- 角色 / NPC / 怪物 动画精灵表导出（model/{模型号}/{sourceId}.png|json） ----------------
/// <summary>单个模型条目（resourceVersion.json 的 "model/{模型号}"）。Name 暂留空，待接入名字表后填充。</summary>
public class AnimModelEntry
{
    /// <summary>模型号（即 "model/{模型号}" 里的整数）</summary>
    public string Id { get; set; } = "";
    /// <summary>动画名字（角色/怪物名）；暂未接入名字表，为 null</summary>
    public string? Name { get; set; }
    /// <summary>该模型当前资源版本号（来自 resourceVersion.json，每个模型各自不同，如 3.4428.1 / 10.x）</summary>
    public string? Version { get; set; }
}

/// <summary>/anim/load 返回：解析 resourceVersion.json 后给出的模型号清单</summary>
public class AnimLoadResult
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public string? ResourceVersionUrl { get; set; }
    /// <summary>model/ 条目总数（png + json 合计）</summary>
    public int TotalEntries { get; set; }
    /// <summary>不同模型号数量（每个模型号 = 一个角色/NPC/怪物/武器…的动画集合）</summary>
    public int ModelCount { get; set; }
    /// <summary>全部模型号（按 Id 升序），供前端逐条列出与勾选</summary>
    public List<AnimModelEntry> Models { get; set; } = new();
    public List<string> Messages { get; set; } = new();
}

/// <summary>/anim/export 请求</summary>
public class AnimExportRequest
{
    /// <summary>要导出的模型号列表（逗号/空格分隔，如 "9001,9002,9027"）；为空或仅含 "*" 表示全部模型。</summary>
    public string? Models { get; set; }
    /// <summary>输出根目录；留空则默认系统下载目录下的 Mir2Anim 子目录。文件按 model/{模型号}/{sourceId}.png 平铺。</summary>
    public string? OutDir { get; set; }
    /// <summary>是否同时下载 .json 帧描述（默认 true，缺它无法还原每帧矩形）。</summary>
    public bool WithJson { get; set; } = true;
    /// <summary>动作枚举上界（默认 60，超过此动作号不再尝试；可用以降低请求量）。</summary>
    public int MaxAction { get; set; } = 60;
}

public class AnimJobResult
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public string? JobId { get; set; }
    public int TotalFiles { get; set; }
}

/// <summary>/anim/progress 轮询返回</summary>
public class AnimProgress
{
    public string JobId { get; set; } = "";
    /// <summary>running / done / error</summary>
    public string Status { get; set; } = "";
    public int TotalFiles { get; set; }
    public int DoneFiles { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public long Bytes { get; set; }
    public int Percent { get; set; }
    public string? Current { get; set; }
    public List<string> Messages { get; set; } = new();
}
