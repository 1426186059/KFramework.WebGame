using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using MirDBToJsonWeb;

// 显式把内容根固定到程序所在目录：无论以 dotnet run 还是直接双击 bin 里的 exe 启动，
// 都能稳定找到 bin/wwwroot（由 csproj 的 CopyWwwrootToOutput 复制），避免落到工作目录导致 404。
var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = exeDir, Args = args });
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// 记录最近一次解析来源，供 /api/info 与数据来源页（sources.html）展示
JsonObject? lastInfo = null;

// ---------- 解析上传的 Server.MirDB（multipart/form-data，字段名 db） ----------
app.MapPost("/api/parse", async (HttpContext ctx) =>
{
    var form = await ctx.Request.ReadFormAsync();
    var file = form.Files["db"];
    if (file == null || file.Length == 0) return Results.BadRequest(new { error = "未收到文件" });

    using var ms = new MemoryStream();
    await file.CopyToAsync(ms);
    ms.Position = 0;
    try
    {
        var result = MirDBParser.Parse(ms, "upload", file.FileName, file.Length);
        lastInfo = BuildInfo(result, file.FileName, "浏览器上传");
        return Results.Ok(result);
    }
    catch (Exception ex) { return Results.Problem($"{ex.GetType().Name}: {ex.Message}"); }
});

// ---------- 按本地路径解析 Server.MirDB（GET ?path=... 或 POST { "path": "..." }） ----------
string? ReadPath(HttpContext ctx)
{
    var q = ctx.Request.Query["path"].ToString();
    if (!string.IsNullOrEmpty(q)) return q;
    return null;
}

app.MapGet("/api/parse-path", (HttpContext ctx) => ParsePath(ctx, ReadPath(ctx)));
app.MapPost("/api/parse-path", async (HttpContext ctx) =>
{
    string? p = ReadPath(ctx);
    if (string.IsNullOrEmpty(p))
    {
        using var doc = await JsonDocument.ParseAsync(ctx.Request.Body);
        if (doc.RootElement.TryGetProperty("path", out var pe)) p = pe.GetString();
    }
    return ParsePath(ctx, p);
});

IResult ParsePath(HttpContext ctx, string? p)
{
    if (string.IsNullOrEmpty(p) || !File.Exists(p)) return Results.BadRequest(new { error = "文件不存在或路径为空" });

    try
    {
        using var fs = File.OpenRead(p);
        var result = MirDBParser.Parse(fs, "path", p, new FileInfo(p).Length);
        lastInfo = BuildInfo(result, p, "本地路径");
        return Results.Ok(result);
    }
    catch (Exception ex) { return Results.Problem($"{ex.GetType().Name}: {ex.Message}"); }
}

// ---------- 数据来源（供前端与 sources.html 实时展示“数据从哪来”） ----------
app.MapGet("/api/info", () =>
{
    if (lastInfo == null)
        return Results.Ok(new
        {
            loaded = false,
            note = "尚未解析任何 MirDB 文件。请在首页上传 Server.MirDB，或填入本地路径后点击「从路径解析」。"
        });
    return Results.Ok(new { loaded = true, info = lastInfo });
});

app.Run();

static JsonObject BuildInfo(ParseResult r, string file, string sourceType)
{
    return new JsonObject
    {
        ["sourceType"] = sourceType,
        ["fileName"] = file,
        ["sizeBytes"] = r.sizeBytes,
        ["version"] = r.meta.version,
        ["customVersion"] = r.meta.customVersion,
        ["parsedAt"] = r.parsedAt.ToString("yyyy-MM-dd HH:mm:ss"),
        ["mapCount"] = r.mapCount,
        ["itemCount"] = r.itemCount,
        ["monsterCount"] = r.monsterCount,
        ["npcCount"] = r.npcCount,
        ["questCount"] = r.questCount,
        ["magicCount"] = r.magicCount,
        ["gameShopCount"] = r.gameShopCount,
        ["conquestCount"] = r.conquestCount,
        ["note"] = "解析自 Server.MirDB 二进制数据库（与控制台版 MirDBToJson 同一套官方 Server.MirDatabase 反序列化器）。"
    };
}
