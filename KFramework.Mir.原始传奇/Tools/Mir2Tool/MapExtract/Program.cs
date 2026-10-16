using MapExtract.Models;
using MapExtract.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<MapExtractService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");

// 路径配置
api.MapGet("/config", (MapExtractService svc) => Results.Ok(svc.GetConfig()));
api.MapPost("/config", (MapExtractConfig cfg, MapExtractService svc) => Results.Ok(svc.UpdateConfig(cfg)));
api.MapGet("/paths/check", (MapExtractService svc) => Results.Ok(svc.CheckPaths()));

// MirDB
api.MapPost("/mirdb/load", (MapExtractService svc) => Results.Ok(svc.LoadMirDB()));

// 客户端资源根目录 → 相对路径自动探测（Map / Data\Map / Data\mmap / Server.MirDB）
api.MapGet("/detect", (string? root, MapExtractService svc) => Results.Ok(svc.Detect(root)));

// 扫描 / 提取（素材按需从 .Lib 抽单张，不做整库导出）
api.MapPost("/scan", (MapExtractService svc) => Results.Ok(svc.Scan()));
api.MapPost("/extract", (ExtractRequest req, MapExtractService svc) => Results.Ok(svc.Extract(req.Maps ?? new List<string>())));

// 小地图预览：kind = original | rendered
api.MapGet("/preview", (string map, string? kind, MapExtractService svc) =>
{
    bool rendered = string.Equals(kind, "rendered", StringComparison.OrdinalIgnoreCase);
    var bytes = svc.GetPreview(map, rendered);
    if (bytes == null)
        return Results.NotFound($"未找到预览图片: {svc.GetPreviewPath(map, rendered)}");
    return Results.File(bytes, "image/png");
});

// 目录浏览（替代 Unity 的 OpenFolderPanel / OpenFilePanel）
api.MapGet("/fs", (string? path, bool? files, string? filter, MapExtractService svc) =>
    Results.Ok(svc.ListDir(path, files ?? false, filter)));

app.Run();
