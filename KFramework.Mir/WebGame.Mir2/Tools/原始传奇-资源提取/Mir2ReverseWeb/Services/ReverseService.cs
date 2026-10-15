using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Mir2ReverseWeb.Models;
using SkiaSharp;

namespace Mir2ReverseWeb.Services;

/// <summary>
/// 原始传奇资源逆向服务。
/// 地图(.dat) 解析采用我们已逆向的格式：
///   大端 [count:u16][ [id:i32][len:i32] … ][ 数据… ]
///   每个地图数据块: 前 4 字节丢弃，其后为 4-bit/格 的单元格（两格压一字节，高 nibble 在前）
///   单元格 bit3=可走 / bit2=遮挡 / bit1=安全区
///   真实网格列数 = ceil(像素宽 / 48)，行数 = ceil(像素高 / 32)（来自 GameDefine.MAP_GRID_*）
/// </summary>
public class ReverseService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(180) };
    private readonly Dictionary<int, MapMeta> _mapConfig = new();
    private readonly Dictionary<int, string> _idToFile = new();
    private string? _loadedFile;
    private byte[]? _mapDatBytes;   // 从 URL 下载并解压后的完整 .dat（仅存内存，不落盘）
    private List<ManifestEntry>? _manifestEntries;   // 加载后的 manifest 条目（内存）
    private static readonly object _lock = new();

    private static readonly JsonSerializerOptions _jsonIndented = new() { WriteIndented = true };

    // ---------------- 透明解压 + 缓存 ----------------
    private readonly Dictionary<string, (long len, long tm, byte[] data)> _cache = new();

    private byte[] GetData(string path)
    {
        var fi = new FileInfo(path);
        var key = path;
        if (_cache.TryGetValue(key, out var c) && c.len == fi.Length && c.tm == fi.LastWriteTimeUtc.Ticks)
            return c.data;

        var raw = File.ReadAllBytes(path);
        var data = DecompressIfNeeded(raw);

        _cache[key] = (fi.Length, fi.LastWriteTimeUtc.Ticks, data);
        return data;
    }

    /// <summary>按需解压：gzip / zlib 魔数则解压，否则原样返回（纯内存，不落盘）</summary>
    private static byte[] DecompressIfNeeded(byte[] raw)
    {
        if (raw.Length > 2 && raw[0] == 0x1f && raw[1] == 0x8b) // gzip
        {
            using var ms = new MemoryStream(raw);
            using var gz = new GZipStream(ms, CompressionMode.Decompress);
            using var o = new MemoryStream(); gz.CopyTo(o); return o.ToArray();
        }
        if (raw.Length > 2 && raw[0] == 0x78 && (raw[1] == 0x01 || raw[1] == 0x9c || raw[1] == 0xda)) // zlib
        {
            using var ms = new MemoryStream(raw);
            using var zl = new ZLibStream(ms, CompressionMode.Decompress);
            using var o = new MemoryStream(); zl.CopyTo(o); return o.ToArray();
        }
        return raw;
    }

    // ---------------- 索引解析（委托给 MapDecoder） ----------------
    public MapIndexResult ParseMapDat(string path)
    {
        var res = new MapIndexResult();
        try
        {
            var data = GetData(path);
            _mapDatBytes = data;
            _loadedFile = path;
            res.SourceFile = path;
            res.Entries = MapDecoder.ParseIndex(data);
            res.Count = res.Entries.Count;
            res.TotalBytes = res.Entries.Sum(e => (long)e.Len);
            res.Ok = true;
        }
        catch (Exception ex) { res.Error = ex.Message; }
        return res;
    }

    // ---------------- 自动下载（仅内存，不落盘） + 配置合并 ----------------
    private async Task<byte[]?> DownloadBytesAsync(string url, List<string> msgs)
    {
        try
        {
            msgs.Add($"[下载] {url}");
            var raw = await _http.GetByteArrayAsync(url);
            var data = DecompressIfNeeded(raw);
            msgs.Add($"[已载] {url}（{(data.Length / 1024.0).ToString("F1")} KB，仅内存）");
            return data;
        }
        catch (Exception ex) { msgs.Add($"[下载失败] {url}：{ex.Message}"); return null; }
    }

    /// <summary>解析配置 JSON（UTF-8 中文地图名，直接读内存字节），合并进内存字典</summary>
    private void MergeConfig(byte[] bytes, string label, List<string> msgs)
    {
        try
        {
            // 配置文件为 UTF-8（不要按 GBK 解码，否则会产生乱码）
            using var doc = JsonDocument.Parse(bytes);
            var root = doc.RootElement;
            // 结构：{ "map": { "154": { id, name, width, height, ... }, ... } }
            if (root.TryGetProperty("map", out var mapEl) && mapEl.ValueKind == JsonValueKind.Object)
                root = mapEl;
            int n = 0;
            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in root.EnumerateObject())
                {
                    if (prop.Value.ValueKind != JsonValueKind.Object) continue;
                    var v = prop.Value;
                    int id = (v.TryGetProperty("id", out var idp) && idp.TryGetInt32(out var idi))
                        ? idi
                        : (int.TryParse(prop.Name, out var pid) ? pid : 0);
                    if (id == 0) continue;
                    string name = v.TryGetProperty("name", out var np) ? (np.GetString() ?? "") : "";
                    string img = v.TryGetProperty("img", out var ip) ? (ip.GetString() ?? "") : "";
                    if (string.IsNullOrEmpty(img) && v.TryGetProperty("id", out var idp2)) img = idp2.GetInt32().ToString();
                    int w = v.TryGetProperty("width", out var wp) ? wp.GetInt32() : 0;
                    int h = v.TryGetProperty("height", out var hp) ? hp.GetInt32() : 0;
                    _mapConfig[id] = new MapMeta { Name = name, Width = w, Height = h, Img = img };
                    n++;
                }
            }
            msgs.Add($"[配置] {label}：{n} 条地图定义");
        }
        catch (Exception ex) { msgs.Add($"[配置失败] {label}：{ex.Message}"); }
    }

    /// <summary>
    /// 自动下载地图包 + 配置（仅放入内存，不写本地磁盘），解析索引并给每张地图补上名字与真实像素宽高。
    /// 地图包 ~1.2MB + 4 个配置分片，下载后直接解压进内存缓存，供预览/导出复用。
    /// </summary>
    public async Task<MapIndexResult> LoadMapDataAsync(string? datUrl = null)
    {
        var res = new MapIndexResult { Messages = new() };
        try
        {
            var datUrls = string.IsNullOrWhiteSpace(datUrl) ? Mir2Config.MapDataUrls : new[] { datUrl };
            foreach (var url in datUrls)
            {
                var bytes = await DownloadBytesAsync(url, res.Messages);
                if (bytes == null) continue;
                var entries = MapDecoder.ParseIndex(bytes);
                res.Entries.AddRange(entries);
                int n = entries.Count;
                res.Messages.Add($"[地图包] {url}：{n} 张");
                _mapDatBytes = bytes;
                _loadedFile = url;
            }
            foreach (var url in Mir2Config.MapConfigUrls)
            {
                var bytes = await DownloadBytesAsync(url, res.Messages);
                if (bytes == null) continue;
                MergeConfig(bytes, url, res.Messages);
            }
            lock (_lock)
            {
                foreach (var e in res.Entries)
                {
                    if (_mapConfig.TryGetValue(e.Id, out var m)) { e.Name = m.Name; e.Width = m.Width; e.Height = m.Height; e.Img = m.Img; }
                }
            }
            res.ConfigCount = _mapConfig.Count;
            res.SourceFile = _loadedFile;
            res.Count = res.Entries.Count;
            res.TotalBytes = res.Entries.Sum(e => (long)e.Len);
            res.MapConfigUrls = Mir2Config.MapConfigUrls.ToList();
            res.MapJsUrls = Mir2Config.MapJsUrls.ToList();
            res.Ok = true;
        }
        catch (Exception ex) { res.Error = ex.Message; }
        return res;
    }

    /// <summary>操作系统默认下载目录（UserProfile/Downloads）</summary>
    internal static string OsDownloadDir()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    // ---------------- 资源 manifest 加载 + 下载（仅内存解析，按需落盘） ----------------
    /// <summary>由 manifest 条目推导实际 CDN 文件 URL：{CdnBase}/resource/{v前2位}/{v}_{s}.{ext}</summary>
    private static string ManifestToUrl(string key, string hash, long size)
    {
        var v2 = hash.Length >= 2 ? hash.Substring(0, 2) : hash;
        var cleanExt = Path.GetExtension(key).TrimStart('.');
        return $"{Mir2Config.CdnBase}/resource/{v2}/{hash}_{size}.{cleanExt}";
    }

    public async Task<ManifestResult> LoadManifestAsync()
    {
        var res = new ManifestResult { Messages = new() };
        try
        {
            res.ManifestUrl = Mir2Config.ManifestUrl;
            res.Messages.Add($"[下载] {Mir2Config.ManifestUrl}");
            var raw = await _http.GetByteArrayAsync(Mir2Config.ManifestUrl);
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            var list = new List<ManifestEntry>();
            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in root.EnumerateObject())
                {
                    if (prop.Value.ValueKind != JsonValueKind.Object) continue;
                    var v = prop.Value;
                    var hash = v.TryGetProperty("v", out var vp) ? (vp.GetString() ?? "") : "";
                    var size = v.TryGetProperty("s", out var sp) ? sp.GetInt64() : 0;
                    var url = ManifestToUrl(prop.Name, hash, size);
                    list.Add(new ManifestEntry { Key = prop.Name, Hash = hash, Size = size, Ext = Path.GetExtension(prop.Name), Url = url });
                }
            }
            _manifestEntries = list;
            res.Entries = list;
            res.Count = list.Count;
            res.Messages.Add($"[清单] 共 {list.Count} 条资源");
            res.Ok = true;
        }
        catch (Exception ex) { res.Error = ex.Message; }
        return res;
    }

    public async Task<AtlasDownloadResult> DownloadManifestAsset(string key, string? outDir = null)
    {
        var res = new AtlasDownloadResult { Key = key };
        try
        {
            var entry = _manifestEntries?.FirstOrDefault(e => e.Key == key);
            if (entry == null) { res.Error = "清单未加载或无此条目"; return res; }
            res.Url = entry.Url;
            var sub = entry.Hash.Length >= 2 ? entry.Hash.Substring(0, 2) : entry.Hash;
            var baseDir = string.IsNullOrWhiteSpace(outDir) ? OsDownloadDir() : outDir!;
            var dir = Path.Combine(baseDir, sub);
            Directory.CreateDirectory(dir);
            var fileName = $"{entry.Hash}_{entry.Size}.{entry.Ext.TrimStart('.')}";
            var file = Path.Combine(dir, fileName);
            if (File.Exists(file) && new FileInfo(file).Length == entry.Size)
            {
                res.Skipped = true; res.LocalPath = file; res.Bytes = entry.Size; res.Ok = true; return res;
            }
            var bytes = await _http.GetByteArrayAsync(entry.Url);
            await File.WriteAllBytesAsync(file, bytes);
            res.LocalPath = file; res.Bytes = bytes.Length; res.Ok = true;
        }
        catch (Exception ex) { res.Error = ex.Message; }
        return res;
    }

    public async Task<AtlasDownloadAllResult> DownloadManifestAll(string? outDir = null)
    {
        var res = new AtlasDownloadAllResult { Messages = new() };
        try
        {
            var list = _manifestEntries;
            if (list == null || list.Count == 0) { res.Error = "请先加载清单"; return res; }
            res.Total = list.Count;
            int n = 0;
            foreach (var e in list)
            {
                n++;
                var r = await DownloadManifestAsset(e.Key, outDir);
                if (r.Skipped) res.Skipped++;
                else if (r.Ok) { res.Downloaded++; res.TotalBytes += r.Bytes; }
                else res.Failed++;
                if (n % 50 == 0) res.Messages.Add($"[进度] {n}/{list.Count}（下载 {res.Downloaded} 跳过 {res.Skipped} 失败 {res.Failed}）");
            }
            res.Messages.Add($"[完成] 总计 {res.Total}：下载 {res.Downloaded}，跳过 {res.Skipped}，失败 {res.Failed}，共 {(res.TotalBytes / 1048576.0).ToString("F1")} MB");
            res.Ok = true;
        }
        catch (Exception ex) { res.Error = ex.Message; }
        return res;
    }

    private string? ResolveFile(int id)
    {
        if (_idToFile.TryGetValue(id, out var f)) return f;
        return _loadedFile;
    }

    public MapDecodeResult DecodeMap(MapDecodeRequest req)
    {
        var res = new MapDecodeResult { Id = req.Id };
        try
        {
            // URL 加载的数据已解压进内存（_mapDatBytes）；仅当内存无数据时才回退到本地文件
            byte[]? data = _mapDatBytes;
            if (data == null)
            {
                string? file = (!string.IsNullOrWhiteSpace(req.Path) && File.Exists(req.Path)) ? req.Path : ResolveFile(req.Id);
                if (file == null || !File.Exists(file)) { res.Error = "未加载地图数据，请先在「提取地图资源」页加载"; return res; }
                data = GetData(file);
            }
            var block = MapDecoder.ExtractMapBlock(data, req.Id, out int rawLen);
            if (block == null) { res.Error = $"未找到地图 {req.Id}（块长 {rawLen}）"; return res; }

            int pw = req.Width, ph = req.Height;
            string name = "";
            if (_mapConfig.TryGetValue(req.Id, out var m)) { name = m.Name; if (pw <= 0) pw = m.Width; if (ph <= 0) ph = m.Height; }

            // 委托给 MapDecoder 解析网格与逐格标志（纯逻辑，可合并到 MonoGame.Client）
            var map = MapDecoder.Decode(req.Id, block, pw, ph, name, Mir2Config.GridWidth, Mir2Config.GridHeight);

            // 导出模式：渲染真实网格比例的完整地图图片（最长边 ≤ 4096）；否则渲染内联预览（≤ 760）
            bool export = !string.IsNullOrWhiteSpace(req.Destination) && Directory.Exists(req.Destination);
            int side = export ? 4096 : 760;
            using var bmp = MapRenderer.RenderMask(map, side);

            if (export)
            {
                var pngBytes = MapRenderer.EncodePng(bmp);
                var pngFile = Path.Combine(req.Destination!, $"map_{req.Id}.png");
                File.WriteAllBytes(pngFile, pngBytes);
                res.OutFile = pngFile;

                // 同时导出结构化地图数据（JSON，含每格可走/遮挡/安全区标志）
                var jsonFile = Path.Combine(req.Destination!, $"map_{req.Id}.json");
                File.WriteAllText(jsonFile, JsonSerializer.Serialize(map, _jsonIndented));
                res.OutDataFile = jsonFile;

                // 内联预览使用缩小图，避免超大 base64
                using var prev = MapRenderer.ScaleDown(bmp, 760);
                res.PreviewPngBase64 = "data:image/png;base64," + Convert.ToBase64String(MapRenderer.EncodePng(prev));
            }
            else
            {
                res.PreviewPngBase64 = "data:image/png;base64," + Convert.ToBase64String(MapRenderer.EncodePng(bmp));
            }

            res.Cells = map.Cols * map.Rows;
            res.RawCells = map.RawCells;
            res.Cols = map.Cols;
            res.Rows = map.Rows;
            res.ConfigCells = (pw > 0 && ph > 0)
                ? (int)Math.Ceiling(pw / (double)Mir2Config.GridWidth) * (int)Math.Ceiling(ph / (double)Mir2Config.GridHeight)
                : 0;
            res.Walkable = map.Walkable;
            res.Cover = map.Cover;
            res.Safe = map.Safe;
            res.Warn = map.Warn;
            res.Ok = true;
        }
        catch (Exception ex) { res.Error = ex.Message; }
        return res;
    }

    public MapExportResult ExportMaps(MapExportRequest req)
    {
        var res = new MapExportResult();
        try
        {
            if (req.Ids.Count == 0) { res.Error = "未选择地图"; return res; }
            Directory.CreateDirectory(req.Destination);
            foreach (var id in req.Ids)
            {
                // 每张地图一个以「地图名_id」命名的文件夹
                string name = _mapConfig.TryGetValue(id, out var mc) ? mc.Name : "";
                string dirName = string.IsNullOrWhiteSpace(name) ? $"map_{id}" : $"{Sanitize(name)}_{id}";
                string dir = Path.Combine(req.Destination, dirName);
                Directory.CreateDirectory(dir);

                var dm = new MapDecodeRequest
                {
                    Path = req.Path,
                    Id = id,
                    Destination = dir
                };
                if (_mapConfig.TryGetValue(id, out var m)) { dm.Width = m.Width; dm.Height = m.Height; }
                var r = DecodeMap(dm);
                if (r.Ok && r.OutFile != null) res.Files.Add(r.OutFile);
                if (r.Ok && r.OutDataFile != null) res.DataFiles.Add(r.OutDataFile);
            }
            res.Exported = res.Files.Count;
            res.Ok = true;
        }
        catch (Exception ex) { res.Error = ex.Message; }
        return res;
    }

    /// <summary>清理文件名中的非法字符，避免建文件夹失败</summary>
    private static string Sanitize(string s)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var c in s)
            sb.Append(invalid.Contains(c) ? '_' : c);
        var outStr = sb.ToString().Trim().TrimEnd('.', ' ');
        return string.IsNullOrWhiteSpace(outStr) ? "map" : outStr;
    }

    // ---------------- 图集 WIL/WIX ----------------
    public ScanResult Scan(string root, List<string> exts)
    {
        var res = new ScanResult();
        try
        {
            if (!Directory.Exists(root)) { res.Error = "目录不存在"; return res; }
            var lower = exts.Select(e => e.Trim().ToLowerInvariant()).ToList();
            var files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(f =>
                {
                    if (lower.Count == 0) return true;
                    var e = Path.GetExtension(f).ToLowerInvariant();
                    return lower.Contains(e) || lower.Contains(e.TrimStart('.'));
                })
                .Select(f => new FsEntry
                {
                    Name = Path.GetFileName(f),
                    Path = f,
                    IsDir = false,
                    Size = new FileInfo(f).Length
                })
                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            res.Files = files;
            res.Ok = true;
        }
        catch (Exception ex) { res.Error = ex.Message; }
        return res;
    }

    public AtlasScanResult ScanAtlases(string root)
    {
        var res = new AtlasScanResult();
        try
        {
            if (!Directory.Exists(root)) { res.Error = "目录不存在"; return res; }
            var exts = new HashSet<string> { ".wil", ".lib", ".wix", ".wzl", ".wzx" };
            var files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(f => exts.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .Select(f => new AtlasFile
                {
                    Name = Path.GetFileName(f),
                    Path = f,
                    Kind = Path.GetExtension(f).TrimStart('.').ToLowerInvariant(),
                    Size = new FileInfo(f).Length
                })
                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            res.Files = files;
            res.Ok = true;
        }
        catch (Exception ex) { res.Error = ex.Message; }
        return res;
    }

    /// <summary>
    /// 解析 WIL 头 + 通过 WIX 索引读取每张图的尺寸。
    /// WIL: 40 字节标题 + int32 版本 + int32 图像数；WIX: int32 图像数 + N×int32 偏移。
    /// 像素解码（调色板/RLE）后续接入。
    /// </summary>
    public AtlasInfo ReadAtlas(string wilPath)
    {
        var info = new AtlasInfo();
        try
        {
            if (!File.Exists(wilPath)) { info.Error = "文件不存在"; return info; }
            var dir = Path.GetDirectoryName(wilPath)!;
            var name = Path.GetFileNameWithoutExtension(wilPath);
            var wixPath = Path.Combine(dir, name + ".wix");
            byte[] wil = File.ReadAllBytes(wilPath);

            if (wil.Length >= 44)
            {
                int tl = 0; while (tl < 40 && wil[tl] != 0) tl++;
                info.Title = (tl > 0) ? Encoding.ASCII.GetString(wil, 0, tl) : "(无标题)";
                info.Version = BitConverter.ToInt32(wil, 40);
                info.ImageCount = BitConverter.ToInt32(wil, 44);
            }

            List<int> offsets = new();
            if (File.Exists(wixPath))
            {
                byte[] wix = File.ReadAllBytes(wixPath);
                int n = BitConverter.ToInt32(wix, 0);
                int start = 4;
                if (n <= 0 || n * 4 + 4 > wix.Length) { n = wix.Length / 4; start = 0; }
                for (int i = 0; i < n; i++)
                {
                    int off = BitConverter.ToInt32(wix, start + i * 4);
                    if (off > 0 && off + 16 <= wil.Length) offsets.Add(off);
                }
            }

            foreach (var off in offsets)
            {
                int w = BitConverter.ToInt32(wil, off);
                int h = BitConverter.ToInt32(wil, off + 4);
                int ox = BitConverter.ToInt32(wil, off + 8);
                int oy = BitConverter.ToInt32(wil, off + 12);
                if (w <= 0 || w > 4096 || h <= 0 || h > 4096) continue;
                info.Images.Add(new AtlasImage
                {
                    Index = info.Images.Count,
                    Width = w,
                    Height = h,
                    OffsetX = ox,
                    OffsetY = oy,
                    HasData = true
                });
            }
            info.ParsedImages = info.Images.Count;
            info.Ok = true;
        }
        catch (Exception ex) { info.Error = ex.Message; }
        return info;
    }

    // ---------------- 文件信息 ----------------
    public FileInfoResult GetFileInfo(string path)
    {
        var res = new FileInfoResult { Path = path };
        try
        {
            if (!File.Exists(path)) { res.Exists = false; res.Note = "文件不存在"; return res; }
            var fi = new FileInfo(path);
            res.Exists = true;
            res.Size = fi.Length;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            res.Kind = ext.TrimStart('.');
            if (ext is ".txt" or ".json" or ".csv" or ".ini" or ".xml" or ".html" or ".js" or ".ts")
            {
                var text = File.ReadAllText(path);
                res.Lines = text.Split('\n').Length;
                res.Note = "文本文件，已统计行数";
            }
            else if (ext is ".mirdb")
            {
                res.Note = "MirDB 二进制数据库（怪物/物品/技能/NPC 等定义），像素/结构解析待接入";
            }
            else
            {
                res.Note = "二进制资源，结构解析待接入";
            }
        }
        catch (Exception ex) { res.Note = "读取失败: " + ex.Message; }
        return res;
    }

    // ---------------- 文件系统 ----------------
    public List<FsEntry> ListDir(string? path, bool files, string? filter)
    {
        var root = string.IsNullOrWhiteSpace(path)
            ? (OperatingSystem.IsWindows() ? "C:\\" : "/")
            : path!;
        if (!Directory.Exists(root))
            return new List<FsEntry>();

        var exts = (filter ?? "")
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim().ToLowerInvariant())
            .Where(x => x.Length > 0)
            .ToList();

        var entries = new List<FsEntry>();
        try { foreach (var d in Directory.EnumerateDirectories(root)) entries.Add(new FsEntry { Name = Path.GetFileName(d), Path = d, IsDir = true }); }
        catch { }
        if (files)
        {
            try
            {
                foreach (var f in Directory.EnumerateFiles(root))
                {
                    var ext = Path.GetExtension(f).ToLowerInvariant();
                    if (exts.Count != 0 && !exts.Contains(ext)) continue;
                    var fi = new FileInfo(f);
                    entries.Add(new FsEntry { Name = Path.GetFileName(f), Path = f, IsDir = false, Size = fi.Length });
                }
            }
            catch { }
        }
        return entries.OrderBy(e => !e.IsDir).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }
}

/// <summary>内存中的地图元数据（来自 config 合并）</summary>
internal class MapMeta
{
    public string Name { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public string Img { get; set; } = "";
}
