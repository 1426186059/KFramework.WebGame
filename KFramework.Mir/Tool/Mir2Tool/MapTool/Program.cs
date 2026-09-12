using MapTool.Models;
using MapTool.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<MapToolService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");

// 路径配置
api.MapGet("/config", (MapToolService svc) => Results.Ok(svc.GetConfig()));
api.MapPost("/config", (MapToolConfig cfg, MapToolService svc) => Results.Ok(svc.UpdateConfig(cfg)));
api.MapGet("/paths/check", (MapToolService svc) => Results.Ok(svc.CheckPaths()));

// MirDB
api.MapPost("/mirdb/load", (MapToolService svc) => Results.Ok(svc.LoadMirDB()));

// 扫描 / 提取
api.MapPost("/scan", (MapToolService svc) => Results.Ok(svc.Scan()));
api.MapPost("/extract", (ExtractRequest req, MapToolService svc) => Results.Ok(svc.Extract(req.Maps ?? new List<string>())));

// 小地图预览：kind = original | rendered
api.MapGet("/preview", (string map, string? kind, MapToolService svc) =>
{
    bool rendered = string.Equals(kind, "rendered", StringComparison.OrdinalIgnoreCase);
    var bytes = svc.GetPreview(map, rendered);
    if (bytes == null)
        return Results.NotFound($"未找到预览图片: {svc.GetPreviewPath(map, rendered)}");
    return Results.File(bytes, "image/png");
});

// 目录浏览（替代 Unity 的 OpenFolderPanel / OpenFilePanel）
api.MapGet("/fs", (string? path, bool? files, string? filter, MapToolService svc) =>
    Results.Ok(svc.ListDir(path, files ?? false, filter)));

app.Run();
