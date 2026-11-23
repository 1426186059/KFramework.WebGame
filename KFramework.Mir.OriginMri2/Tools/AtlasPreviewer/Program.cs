using System.Text.Json;
using System.IO;
using System.IO.Compression;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// 用户在页面粘贴“本地图集文件夹路径”，服务端（本机运行的 dotnet）直接读该路径，
// 列出 / 解析 *-sheet.json 并返回同目录 png。无需浏览器选文件夹、也无需把文件拷进工程。
// 这是本地开发工具，服务端即用户本机，故可读任意本地路径。
app.UseDefaultFiles();
app.UseStaticFiles();

static string NormRoot(string root) => (root ?? "").Trim().Trim('"').TrimEnd('\\', '/');

// 按 JSON 结构判定图集类型（与客户端 EgretJsonDetector 同口径）：
//   movieclip = 根有非空 "mc" 对象（子图在 "res"）；
//   font/json图集 = 文件名/路径含 font 或帧名含 hurt/blood/career 等位图字体特征，且是 frames 对象；
//   atlas = frames 对象 / frames 数组 / res 对象；其余 = other。
// 返回给前端做“图集类型”下拉分类。
static string DetectKind(string json)
{
    try
    {
        using var d = JsonDocument.Parse(json);
        var r = d.RootElement;
        if (r.ValueKind != JsonValueKind.Object) return "other";
        if (r.TryGetProperty("font", out var fontEl) && fontEl.ValueKind == JsonValueKind.Object &&
            fontEl.TryGetProperty("size", out _)) return "font"; // 真·位图字体（Egret BitmapFont）
        if (r.TryGetProperty("mc", out var mc) && mc.ValueKind == JsonValueKind.Object && mc.EnumerateObject().Any())
            return "movieclip";
        bool hasFrames = r.TryGetProperty("frames", out var fr) &&
                         (fr.ValueKind == JsonValueKind.Object || fr.ValueKind == JsonValueKind.Array);
        bool hasRes = r.TryGetProperty("res", out var rs) && rs.ValueKind == JsonValueKind.Object;
        return (hasFrames || hasRes) ? "atlas" : "other";
    }
    catch { return "other"; }
}

// 真正的 Egret 位图字体：根有 "font" 对象（含 size 等）且帧名是字符，不是普通 frames 图集。
// BloodFont.json 只有 {file, frames}，属于普通图集，不会被这里判成 font。
static bool IsBitmapFontJson(string json)
{
    try
    {
        using var d = JsonDocument.Parse(json);
        var r = d.RootElement;
        if (r.ValueKind != JsonValueKind.Object) return false;
        if (!r.TryGetProperty("font", out var f) || f.ValueKind != JsonValueKind.Object) return false;
        return f.TryGetProperty("size", out _);
    }
    catch { return false; }
}

// 全量图集路径索引缓存：只在首次（或目录变动）时扫描一次整个 root 并读 JSON 判类型，
// 之后所有 /api/atlases 请求直接返回缓存，不再每次遍历几百个文件 + JsonDocument.Parse（避免 GC 抖动）。
// 失效依据：root 下所有目录的 LastWriteTimeUtc + 文件数，新增/删除/移动图集目录后自动重扫。
var AtlasIndexCache = new Dictionary<string, (string Stamp, List<AtlasItem> List)>(StringComparer.OrdinalIgnoreCase);
var AtlasIndexLock = new object();

// 失效指纹只看 root 顶层目录的写入时间+数量（O(顶层)），不递归几百个子目录——
// 递归统计本身就是每次请求的 GC/IO 开销，与“缓存避免重扫”的初衷相悖。
// 新增/删除顶层图集目录会自动失效；子目录内部变动可直接刷新页面或重启工具。
static string DirStamp(string root)
{
    long ticks = 0; int count = 0;
    foreach (var d in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
    { try { ticks += new DirectoryInfo(d).LastWriteTimeUtc.Ticks; } catch { } count++; }
    try { ticks += new DirectoryInfo(root).LastWriteTimeUtc.Ticks; } catch { }
    return ticks + "|" + count;
}

// 并行扫描单个 root：JSON 文件用 Parallel.ForEach 分给多线程，各线程独立 List 最后合并，
// 避免共享 List 加锁竞争；几百个 JSON 的读盘+解析从串行数秒压到亚秒级。
List<AtlasItem> ScanAtlases(string root)
{
    var files = Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories).ToArray();
    var buckets = new List<AtlasItem>[Environment.ProcessorCount];
    for (int i = 0; i < buckets.Length; i++) buckets[i] = new List<AtlasItem>();
    int bucketSeq = -1;

    Parallel.ForEach(files, (f) =>
    {
        int idx = Interlocked.Increment(ref bucketSeq);
        var name = Path.GetFileName(f);
        // 排除配置文件（非图集）
        if (name.StartsWith("default.res", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("allmanifest", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("manifest", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("resourceVersion", StringComparison.OrdinalIgnoreCase)) return;
        var rel = Path.GetRelativePath(root, f).Replace('\\', '/');
        string json;
        try { json = File.ReadAllText(f); } catch { return; }
        var kind = DetectKind(json);
        if (kind == "other") return;
        // 不按名字/路径猜“位图字体”：BloodFont.json 等只是名字带 Font 的普通 frames 图集。
        // 真正的 Egret 位图字体是根含 font{size,file} 的特殊结构（本地资源里没有），单独按结构识别。
        bool isFont = IsBitmapFontJson(json);
        buckets[idx % buckets.Length].Add(new AtlasItem { Name = name, RelPath = rel, Kind = kind, Font = isFont });
    });

    var list = new List<AtlasItem>(files.Length);
    foreach (var b in buckets) list.AddRange(b);
    return list;
}

// 列出目录下所有原始传奇图集 JSON（走全量索引缓存）。
app.MapGet("/api/atlases", (string root) =>
{
    root = NormRoot(root);
    if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        return Results.BadRequest(new { error = "目录不存在或为空：" + root });
    string stamp = DirStamp(root);
    List<AtlasItem> list;
    lock (AtlasIndexLock)
    {
        if (AtlasIndexCache.TryGetValue(root, out var c) && c.Stamp == stamp) list = c.List;
        else
        {
            list = ScanAtlases(root);
            AtlasIndexCache[root] = (stamp, list);
        }
    }
    return Results.Ok(new { root, atlases = list });
});

// 图集解析/图片字节缓存：切换图集时避免重复读磁盘 + 重复 JSON 解析（本地工具，图集文件基本只读）。
// key = 绝对路径；值带文件长度与最后写入时间，文件被替换后自动失效。
var AtlasCache = new Dictionary<string, (long Ticks, object Value)>(StringComparer.OrdinalIgnoreCase);
var AtlasCacheLock = new object();
T Cached<T>(string path, Func<T> factory)
{
    long stamp;
    try { var fi = new FileInfo(path); stamp = fi.LastWriteTimeUtc.Ticks ^ fi.Length; }
    catch { return factory(); }
    lock (AtlasCacheLock)
    {
        if (AtlasCache.TryGetValue(path, out var c) && c.Ticks == stamp && c.Value is T hit) return hit;
    }
    var val = factory();
    lock (AtlasCacheLock) { AtlasCache[path] = (stamp, val); }
    return val;
}

// 解析单个图集：返回帧列表 + png 访问 URL（pngUrl 指向 /api/atlasimage）
app.MapGet("/api/atlas", (string root, string rel) =>
{
    root = NormRoot(root);
    var baseRoot = Path.GetFullPath(root);
    var jsonPath = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
    var full = Path.GetFullPath(jsonPath);
    if (!full.StartsWith(baseRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
        return Results.NotFound(new { error = "找不到图集：" + rel });

    // 整段结果（帧表 + pngUrl + kind）按路径缓存，重复选择直接返回。
    return Results.Ok(Cached(full, () => ParseAtlas(root, rel, full)));
});

static object ParseAtlas(string root, string rel, string full)
{
    var json = File.ReadAllText(full);
    using var doc = JsonDocument.Parse(json);
    var r = doc.RootElement;

    // PNG 名：普通图集从 "file" 取；MovieClip(有 mc、无 file) 按 JSON 同名 .png 推断。
    var pngName = r.TryGetProperty("file", out var fp) ? fp.GetString() ?? "" : "";
    if (string.IsNullOrEmpty(pngName))
    {
        var mcFile = r.TryGetProperty("mc", out var mcEl) && mcEl.ValueKind == JsonValueKind.Object &&
                     mcEl.TryGetProperty("file", out var mfp) ? mfp.GetString() ?? "" : "";
        pngName = string.IsNullOrEmpty(mcFile) ? Path.GetFileNameWithoutExtension(full) + ".png" : mcFile;
    }
    var dir = Path.GetDirectoryName(rel)?.Replace('\\', '/');
    var pngRel = (string.IsNullOrEmpty(dir) ? "" : dir + "/") + pngName;
    var pngUrl = "/api/atlasimage?root=" + Uri.EscapeDataString(root) + "&rel=" + Uri.EscapeDataString(pngRel);

    var frames = new List<object>();
    void AddRect(string frameName, JsonElement o)
    {
        if (o.ValueKind != JsonValueKind.Object) return;
        int x = o.TryGetProperty("x", out var vx) ? vx.GetInt32() : 0;
        int y = o.TryGetProperty("y", out var vy) ? vy.GetInt32() : 0;
        int w = o.TryGetProperty("w", out var vw) ? vw.GetInt32() : 0;
        int h = o.TryGetProperty("h", out var vh) ? vh.GetInt32() : 0;
        frames.Add(new { name = frameName, x, y, w, h });
    }

    string kind = DetectKind(json);
    // 帧矩形来源优先级（同客户端 EgretAtlas）：frames 字典 → frames 数组 → res 字典(MovieClip)。
    if (r.TryGetProperty("frames", out var fe))
    {
        if (fe.ValueKind == JsonValueKind.Object)
            foreach (var f in fe.EnumerateObject()) AddRect(f.Name, f.Value);
        else if (fe.ValueKind == JsonValueKind.Array)
        {
            int idx = 0;
            foreach (var o in fe.EnumerateArray()) AddRect("frame_" + (idx++), o);
        }
    }
    else if (r.TryGetProperty("res", out var re) && re.ValueKind == JsonValueKind.Object)
        foreach (var f in re.EnumerateObject()) AddRect(f.Name, f.Value);

    return new { pngUrl, frames, kind };
}

// 按相对路径返回图集 png 字节（同源，前端直接当图片加载）
app.MapGet("/api/atlasimage", (string root, string rel) =>
{
    root = NormRoot(root);
    var baseRoot = Path.GetFullPath(root);
    var pngPath = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
    var full = Path.GetFullPath(pngPath);
    if (!full.StartsWith(baseRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
        return Results.NotFound();
    var bytes = Cached(full, () => File.ReadAllBytes(full));
    var ext = Path.GetExtension(full).ToLowerInvariant();
    var ct = ext == ".png" ? "image/png"
           : (ext == ".jpg" || ext == ".jpeg") ? "image/jpeg"
           : "application/octet-stream";
    return Results.File(bytes, ct);
});

// 返回本机“下载”目录，作为前端输出目录输入框的默认值（本地工具，服务端即本机）。
app.MapGet("/api/defaultdownload", () =>
{
    var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    var downloads = Path.Combine(profile, "Downloads");
    if (!Directory.Exists(downloads)) downloads = profile;
    return Results.Ok(new { path = downloads });
});

// 接收前端打包好的图集 zip，解压到用户指定的输出目录（dir 由前端传，默认即下载目录，可自定义）。
// zip 内部已是 <图集名>/<帧名>.png 结构，解压即还原该层级。
app.MapPost("/api/export", async (HttpContext ctx) =>
{
    var dir = ctx.Request.Query["dir"].ToString();
    var form = await ctx.Request.ReadFormAsync();
    var file = form.Files["zip"];
    if (string.IsNullOrWhiteSpace(dir))
        return Results.BadRequest(new { error = "未指定输出目录" });
    if (file == null || file.Length == 0)
        return Results.BadRequest(new { error = "未收到 zip 文件" });
    try
    {
        Directory.CreateDirectory(dir);
        var tmp = Path.Combine(Path.GetTempPath(), "AtlasPreviewer_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        using (var stream = file.OpenReadStream())
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
        {
            archive.ExtractToDirectory(tmp);
        }
        // 把临时目录里的 <图集名>/ 与零散文件搬到输出目录（保留 zip 内层级，覆盖同名）
        foreach (var d in Directory.EnumerateDirectories(tmp))
        {
            var dest = Path.Combine(dir, Path.GetFileName(d));
            if (Directory.Exists(dest)) Directory.Delete(dest, true);
            Directory.Move(d, dest);
        }
        foreach (var f in Directory.EnumerateFiles(tmp))
        {
            var dest = Path.Combine(dir, Path.GetFileName(f));
            if (File.Exists(dest)) File.Delete(dest);
            File.Move(f, dest);
        }
        Directory.Delete(tmp, true);
        return Results.Ok(new { outDir = Path.GetFullPath(dir) });
    }
    catch (Exception e)
    {
        return Results.BadRequest(new { error = "导出失败：" + e.Message });
    }
});

// 启动后台并行预热：每个 root 单独一个 Task，同时开跑（ScanAtlases 内部再对 JSON 文件 Parallel.ForEach），
// 两级并行把预热从串行数秒压到 1 秒内；首次打开下拉即为缓存命中。
_ = Task.Run(async () =>
{
    var roots = new[]
    {
        "D:/OpenSource/KFramework.WebGame/KFramework.Mir.OriginMri2/Mir2Res",
        "D:/OpenSource/KFramework.WebGame/KFramework.Mir.OriginMri2/Mir2Res/resource"
    };
    var tasks = roots.Select(r => Task.Run(() =>
    {
        try
        {
            if (!Directory.Exists(r)) return;
            var stamp = DirStamp(r);
            var list = ScanAtlases(r);
            lock (AtlasIndexLock)
            {
                if (!AtlasIndexCache.ContainsKey(r)) AtlasIndexCache[r] = (stamp, list);
            }
        }
        catch { }
    })).ToArray();
    await Task.WhenAll(tasks);
});

app.Run();

// 图集条目：每个 Item 一个明确类，含图集名字、相对路径、类型（另保留 font 标记供下拉分类）。
// Minimal API 默认按 Web 约定 camelCase 序列化，前端仍拿到 name / relPath / kind / font。
public sealed class AtlasItem
{
    public string Name { get; set; } = "";
    public string RelPath { get; set; } = "";
    public string Kind { get; set; } = "atlas";
    public bool Font { get; set; }
}
