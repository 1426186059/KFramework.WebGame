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
    private static readonly object _lock = new();

    private static int ReadBEInt(byte[] d, int p)
        => (d[p] << 24) | (d[p + 1] << 16) | (d[p + 2] << 8) | d[p + 3];

    // ---------------- 透明解压 + 缓存 ----------------
    private readonly Dictionary<string, (long len, long tm, byte[] data)> _cache = new();

    private byte[] GetData(string path)
    {
        var fi = new FileInfo(path);
        var key = path;
        if (_cache.TryGetValue(key, out var c) && c.len == fi.Length && c.tm == fi.LastWriteTimeUtc.Ticks)
            return c.data;

        var raw = File.ReadAllBytes(path);
        byte[] data = raw;
        if (raw.Length > 2 && raw[0] == 0x1f && raw[1] == 0x8b) // gzip
        {
            using var ms = new MemoryStream(raw);
            using var gz = new GZipStream(ms, CompressionMode.Decompress);
            using var o = new MemoryStream(); gz.CopyTo(o); data = o.ToArray();
        }
        else if (raw.Length > 2 && raw[0] == 0x78 && (raw[1] == 0x01 || raw[1] == 0x9c || raw[1] == 0xda)) // zlib
        {
            using var ms = new MemoryStream(raw);
            using var zl = new ZLibStream(ms, CompressionMode.Decompress);
            using var o = new MemoryStream(); zl.CopyTo(o); data = o.ToArray();
        }

        _cache[key] = (fi.Length, fi.LastWriteTimeUtc.Ticks, data);
        return data;
    }

    // ---------------- 索引解析 ----------------
    private int ParseIndexInto(byte[] data, List<MapEntry> list)
    {
        int count = (data[0] << 8) | data[1];
        int pos = 2;
        for (int i = 0; i < count && pos < data.Length; i++)
        {
            if (pos + 8 > data.Length) break;
            int id = ReadBEInt(data, pos);
            int len = ReadBEInt(data, pos + 4);
            pos += 8;
            if (len < 0 || len > data.Length - pos) break;
            list.Add(new MapEntry { Id = id, Len = len, Kb = Math.Round(len / 1024.0, 2) });
            pos += len;
        }
        return list.Count;
    }

    public MapIndexResult ParseMapDat(string path)
    {
        var res = new MapIndexResult();
        try
        {
            var data = GetData(path);
            res.Count = ParseIndexInto(data, res.Entries);
            res.TotalBytes = res.Entries.Sum(e => (long)e.Len);
            res.Ok = true;
        }
        catch (Exception ex) { res.Error = ex.Message; }
        return res;
    }

    // ---------------- 自动下载 + 配置合并 ----------------
    private string CacheRoot() => Path.Combine(Directory.GetCurrentDirectory(), Mir2Config.CacheDir);

    private async Task<string?> DownloadAsync(string url, List<string> msgs)
    {
        try
        {
            var uri = new Uri(url);
            var seg = string.Concat(uri.Segments
                .Where(s => !string.IsNullOrEmpty(s) && s != "/")
                .Select(s => s.Trim('/')))
                .Replace('/', '_');
            var cache = CacheRoot();
            Directory.CreateDirectory(cache);
            var file = Path.Combine(cache, seg + ".bin");
            if (File.Exists(file) && new FileInfo(file).Length > 0)
            {
                msgs.Add($"[缓存] {seg}");
                return file;
            }
            msgs.Add($"[下载] {url}");
            var bytes = await _http.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(file, bytes);
            msgs.Add($"[已存] {seg} ({bytes.Length} 字节)");
            return file;
        }
        catch (Exception ex) { msgs.Add($"[下载失败] {url}：{ex.Message}"); return null; }
    }

    /// <summary>解析配置 JSON（GBK 编码的中文地图名），合并进内存字典</summary>
    private void MergeConfig(string file, List<string> msgs)
    {
        try
        {
            var bytes = File.ReadAllBytes(file);
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
                    int w = v.TryGetProperty("width", out var wp) ? wp.GetInt32() : 0;
                    int h = v.TryGetProperty("height", out var hp) ? hp.GetInt32() : 0;
                    _mapConfig[id] = new MapMeta { Name = name, Width = w, Height = h };
                    n++;
                }
            }
            msgs.Add($"[配置] {Path.GetFileName(file)}：{n} 条地图定义");
        }
        catch (Exception ex) { msgs.Add($"[配置失败] {file}：{ex.Message}"); }
    }

    /// <summary>
    /// 自动下载地图包 + 配置，解析索引并给每张地图补上名字与真实像素宽高。
    /// 首次会联网下载（地图包 ~1.2MB + 4 个配置分片各 ~7MB），之后走本地缓存。
    /// </summary>
    public async Task<MapIndexResult> LoadMapDataAsync()
    {
        var res = new MapIndexResult { Messages = new() };
        try
        {
            foreach (var url in Mir2Config.MapDataUrls)
            {
                var f = await DownloadAsync(url, res.Messages);
                if (f == null) continue;
                var data = GetData(f);
                int n = ParseIndexInto(data, res.Entries);
                res.Messages.Add($"[地图包] {Path.GetFileName(f)}：{n} 张");
                _loadedFile = f;
            }
            foreach (var url in Mir2Config.MapConfigUrls)
            {
                var f = await DownloadAsync(url, res.Messages);
                if (f == null) continue;
                MergeConfig(f, res.Messages);
            }
            lock (_lock)
            {
                foreach (var e in res.Entries)
                {
                    _idToFile[e.Id] = _loadedFile!;
                    if (_mapConfig.TryGetValue(e.Id, out var m)) { e.Name = m.Name; e.Width = m.Width; e.Height = m.Height; }
                }
            }
            res.ConfigCount = _mapConfig.Count;
            res.SourceFile = _loadedFile;
            res.Count = res.Entries.Count;
            res.TotalBytes = res.Entries.Sum(e => (long)e.Len);
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

    private byte[]? ExtractMapBlock(byte[] data, int id, out int lenOut)
    {
        lenOut = 0;
        int count = (data[0] << 8) | data[1];
        int pos = 2;
        for (int i = 0; i < count && pos < data.Length; i++)
        {
            if (pos + 8 > data.Length) break;
            int mid = ReadBEInt(data, pos);
            int len = ReadBEInt(data, pos + 4);
            pos += 8;
            if (len < 0 || len > data.Length - pos) break;
            if (mid == id)
            {
                lenOut = len;
                if (len <= 4) return null;
                return data.Skip(pos + 4).Take(len - 4).ToArray();
            }
            pos += len;
        }
        return null;
    }

    public MapDecodeResult DecodeMap(MapDecodeRequest req)
    {
        var res = new MapDecodeResult { Id = req.Id };
        try
        {
            // 优先用请求里带的文件路径；若路径无效（如含中文在传输中损坏），回退到加载时记录的内存路径
            string? file = (!string.IsNullOrWhiteSpace(req.Path) && File.Exists(req.Path)) ? req.Path : ResolveFile(req.Id);
            if (file == null || !File.Exists(file)) { res.Error = "未加载地图数据，请先在「提取地图资源」页加载"; return res; }
            var data = GetData(file);
            var block = ExtractMapBlock(data, req.Id, out int rawLen);
            if (block == null) { res.Error = $"未找到地图 {req.Id}（块长 {rawLen}）"; return res; }

            int pw = req.Width, ph = req.Height;
            if ((pw <= 0 || ph <= 0) && _mapConfig.TryGetValue(req.Id, out var m)) { pw = m.Width; ph = m.Height; }

            int cols, rows;
            bool fromConfig = pw > 0 && ph > 0;
            if (fromConfig)
            {
                cols = (int)Math.Ceiling(pw / (double)Mir2Config.GridWidth);
                rows = (int)Math.Ceiling(ph / (double)Mir2Config.GridHeight);
            }
            else
            {
                int total = block.Length * 2;
                cols = (int)Math.Ceiling(Math.Sqrt(total));
                rows = (int)Math.Ceiling(total / (double)cols);
            }

            int maxCells = cols * rows;
            // 单元格按 48:32 比例绘制（3:2），避免预览变形
            int CW = 3, CH = 2;

            int walk = 0, cover = 0, safe = 0, idx = 0;
            using var bmp = new SKBitmap(Math.Max(1, cols) * CW, Math.Max(1, rows) * CH);
            using var canvas = new SKCanvas(bmp);
            canvas.Clear(new SKColor(18, 22, 28));
            using var pWalk = new SKPaint { Color = new SKColor(56, 180, 99) };
            using var pBlock = new SKPaint { Color = new SKColor(38, 46, 58) };
            using var pSafe = new SKPaint { Color = new SKColor(230, 200, 40) };
            using var pCover = new SKPaint { Color = new SKColor(220, 90, 80) };

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int nib = 0;
                    if (idx < maxCells)
                        nib = (idx % 2 == 0) ? ((block[idx / 2] >> 4) & 0xF) : (block[idx / 2] & 0xF);
                    idx++;
                    bool w = (nib & 8) != 0, cov = (nib & 4) != 0, sf = (nib & 2) != 0;
                    if (idx <= maxCells) { if (w) walk++; if (cov) cover++; if (sf) safe++; }
                    var paint = !w ? pBlock : (sf ? pSafe : pWalk);
                    canvas.DrawRect(c * CW, r * CH, CW, CH, paint);
                    if (cov) canvas.DrawRect(c * CW, r * CH, CW, 1, pCover); // 遮挡：顶红线
                }
            }

            using var img = SKImage.FromBitmap(bmp);
            using var png = img.Encode(SKEncodedImageFormat.Png, 100);
            byte[] bytes = png.ToArray();
            res.PreviewPngBase64 = "data:image/png;base64," + Convert.ToBase64String(bytes);
            res.Cells = maxCells;
            res.Walkable = walk;
            res.Cover = cover;
            res.Safe = safe;

            if (!string.IsNullOrWhiteSpace(req.Destination) && Directory.Exists(req.Destination))
            {
                var outFile = Path.Combine(req.Destination, $"map_{req.Id}.png");
                File.WriteAllBytes(outFile, bytes);
                res.OutFile = outFile;
            }
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
                var dm = new MapDecodeRequest
                {
                    Path = req.Path,
                    Id = id,
                    Destination = req.Destination
                };
                if (_mapConfig.TryGetValue(id, out var m)) { dm.Width = m.Width; dm.Height = m.Height; }
                var r = DecodeMap(dm);
                if (r.Ok && r.OutFile != null) res.Files.Add(r.OutFile);
            }
            res.Exported = res.Files.Count;
            res.Ok = true;
        }
        catch (Exception ex) { res.Error = ex.Message; }
        return res;
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
}
