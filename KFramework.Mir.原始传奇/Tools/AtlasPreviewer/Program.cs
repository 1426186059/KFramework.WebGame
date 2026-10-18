using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// 用户在页面粘贴“本地图集文件夹路径”，服务端（本机运行的 dotnet）直接读该路径，
// 列出 / 解析 *-sheet.json 并返回同目录 png。无需浏览器选文件夹、也无需把文件拷进工程。
// 这是本地开发工具，服务端即用户本机，故可读任意本地路径。
app.UseDefaultFiles();
app.UseStaticFiles();

static string NormRoot(string root) => (root ?? "").Trim().Trim('"').TrimEnd('\\', '/');

// 列出目录下所有 *-sheet.json 图集
app.MapGet("/api/atlases", (string root) =>
{
    root = NormRoot(root);
    if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        return Results.BadRequest(new { error = "目录不存在或为空：" + root });
    var list = new List<object>();
    foreach (var f in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories))
    {
        var name = Path.GetFileName(f);
        if (!name.EndsWith("-sheet.json", StringComparison.OrdinalIgnoreCase)) continue;
        var rel = Path.GetRelativePath(root, f).Replace('\\', '/');
        list.Add(new { name, relPath = rel });
    }
    return Results.Ok(new { root, atlases = list });
});

// 解析单个图集：返回帧列表 + png 访问 URL（pngUrl 指向 /api/atlasimage）
app.MapGet("/api/atlas", (string root, string rel) =>
{
    root = NormRoot(root);
    var baseRoot = Path.GetFullPath(root);
    var jsonPath = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
    var full = Path.GetFullPath(jsonPath);
    if (!full.StartsWith(baseRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
        return Results.NotFound(new { error = "找不到图集：" + rel });

    var json = File.ReadAllText(full);
    using var doc = JsonDocument.Parse(json);
    var r = doc.RootElement;
    var pngName = r.TryGetProperty("file", out var fp) ? fp.GetString() ?? "" : "";
    var dir = Path.GetDirectoryName(rel)?.Replace('\\', '/');
    var pngRel = (string.IsNullOrEmpty(dir) ? "" : dir + "/") + pngName;
    var pngUrl = string.IsNullOrEmpty(pngName)
        ? ""
        : "/api/atlasimage?root=" + Uri.EscapeDataString(root) + "&rel=" + Uri.EscapeDataString(pngRel);

    var frames = new List<object>();
    if (r.TryGetProperty("frames", out var fe) && fe.ValueKind == JsonValueKind.Object)
    {
        foreach (var f in fe.EnumerateObject())
        {
            var o = f.Value;
            int x = o.TryGetProperty("x", out var vx) ? vx.GetInt32() : 0;
            int y = o.TryGetProperty("y", out var vy) ? vy.GetInt32() : 0;
            int w = o.TryGetProperty("w", out var vw) ? vw.GetInt32() : 0;
            int h = o.TryGetProperty("h", out var vh) ? vh.GetInt32() : 0;
            frames.Add(new { name = f.Name, x, y, w, h });
        }
    }
    return Results.Ok(new { pngUrl, frames });
});

// 按相对路径返回图集 png 字节（同源，前端直接当图片加载）
app.MapGet("/api/atlasimage", (string root, string rel) =>
{
    root = NormRoot(root);
    var baseRoot = Path.GetFullPath(root);
    var pngPath = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
    var full = Path.GetFullPath(pngPath);
    if (!full.StartsWith(baseRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
        return Results.NotFound();
    var bytes = File.ReadAllBytes(full);
    var ext = Path.GetExtension(full).ToLowerInvariant();
    var ct = ext == ".png" ? "image/png"
           : (ext == ".jpg" || ext == ".jpeg") ? "image/jpeg"
           : "application/octet-stream";
    return Results.File(bytes, ct);
});

app.Run();
