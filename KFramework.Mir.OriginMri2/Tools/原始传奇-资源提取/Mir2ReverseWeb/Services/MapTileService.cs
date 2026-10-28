using System.Collections.Concurrent;
using Mir2ReverseWeb.Models;
using SkiaSharp;

namespace Mir2ReverseWeb.Services;

/// <summary>
/// 真实地图瓦片下载服务（类细分，便于将来并入 MonoGame.Client）。
/// 逆向自客户端 main.min.js 的 MapLoader：每幅地图按 512×256 切成 N 张 jpg 小图，
/// 直链规则（见 URL.txt）：
///     {CdnBase}/assets/resource/zipmap/{img}/{index}.jpg?ver={ResourceVersion}
/// 本服务批量下载这些“真实地图小图”（全分辨率），并可选择拼接成一张总览 PNG。
/// 与 GameConfigService 解耦：仅接收 MapEntry（Img/Width/Height/Name），不依赖配置。
/// </summary>
public class MapTileService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private const int TILE_W = 512;
    private const int TILE_H = 256;
    private const int CONCURRENCY = 16;
    private const long SAFE_TILES_FOR_STITCH = 4000;   // 瓦片过多时跳过拼接，避免内存爆掉
    private static readonly ConcurrentDictionary<Guid, Job> _jobs = new();

    /// <summary>启动一个异步下载任务，立即返回 jobId 供前端轮询进度。</summary>
    public Guid Start(List<MapEntry> maps, string destDir, bool stitch)
    {
        var id = Guid.NewGuid();
        var job = new Job { TotalMaps = maps.Count };
        _jobs[id] = job;
        _ = Task.Run(async () => await Run(id, job, maps, destDir, stitch));
        return id;
    }

    public MapTileProgress? GetProgress(Guid id)
    {
        if (!_jobs.TryGetValue(id, out var j)) return null;
        int pct = j.TotalTiles > 0 ? (int)(100L * j.DoneTiles / j.TotalTiles) : (j.Finished ? 100 : 0);
        return new MapTileProgress
        {
            JobId = id.ToString(),
            Status = j.Status,
            TotalMaps = j.TotalMaps,
            DoneMaps = j.DoneMaps,
            TotalTiles = j.TotalTiles,
            DoneTiles = j.DoneTiles,
            Bytes = j.Bytes,
            Failed = j.Failed,
            Missing = j.Missing,
            Percent = pct,
            CurrentMap = j.CurrentMap,
            Messages = j.Messages.TakeLast(12).ToList(),
        };
    }

    private async Task Run(Guid id, Job job, List<MapEntry> maps, string destDir, bool stitch)
    {
        try
        {
            foreach (var m in maps)
            {
                if (string.IsNullOrWhiteSpace(m.Img)) { job.Messages.Add($"[跳过] {m.Id} 无 img 字段"); job.DoneMaps++; continue; }
                int cols = Math.Max(1, (int)Math.Ceiling(m.Width / (double)TILE_W));
                int rows = Math.Max(1, (int)Math.Ceiling(m.Height / (double)TILE_H));
                long n = (long)cols * rows;
                Interlocked.Add(ref job.TotalTiles, n);
                job.CurrentMap = $"{m.Id} {m.Name}";

                var dirName = string.IsNullOrWhiteSpace(m.Name) ? $"map_{m.Id}" : $"{Sanitize(m.Name)}_{m.Id}";
                var mapDir = Path.Combine(destDir, dirName);
                var tileDir = Path.Combine(mapDir, "tiles");
                Directory.CreateDirectory(tileDir);

                var sem = new SemaphoreSlim(CONCURRENCY);
                var tasks = new List<Task>();
                for (int i = 1; i <= n; i++)
                {
                    int idx = i;
                    await sem.WaitAsync();
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            var (ok, len) = await DownloadTileAsync(m.Img!, idx, tileDir);
                            if (ok) { Interlocked.Increment(ref job.DoneTiles); Interlocked.Add(ref job.Bytes, len); }
                            else Interlocked.Increment(ref job.Missing);
                        }
                        catch { Interlocked.Increment(ref job.Failed); }
                        finally { sem.Release(); }
                    }));
                }
                await Task.WhenAll(tasks);
                job.DoneMaps++;
                job.Messages.Add($"[完成] {m.Id} {m.Name}：{n} 张瓦片 -> {tileDir}");

                if (stitch && n <= SAFE_TILES_FOR_STITCH)
                {
                    try
                    {
                        await StitchAsync(tileDir, cols, rows, mapDir, m.Id);
                        job.Messages.Add($"[总览] map_{m.Id}_real.png 已生成");
                    }
                    catch (Exception ex) { job.Messages.Add($"[总览失败] {m.Id}: {ex.Message}"); }
                }
                else if (stitch)
                {
                    job.Messages.Add($"[跳过总览] {m.Id} 瓦片过多({n})，仅保存瓦片（全分辨率真实地图）");
                }
            }
            job.Status = "done";
        }
        catch (Exception ex) { job.Status = "error"; job.Messages.Add("错误: " + ex.Message); }
        finally { job.Finished = true; }
    }

    private async Task<(bool ok, int len)> DownloadTileAsync(string img, int idx, string tileDir)
    {
        var url = $"{Mir2Config.CdnBase}/assets/resource/zipmap/{img}/{idx}.jpg?ver={Mir2Config.ResourceVersion}";
        var file = Path.Combine(tileDir, idx + ".jpg");
        if (File.Exists(file) && new FileInfo(file).Length > 0)
            return (true, (int)new FileInfo(file).Length);
        try
        {
            var bytes = await _http.GetByteArrayAsync(url);
            if (bytes == null || bytes.Length == 0) return (false, 0);
            await File.WriteAllBytesAsync(file, bytes);
            return (true, bytes.Length);
        }
        catch { return (false, 0); }
    }

    /// <summary>将瓦片拼成一张 ≤4096 的总览 PNG（保持真实地图观感；逐瓦片解码后缩放绘制）。</summary>
    private async Task StitchAsync(string tileDir, int cols, int rows, string mapDir, int id)
    {
        const int maxSide = 4096;
        double scale = Math.Min(maxSide / (double)(cols * TILE_W), maxSide / (double)(rows * TILE_H));
        int outW = Math.Max(1, (int)(cols * TILE_W * scale));
        int outH = Math.Max(1, (int)(rows * TILE_H * scale));
        using var bmp = new SKBitmap(outW, outH);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.Black);
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                int idx = r * cols + c + 1;
                var f = Path.Combine(tileDir, idx + ".jpg");
                if (!File.Exists(f)) continue;
                using var tile = SKBitmap.Decode(f);
                if (tile == null) continue;
                var dx = (float)(c * TILE_W * scale);
                var dy = (float)(r * TILE_H * scale);
                var dw = (float)(TILE_W * scale) + 1;
                var dh = (float)(TILE_H * scale) + 1;
                canvas.DrawBitmap(tile, new SKRect(dx, dy, dx + dw, dy + dh));
            }
        }
        using var img = SKImage.FromBitmap(bmp);
        using var png = img.Encode(SKEncodedImageFormat.Png, 90);
        await File.WriteAllBytesAsync(Path.Combine(mapDir, $"map_{id}_real.png"), png.ToArray());
    }

    /// <summary>
    /// 预览：基于已下载的真实瓦片（zipmap/{img}/{n}.jpg）拼接一张缩略 PNG 返回。
    /// 若此前下载时已生成 map_{id}_real.png 总览图则直接复用（最快），否则即时拼接。
    /// 这是“真实地图”预览，与可走性红绿掩码无关。
    /// </summary>
    public (bool Ok, string? Error, string? Base64, int Tiles, int Cols, int Rows) PreviewReal(MapEntry m, string destDir, int maxSide = 1200)
    {
        if (string.IsNullOrWhiteSpace(m.Img))
            return (false, "地图缺少 img 信息（配置可能未加载），无法定位瓦片；请先在「提取地图资源」页加载地图数据", null, 0, 0, 0);
        int cols = Math.Max(1, (int)Math.Ceiling(m.Width / (double)TILE_W));
        int rows = Math.Max(1, (int)Math.Ceiling(m.Height / (double)TILE_H));
        var dirName = string.IsNullOrWhiteSpace(m.Name) ? $"map_{m.Id}" : $"{Sanitize(m.Name)}_{m.Id}";
        var mapDir = Path.Combine(destDir, dirName);
        var tileDir = Path.Combine(mapDir, "tiles");
        if (!Directory.Exists(tileDir))
            return (false, $"尚未下载真实地图瓦片（未找到 {tileDir}）。请先点「提取选中地图资源」下载，再预览。", null, 0, cols, rows);

        // 优先复用已生成的总览图（最快）
        var existing = Path.Combine(mapDir, $"map_{m.Id}_real.png");
        if (File.Exists(existing))
        {
            var bytes = File.ReadAllBytes(existing);
            return (true, null, "data:image/png;base64," + Convert.ToBase64String(bytes),
                Directory.GetFiles(tileDir, "*.jpg").Length, cols, rows);
        }

        var files = Directory.GetFiles(tileDir, "*.jpg")
            .Select(f => { int.TryParse(Path.GetFileNameWithoutExtension(f), out var i); return (f, i); })
            .Where(x => x.i > 0).OrderBy(x => x.i).ToList();
        if (files.Count == 0)
            return (false, "瓦片目录为空，请先点「提取选中地图资源」下载真实瓦片", null, 0, cols, rows);

        double scale = Math.Min(maxSide / (double)(cols * TILE_W), maxSide / (double)(rows * TILE_H));
        int outW = Math.Max(1, (int)(cols * TILE_W * scale));
        int outH = Math.Max(1, (int)(rows * TILE_H * scale));
        using var bmp = new SKBitmap(outW, outH);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.Black);
        foreach (var (f, i) in files)
        {
            int r = (i - 1) / cols, c = (i - 1) % cols;
            using var tile = SKBitmap.Decode(f);
            if (tile == null) continue;
            var dx = (float)(c * TILE_W * scale);
            var dy = (float)(r * TILE_H * scale);
            var dw = (float)(TILE_W * scale) + 1;
            var dh = (float)(TILE_H * scale) + 1;
            canvas.DrawBitmap(tile, new SKRect(dx, dy, dx + dw, dy + dh));
        }
        using var img = SKImage.FromBitmap(bmp);
        using var png = img.Encode(SKEncodedImageFormat.Png, 90);
        return (true, null, "data:image/png;base64," + Convert.ToBase64String(png.ToArray()), files.Count, cols, rows);
    }

    private static string Sanitize(string s)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var c in s) sb.Append(invalid.Contains(c) ? '_' : c);
        var o = sb.ToString().Trim().TrimEnd('.', ' ');
        return string.IsNullOrWhiteSpace(o) ? "map" : o;
    }

    private class Job
    {
        public int TotalMaps;
        public int DoneMaps;
        public long TotalTiles;
        public long DoneTiles;
        public long Bytes;
        public int Failed;
        public int Missing;
        public string Status = "running";
        public string? CurrentMap;
        public bool Finished;
        public List<string> Messages = new();
    }
}
