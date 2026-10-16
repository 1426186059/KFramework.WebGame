using Mir2ReverseWeb.Models;
using Mir2ReverseWeb.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<ReverseService>();
builder.Services.AddSingleton<MapTileService>();
builder.Services.AddSingleton<MapMinimapService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");

// 健康检查
api.MapGet("/ping", () => Results.Ok(new { ok = true, time = DateTime.Now }));

// 运行信息
api.MapGet("/info", () => Results.Ok(new
{
    os = Environment.OSVersion.ToString(),
    framework = Environment.Version.ToString(),
    cwd = Directory.GetCurrentDirectory(),
    downloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
}));

// 文件系统浏览（替代 Unity 的 OpenFolderPanel / OpenFilePanel）
api.MapGet("/fs", (string? path, bool? files, string? filter, ReverseService s) =>
    Results.Ok(s.ListDir(path, files ?? false, filter)));

// 通用扫描（按扩展名）
api.MapPost("/scan", (ScanReq req, ReverseService s) =>
    Results.Ok(s.Scan(req.Root, req.Exts ?? new List<string>())));

// 文件信息
api.MapPost("/file/info", (PathReq req, ReverseService s) =>
    Results.Ok(s.GetFileInfo(req.Path)));

// ---- 地图 .dat ----
// 自动下载（按 Config.cs 中的 URL）地图包 + 配置，解析索引并合并地图名/宽高
api.MapPost("/map/load", async (MapLoadRequest req, ReverseService s) =>
    Results.Ok(await s.LoadMapDataAsync(req.DatUrl)));
api.MapPost("/map/parse", (PathReq req, ReverseService s) =>
    Results.Ok(s.ParseMapDat(req.Path)));
api.MapPost("/map/decode", (MapDecodeRequest req, ReverseService s) =>
    Results.Ok(s.DecodeMap(req)));
api.MapPost("/map/export", (MapExportRequest req, ReverseService s) =>
    Results.Ok(s.ExportMaps(req)));

// 真实地图瓦片（zipmap/{img}/{index}.jpg）批量下载：启动异步任务，立即返回 jobId 供前端轮询
api.MapPost("/map/downloadTiles", (MapTileDownloadRequest req, MapTileService s) =>
{
    if (req.Maps == null || req.Maps.Count == 0)
        return Results.Ok(new MapTileJobResult { Ok = false, Error = "未选择地图" });
    var dest = string.IsNullOrWhiteSpace(req.Destination) ? ReverseService.OsDownloadDir() : req.Destination;
    var id = s.Start(req.Maps, dest, req.Stitch);
    return Results.Ok(new MapTileJobResult { Ok = true, JobId = id.ToString(), TotalMaps = req.Maps.Count });
});

// 轮询真实地图瓦片下载进度（真实百分比）
api.MapGet("/map/tilesProgress", (string? job, MapTileService s) =>
{
    if (string.IsNullOrWhiteSpace(job) || !Guid.TryParse(job, out var gid))
        return Results.Ok(new { ok = false, error = "bad job" });
    var p = s.GetProgress(gid);
    return p == null ? Results.Ok(new { ok = false, error = "unknown job" }) : Results.Ok(p);
});

// 预览真实地图（基于已下载的 zipmap 瓦片拼接缩略 PNG）
api.MapPost("/map/previewReal", (MapRealPreviewRequest req, MapTileService s) =>
{
    var dest = string.IsNullOrWhiteSpace(req.Destination) ? ReverseService.OsDownloadDir() : req.Destination;
    var m = new MapEntry { Id = req.Id, Img = req.Img, Name = req.Name, Width = req.Width, Height = req.Height };
    var r = s.PreviewReal(m, dest);
    return Results.Ok(new
    {
        ok = r.Ok,
        error = r.Error,
        previewPngBase64 = r.Base64 ?? "",
        tiles = r.Tiles,
        cols = r.Cols,
        rows = r.Rows
    });
});

// 提取小地图：从 minimaps.png 图集按 {img}_png 帧裁出每幅地图的小地图 PNG
api.MapPost("/map/minimap", (MapMinimapRequest req, MapMinimapService s) =>
{
    if (req.Maps == null || req.Maps.Count == 0)
        return Results.Ok(new { ok = false, error = "未选择地图" });
    var dest = string.IsNullOrWhiteSpace(req.Destination) ? ReverseService.OsDownloadDir() : req.Destination;
    int ok = 0;
    var results = new List<object>();
    foreach (var m in req.Maps)
    {
        var r = s.Extract(dest, m.Id, m.Img, m.Name);
        if (r.Ok) ok++;
        results.Add(new { id = m.Id, name = m.Name, ok = r.Ok, file = r.File ?? "", error = r.Error ?? "" });
    }
    return Results.Ok(new { ok = true, extracted = ok, total = req.Maps.Count, results });
});

// 预览小地图：优先取 minimap/{img}.jpg 独立整图，缺则回退 minimaps 图集 img_png 帧（再回退 43005_png）
api.MapPost("/map/previewMinimap", (MapMinimapPreviewRequest req, MapMinimapService s) =>
{
    var r = s.Preview(req.Id, req.Img, req.Name);
    return Results.Ok(new
    {
        ok = r.Ok,
        error = r.Error,
        previewPngBase64 = r.Base64 ?? "",
        w = r.W,
        h = r.H,
        source = r.Source
    });
});

// ---- 图集 WIL/WIX ----
api.MapPost("/atlas/scan", (PathReq req, ReverseService s) =>
    Results.Ok(s.ScanAtlases(req.Path)));
api.MapPost("/atlas/info", (PathReq req, ReverseService s) =>
    Results.Ok(s.ReadAtlas(req.Path)));

// ---- 图集（manifest 模式）：加载清单 → 单条/全部下载 ----
api.MapPost("/atlas/manifest", async (ReverseService s) =>
    Results.Ok(await s.LoadManifestAsync()));
api.MapPost("/atlas/download", async (AtlasDownloadRequest req, ReverseService s) =>
    Results.Ok(await s.DownloadManifestAsset(req.Key, req.OutDir)));
api.MapPost("/atlas/downloadAll", async (AtlasDownloadRequest req, ReverseService s) =>
    Results.Ok(await s.DownloadManifestAll(req.OutDir)));

// ---- 配置表（0config*.json 顶层每个键 = 一张配置表）----
// 加载所有配置分片到内存（不落盘），解析出全部表
api.MapPost("/config/load", async (ReverseService s) =>
    Results.Ok(await s.LoadConfigTablesAsync()));

// 枚举已加载的表（名 / 形态 / 条目数 / 字段列表与推断类型）
api.MapPost("/config/tables", (ReverseService s) =>
    Results.Ok(s.ListConfigTables()));

// 提取指定表（或 "*" 全部）为 C# 类 + JSON 数据，写出到输出目录
api.MapPost("/config/extract", (ConfigExtractRequest req, ReverseService s) =>
    Results.Ok(s.ExtractConfigTables(req)));

app.Run();
