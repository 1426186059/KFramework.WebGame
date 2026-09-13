using LibraryExtract.Models;
using LibraryExtract.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<LibraryExtractService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");

// 路径配置
api.MapGet("/config", (LibraryExtractService svc) => Results.Ok(svc.GetConfig()));
api.MapPost("/config", (LibraryExtractConfig cfg, LibraryExtractService svc) => Results.Ok(svc.UpdateConfig(cfg)));

// 扫描待转换的 .Lib 文件
api.MapPost("/scan", (LibraryExtractService svc) => Results.Ok(svc.Scan()));

// 开始 / 取消 / 查询进度
api.MapPost("/convert", (LibraryExtractService svc) => Results.Ok(svc.Start()));
api.MapPost("/cancel", (LibraryExtractService svc) =>
{
    svc.Cancel();
    return Results.Ok(new StartResultDto { Ok = true, Message = "已请求取消。" });
});
api.MapGet("/status", (LibraryExtractService svc) => Results.Ok(svc.GetStatus()));

// 目录浏览（替代 WinForms 的 FolderBrowserDialog / OpenFileDialog）
api.MapGet("/fs", (string? path, bool? files, string? filter, LibraryExtractService svc) =>
    Results.Ok(svc.ListDir(path, files ?? false, filter)));

app.Run();
