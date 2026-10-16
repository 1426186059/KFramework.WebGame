using System.Text;
using System.Text.Json;
using Mir2ReverseWeb.Models;
using SkiaSharp;

namespace Mir2ReverseWeb.Services;

/// <summary>
/// 小地图提取。线上小地图有两种形态（逆向自 main.min.js 的 addMinimap / checkMinimap）：
/// 1) 独立整图：assets/resource/minimap/{img}.jpg —— 每幅地图一张完整小地图，
///    resourceVersion.json 中登记了 600+ 条，按地图 img 命名（与图集帧一致）。这是“真正的小地图”。
/// 2) 图集帧：minimaps.json 图集里的 {img}_png 帧（缺失时用 43005_png 默认帧兜底），是游戏内角落小缩略。
/// 游戏内 addMinimap 取 frames[img+"_png"]，缺失回退 43005_png。
/// 本服务优先取独立整图（更完整），取不到再回退图集帧。所有结果统一编码为 PNG 返回/落盘。
/// </summary>
public class MapMinimapService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private static readonly object _lock = new();
    private static SKBitmap? _atlas;
    private static Dictionary<string, Frame>? _frames;
    private static readonly Dictionary<string, (byte[] Png, int W, int H)> _singleCache = new();
    private static readonly HashSet<string> _singleMissing = new();

    private record Frame(int X, int Y, int W, int H, string Key);

    /// <summary>预览：返回 base64 PNG（统一编码为 PNG）。</summary>
    public (bool Ok, string? Error, string? Base64, int W, int H, string Source) Preview(int id, string? img, string? name)
    {
        var (ok, err, png, w, h, source) = GetMinimap(img, id);
        if (!ok) return (false, err, null, 0, 0, "");
        return (true, null, "data:image/png;base64," + Convert.ToBase64String(png), w, h, source);
    }

    /// <summary>提取：把小地图保存为 &lt;名&gt;_&lt;id&gt;/minimap.png。</summary>
    public (bool Ok, string? File, string? Error) Extract(string destDir, int id, string? img, string? name)
    {
        var (ok, err, png, w, h, source) = GetMinimap(img, id);
        if (!ok) return (false, null, err);
        var dirName = string.IsNullOrWhiteSpace(name) ? $"map_{id}" : $"{Sanitize(name)}_{id}";
        var mapDir = Path.Combine(destDir, dirName);
        Directory.CreateDirectory(mapDir);
        try
        {
            var outFile = Path.Combine(mapDir, "minimap.png");
            File.WriteAllBytes(outFile, png);
            return (true, outFile, null);
        }
        catch (Exception ex) { return (false, null, "保存小地图失败: " + ex.Message); }
    }

    /// <summary>统一取小地图 PNG 字节（优先独立整图，回退图集帧）。</summary>
    private (bool Ok, string? Error, byte[] Png, int W, int H, string Source) GetMinimap(string? img, int id)
    {
        // 1) 独立整图 minimap/{img}.jpg（按 img 命名）
        if (!string.IsNullOrWhiteSpace(img))
        {
            var single = TryStandalone(img);
            if (single != null)
                return (true, null, single.Value.Png, single.Value.W, single.Value.H, "minimap/" + img + ".jpg（独立整图）");
        }

        // 2) 图集帧 img_png -> 43005_png 兜底
        var aerr = EnsureAtlas();
        if (aerr != null) return (false, aerr, Array.Empty<byte>(), 0, 0, "");
        var (f, key) = ResolveAtlasFrame(img);
        if (f == null)
            return (false,
                $"该地图没有小地图资源（minimap/{img}.jpg 不存在，图集也无 {img}_png / 43005_png 帧）",
                Array.Empty<byte>(), 0, 0, "");
        try
        {
            using var subset = new SKBitmap(f.W, f.H);
            using (var canvas = new SKCanvas(subset))
                canvas.DrawBitmap(_atlas!, new SKRectI(f.X, f.Y, f.X + f.W, f.Y + f.H), new SKRect(0, 0, f.W, f.H));
            using var image = SKImage.FromBitmap(subset);
            using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            return (true, null, png.ToArray(), f.W, f.H, "minimaps 图集帧 " + key);
        }
        catch (Exception ex) { return (false, "裁剪小地图失败: " + ex.Message, Array.Empty<byte>(), 0, 0, ""); }
    }

    /// <summary>下载并归一化为 PNG 的独立小地图整图；不存在/非图片返回 null（并记入缺失集合避免重复请求）。</summary>
    private (byte[] Png, int W, int H)? TryStandalone(string img)
    {
        lock (_lock)
        {
            if (_singleMissing.Contains(img)) return null;
            if (_singleCache.TryGetValue(img, out var cached)) return cached;
        }
        var url = $"{Mir2Config.CdnBase}/assets/resource/minimap/{img}.jpg?ver={Mir2Config.ResourceVersion}";
        try
        {
            var bytes = _http.GetByteArrayAsync(url).Result;
            if (bytes.Length < 2 || bytes[0] != 0xFF || bytes[1] != 0xD8) // 非 JPEG
            {
                lock (_lock) { _singleMissing.Add(img); }
                return null;
            }
            using var bmp = SKBitmap.Decode(bytes);
            if (bmp == null)
            {
                lock (_lock) { _singleMissing.Add(img); }
                return null;
            }
            using var image = SKImage.FromBitmap(bmp);
            using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            var pngBytes = png.ToArray();
            var result = (pngBytes, bmp.Width, bmp.Height);
            lock (_lock) { _singleCache[img] = result; }
            return result;
        }
        catch
        {
            lock (_lock) { _singleMissing.Add(img); }
            return null;
        }
    }

    /// <summary>图集帧定位：img_png 优先，缺失回退 43005_png 默认帧。</summary>
    private (Frame? frame, string? key) ResolveAtlasFrame(string? img)
    {
        if (_frames == null) return (null, null);
        var keys = img != null ? new[] { img + "_png", "43005_png" } : new[] { "43005_png" };
        foreach (var k in keys)
            if (_frames.TryGetValue(k, out var f)) return (f, k);
        return (null, null);
    }

    private string? EnsureAtlas()
    {
        if (_atlas != null && _frames != null) return null;
        lock (_lock)
        {
            if (_atlas != null && _frames != null) return null;
            try
            {
                var pngUrl = $"{Mir2Config.CdnBase}/assets/resource/minimap/minimaps.png?ver={Mir2Config.ResourceVersion}";
                var jsonUrl = $"{Mir2Config.CdnBase}/assets/resource/minimap/minimaps.json?ver={Mir2Config.ResourceVersion}";
                var pngBytes = _http.GetByteArrayAsync(pngUrl).Result;
                var jsonBytes = _http.GetByteArrayAsync(jsonUrl).Result;
                var json = Encoding.UTF8.GetString(jsonBytes);
                var doc = JsonDocument.Parse(json);
                var frames = new Dictionary<string, Frame>();
                if (doc.RootElement.TryGetProperty("frames", out var fe))
                    foreach (var el in fe.EnumerateObject())
                    {
                        var v = el.Value;
                        var fv = v.TryGetProperty("frame", out var nf) ? nf : v; // 兼容嵌套/顶层两种格式
                        frames[el.Name] = new Frame(
                            fv.GetProperty("x").GetInt32(),
                            fv.GetProperty("y").GetInt32(),
                            fv.GetProperty("w").GetInt32(),
                            fv.GetProperty("h").GetInt32(),
                            el.Name);
                    }
                var bmp = SKBitmap.Decode(pngBytes);
                if (bmp == null) return "小地图图集 PNG 解码失败";
                _frames = frames;
                _atlas = bmp;
                return null;
            }
            catch (Exception ex) { return "下载/解析小地图图集失败: " + ex.Message; }
        }
    }

    private static string Sanitize(string s)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var c in s) sb.Append(invalid.Contains(c) ? '_' : c);
        var o = sb.ToString().Trim().TrimEnd('.', ' ');
        return string.IsNullOrWhiteSpace(o) ? "map" : o;
    }
}
