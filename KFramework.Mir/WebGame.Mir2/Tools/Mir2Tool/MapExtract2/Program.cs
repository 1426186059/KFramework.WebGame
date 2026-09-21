using System.Diagnostics;
using MapExtract2.Models;
using MapExtract2.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<MapExtract2Service>();

var app = builder.Build();

// 启动后自动用默认浏览器打开页面（本机使用更顺手；无界面环境打开失败则忽略）
app.Lifetime.ApplicationStarted.Register(() =>
{
    try
    {
        var url = app.Urls.FirstOrDefault(u => u.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                 ?? app.Urls.FirstOrDefault();
        if (!string.IsNullOrEmpty(url))
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }
    catch
    {
        // 无浏览器 / 打开失败：忽略
    }
});

app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");

// 路径配置
api.MapGet("/config", (MapExtract2Service svc) => Results.Ok(svc.GetConfig()));
api.MapPost("/config", (MapExtract2Config cfg, MapExtract2Service svc) => Results.Ok(svc.UpdateConfig(cfg)));
api.MapGet("/paths/check", (MapExtract2Service svc) => Results.Ok(svc.CheckPaths()));

// 客户端资源根目录 → 相对路径自动探测（Map / Data\Map / Data\mmap.Lib / Server.MirDB）
api.MapGet("/detect", (string? root, MapExtract2Service svc) => Results.Ok(svc.Detect(root)));

// MirDB
api.MapPost("/mirdb/load", (MapExtract2Service svc) => Results.Ok(svc.LoadMirDB()));

// 扫描 / 提取（提取 = 蒸馏 Lib → 按地图打包 AssetBundle）
api.MapPost("/scan", (MapExtract2Service svc) => Results.Ok(svc.Scan()));
api.MapPost("/extract", (ExtractRequest req, MapExtract2Service svc) => Results.Ok(svc.Extract(req.Maps ?? new List<string>())));

// 单图产物信息（右侧「产物信息」卡片）
api.MapGet("/result", (string map, MapExtract2Service svc) => Results.Ok(svc.GetResult(map)));

// 目录浏览（替代 Unity 的 OpenFolderPanel / OpenFilePanel）
api.MapGet("/fs", (string? path, bool? files, string? filter, MapExtract2Service svc) =>
    Results.Ok(svc.ListDir(path, files ?? false, filter)));

app.Run();
