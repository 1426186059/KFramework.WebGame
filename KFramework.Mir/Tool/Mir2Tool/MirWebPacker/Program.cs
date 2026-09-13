using MirWebPacker.Models;
using MirWebPacker.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<MirWebPackerService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");

// 目录浏览（替代浏览器的 OpenFolderPanel，用于在网页上选择资源根目录）
api.MapGet("/fs", (string? path, bool? files, string? filter, MirWebPackerService svc) =>
    Results.Ok(svc.ListDir(path, files ?? false, filter)));

// 打包：把 root 下每个子目录分别打成 .web.lib，并生成总 version.manifest
api.MapPost("/pack", (PackRootRequest req, MirWebPackerService svc) =>
    Results.Ok(svc.PackRoot(req)));

// 下载生成的 .web.lib / version.manifest
api.MapGet("/file/{**name}", (string name, MirWebPackerService svc) =>
{
    var fp = svc.ResolveFile(name);
    if (fp == null) return Results.NotFound($"文件不存在: {name}");
    string ct = name.EndsWith(".manifest", StringComparison.OrdinalIgnoreCase)
        ? "application/json"
        : "application/octet-stream";
    return Results.File(fp, ct, name);
});

app.Run();
