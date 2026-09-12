using TextureToWebP.Models;
using TextureToWebP.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<TextureService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");

// 配置
api.MapGet("/config", (TextureService svc) => Results.Ok(svc.GetConfig()));
api.MapPost("/config", (TextureConfig cfg, TextureService svc) => Results.Ok(svc.UpdateConfig(cfg)));

// 扫描待转换图片
api.MapPost("/scan", (TextureService svc) => Results.Ok(svc.Scan()));

// 转换 / 取消 / 进度
api.MapPost("/convert", (TextureService svc) => Results.Ok(svc.Start()));
api.MapPost("/cancel", (TextureService svc) =>
{
    svc.Cancel();
    return Results.Ok(new OpResultDto { Ok = true, Message = "已请求取消。" });
});
api.MapGet("/status", (TextureService svc) => Results.Ok(svc.GetStatus()));

// 目录浏览
api.MapGet("/fs", (string? path, TextureService svc) => Results.Ok(svc.ListDir(path)));

app.Run();
