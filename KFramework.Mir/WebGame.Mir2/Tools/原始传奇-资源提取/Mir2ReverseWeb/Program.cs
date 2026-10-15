using Mir2ReverseWeb.Models;
using Mir2ReverseWeb.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<ReverseService>();

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
    cwd = Directory.GetCurrentDirectory()
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
api.MapPost("/map/load", async (ReverseService s) =>
    Results.Ok(await s.LoadMapDataAsync()));
api.MapPost("/map/parse", (PathReq req, ReverseService s) =>
    Results.Ok(s.ParseMapDat(req.Path)));
api.MapPost("/map/decode", (MapDecodeRequest req, ReverseService s) =>
    Results.Ok(s.DecodeMap(req)));
api.MapPost("/map/export", (MapExportRequest req, ReverseService s) =>
    Results.Ok(s.ExportMaps(req)));

// ---- 图集 WIL/WIX ----
api.MapPost("/atlas/scan", (PathReq req, ReverseService s) =>
    Results.Ok(s.ScanAtlases(req.Path)));
api.MapPost("/atlas/info", (PathReq req, ReverseService s) =>
    Results.Ok(s.ReadAtlas(req.Path)));

app.Run();
