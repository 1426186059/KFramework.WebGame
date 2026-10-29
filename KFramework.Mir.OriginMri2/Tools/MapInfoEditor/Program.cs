using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using MapInfoEditor;

// 显式把内容根固定到程序所在目录：无论以 dotnet run 还是直接双击 bin 里的 exe 启动，
// 都能稳定找到 bin/wwwroot（由 csproj 的 CopyWwwrootToOutput 复制），避免落到工作目录导致 404。
var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = exeDir, Args = args });
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

var store = new MapInfoStore();
try
{
    store.Load();
    Console.WriteLine($"MapInfoEditor: 已加载 {store.Maps.Count} 张地图, {store.Monsters.Count} 个怪物");
}
catch (Exception ex)
{
    Console.WriteLine("MapInfoEditor 加载失败: " + ex.Message);
}

// ---------- 地图列表 ----------
app.MapGet("/api/maps", () =>
{
    var list = store.Maps.OrderBy(m => m.Index)
        .Select(m => new { index = m.Index, fileName = m.FileName, title = m.Title })
        .ToList();
    return Results.Ok(new { count = list.Count, maps = list });
});

// ---------- 怪物列表（用于下拉与名称显示） ----------
app.MapGet("/api/monsters", () =>
{
    var list = store.Monsters.OrderBy(k => k.Key)
        .Select(k => new { index = k.Key, name = k.Value.Name, isBoss = k.Value.IsBoss })
        .ToList();
    return Results.Ok(new { monsters = list });
});

// ---------- 单张地图详情 + 解码网格 ----------
app.MapGet("/api/maps/{id:int}", (int id) =>
{
    var e = store.GetMap(id);
    if (e == null) return Results.NotFound(new { error = "未找到地图 id=" + id });

    var data = store.DecodeMap(id);
    string flagsB64 = "";
    int cols = 0, rows = 0, walkable = 0;
    if (data != null)
    {
        cols = data.Cols; rows = data.Rows; walkable = data.Walkable;
        flagsB64 = Convert.ToBase64String(data.CellFlags);
    }

    var general = new
    {
        title = e.Node["Title"]?.GetValue<string>() ?? "",
        fileName = e.Node["FileName"]?.GetValue<string>() ?? "",
        miniMap = e.Node["MiniMap"]?.GetValue<int>() ?? 0,
        bigMap = e.Node["BigMap"]?.GetValue<int>() ?? 0,
        music = e.Node["Music"]?.GetValue<int>() ?? 0,
        light = e.Node["Light"]?.GetValue<int>() ?? 0,
        noTeleport = e.Node["NoTeleport"]?.GetValue<bool>() ?? false,
        noReconnect = e.Node["NoReconnect"]?.GetValue<bool>() ?? false,
        noRecall = e.Node["NoRecall"]?.GetValue<bool>() ?? false,
        noDrug = e.Node["NoDrug"]?.GetValue<bool>() ?? false,
        noFight = e.Node["NoFight"]?.GetValue<bool>() ?? false,
        noDropMonster = e.Node["NoDropMonster"]?.GetValue<bool>() ?? false,
        noNames = e.Node["NoNames"]?.GetValue<bool>() ?? false,
        noMount = e.Node["NoMount"]?.GetValue<bool>() ?? false,
        noGroup = e.Node["NoGroup"]?.GetValue<bool>() ?? false,
        requiredGroup = e.Node["RequiredGroup"]?.GetValue<bool>() ?? false,
        fight = e.Node["Fight"]?.GetValue<bool>() ?? false,
        fire = e.Node["Fire"]?.GetValue<bool>() ?? false,
        noTownTeleport = e.Node["NoTownTeleport"]?.GetValue<bool>() ?? false,
        gt = e.Node["GT"]?.GetValue<bool>() ?? false,
        noReconnectMap = e.Node["NoReconnectMap"]?.GetValue<string>() ?? ""
    };

    var safeZones = store.GetSafeZones(e)
        .Select(z => new { x = z.X, y = z.Y, size = z.Size, start = z.Start }).ToList();

    var respawns = store.GetRespawns(e).Select(r =>
    {
        string mname = store.Monsters.TryGetValue(r.MonsterIndex, out var mm) ? mm.Name : "";
        return new
        {
            monsterIndex = r.MonsterIndex,
            x = r.X, y = r.Y, count = r.Count, spread = r.Spread, delay = r.Delay, direction = r.Direction,
            isBoss = store.IsBoss(r.MonsterIndex, mname)
        };
    }).ToList();

    // NPC 来自全局 NPC 表（按 MapIndex 关联），不放在 MapInfo_Fix 里
    var npcs = store.GetNpcs(e.Index)
        .Select(n => new { index = n.Index, fileName = n.FileName, name = n.Name, x = n.X, y = n.Y, rate = n.Rate, image = n.Image }).ToList();

    var movements = store.GetMovements(e).Select(mv => new
    {
        destMapIndex = mv.DestMapIndex,
        sourceX = mv.SourceX, sourceY = mv.SourceY,
        destX = mv.DestX, destY = mv.DestY,
        needHole = mv.NeedHole, needMove = mv.NeedMove
    }).ToList();

    return Results.Ok(new
    {
        index = e.Index,
        fileName = e.FileName,
        title = e.Title,
        general,
        safeZones,
        respawns,
        npcs,
        movements,
        map = new { cols, rows, walkable, flagsBase64 = flagsB64 }
    });
});

// ---------- 保存编辑（整体替换安全区/怪物/NPC + 常规字段，其余节点原样保留） ----------
app.MapPost("/api/maps/{id:int}", async (int id, HttpContext ctx) =>
{
    var e = store.GetMap(id);
    if (e == null) return Results.NotFound(new { error = "未找到地图 id=" + id });

    using var doc = await JsonDocument.ParseAsync(ctx.Request.Body);
    var body = doc.RootElement;

    // 常规字段
    var g = body.GetProperty("general");
    e.Node["Title"] = JsonValue.Create(g.GetProperty("title").GetString() ?? "");
    e.Node["MiniMap"] = JsonValue.Create(g.GetProperty("miniMap").GetInt32());
    e.Node["BigMap"] = JsonValue.Create(g.GetProperty("bigMap").GetInt32());
    e.Node["Music"] = JsonValue.Create(g.GetProperty("music").GetInt32());
    e.Node["Light"] = JsonValue.Create(g.GetProperty("light").GetInt32());
    e.Node["NoTeleport"] = JsonValue.Create(g.GetProperty("noTeleport").GetBoolean());
    e.Node["NoReconnect"] = JsonValue.Create(g.GetProperty("noReconnect").GetBoolean());
    e.Node["NoRecall"] = JsonValue.Create(g.GetProperty("noRecall").GetBoolean());
    e.Node["NoDrug"] = JsonValue.Create(g.GetProperty("noDrug").GetBoolean());
    e.Node["NoFight"] = JsonValue.Create(g.GetProperty("noFight").GetBoolean());
    e.Node["NoDropMonster"] = JsonValue.Create(g.GetProperty("noDropMonster").GetBoolean());
    e.Node["NoNames"] = JsonValue.Create(g.GetProperty("noNames").GetBoolean());
    e.Node["NoMount"] = JsonValue.Create(g.GetProperty("noMount").GetBoolean());
    e.Node["NoGroup"] = JsonValue.Create(g.GetProperty("noGroup").GetBoolean());
    e.Node["RequiredGroup"] = JsonValue.Create(g.GetProperty("requiredGroup").GetBoolean());
    e.Node["Fight"] = JsonValue.Create(g.GetProperty("fight").GetBoolean());
    e.Node["Fire"] = JsonValue.Create(g.GetProperty("fire").GetBoolean());
    e.Node["NoTownTeleport"] = JsonValue.Create(g.GetProperty("noTownTeleport").GetBoolean());
    e.Node["GT"] = JsonValue.Create(g.GetProperty("gt").GetBoolean());
    e.Node["NoReconnectMap"] = JsonValue.Create(g.GetProperty("noReconnectMap").GetString() ?? "");

    // 安全区
    ReplaceArray(e, "SafeZones", body.GetProperty("safeZones"), (arr, it) =>
    {
        arr.Add(new JsonObject
        {
            ["Location"] = new JsonObject { ["X"] = JsonValue.Create(it.GetProperty("x").GetInt32()), ["Y"] = JsonValue.Create(it.GetProperty("y").GetInt32()) },
            ["Size"] = JsonValue.Create(it.GetProperty("size").GetInt32()),
            ["StartPoint"] = JsonValue.Create(it.GetProperty("start").GetBoolean())
        });
    });

    // 怪物刷新
    ReplaceArray(e, "Respawns", body.GetProperty("respawns"), (arr, it) =>
    {
        arr.Add(new JsonObject
        {
            ["MonsterIndex"] = JsonValue.Create(it.GetProperty("monsterIndex").GetInt32()),
            ["Location"] = new JsonObject { ["X"] = JsonValue.Create(it.GetProperty("x").GetInt32()), ["Y"] = JsonValue.Create(it.GetProperty("y").GetInt32()) },
            ["Count"] = JsonValue.Create(it.GetProperty("count").GetInt32()),
            ["Spread"] = JsonValue.Create(it.GetProperty("spread").GetInt32()),
            ["Delay"] = JsonValue.Create(it.GetProperty("delay").GetInt32()),
            ["RandomDelay"] = JsonValue.Create(0),
            ["Direction"] = JsonValue.Create(it.GetProperty("direction").GetInt32()),
            ["RoutePath"] = JsonValue.Create(""),
            ["RespawnIndex"] = JsonValue.Create(store.NextId()),
            ["SaveRespawnTime"] = JsonValue.Create(false),
            ["RespawnTicks"] = JsonValue.Create(0)
        });
    });

    // 传送点（Movements，属于 MapInfo_Fix，服务器 Web 模式本地读取即生效）
    ReplaceArray(e, "Movements", body.GetProperty("movements"), (arr, it) =>
    {
        arr.Add(new JsonObject
        {
            ["MapIndex"] = JsonValue.Create(it.GetProperty("destMapIndex").GetInt32()),
            ["Source"] = new JsonObject { ["X"] = JsonValue.Create(it.GetProperty("sourceX").GetInt32()), ["Y"] = JsonValue.Create(it.GetProperty("sourceY").GetInt32()) },
            ["Destination"] = new JsonObject { ["X"] = JsonValue.Create(it.GetProperty("destX").GetInt32()), ["Y"] = JsonValue.Create(it.GetProperty("destY").GetInt32()) },
            ["NeedHole"] = JsonValue.Create(it.GetProperty("needHole").GetBoolean()),
            ["NeedMove"] = JsonValue.Create(it.GetProperty("needMove").GetBoolean()),
            ["ConquestIndex"] = JsonValue.Create(0),
            ["ShowOnBigMap"] = JsonValue.Create(false),
            ["Icon"] = JsonValue.Create(0)
        });
    });

    store.Save();
    return Results.Ok(new { ok = true });
});

// ---------- NPC 增删改（作用于全局 NPCInfo.json，按 MapIndex 关联地图） ----------
app.MapPost("/api/npcs", async (HttpContext ctx) =>
{
    using var doc = await JsonDocument.ParseAsync(ctx.Request.Body);
    var b = doc.RootElement;
    int mapIndex = b.GetProperty("mapIndex").GetInt32();
    string fileName = b.GetProperty("fileName").GetString() ?? "";
    string name = b.GetProperty("name").GetString() ?? "";
    int x = b.GetProperty("x").GetInt32();
    int y = b.GetProperty("y").GetInt32();
    int rate = b.TryGetProperty("rate", out var r) ? r.GetInt32() : 100;
    int image = b.TryGetProperty("image", out var im) ? im.GetInt32() : 0;
    int newIndex = store.AddNpcGlobal(mapIndex, fileName, name, x, y, rate, image);
    store.SaveNpc();
    return Results.Ok(new { ok = true, index = newIndex });
});

app.MapPut("/api/npcs/{index:int}", async (int index, HttpContext ctx) =>
{
    using var doc = await JsonDocument.ParseAsync(ctx.Request.Body);
    var b = doc.RootElement;
    string fileName = b.GetProperty("fileName").GetString() ?? "";
    string name = b.GetProperty("name").GetString() ?? "";
    int x = b.GetProperty("x").GetInt32();
    int y = b.GetProperty("y").GetInt32();
    int rate = b.GetProperty("rate").GetInt32();
    int image = b.GetProperty("image").GetInt32();
    bool ok = store.UpdateNpc(index, fileName, name, x, y, rate, image);
    if (!ok) return Results.NotFound(new { error = "未找到 NPC index=" + index });
    store.SaveNpc();
    return Results.Ok(new { ok = true });
});

app.MapDelete("/api/npcs/{index:int}", (int index) =>
{
    bool ok = store.DeleteNpc(index);
    if (!ok) return Results.NotFound(new { error = "未找到 NPC index=" + index });
    store.SaveNpc();
    return Results.Ok(new { ok = true });
});

void ReplaceArray(MapEntry e, string field, JsonElement items, Action<JsonArray, JsonElement> addOne)
{
    var arr = store.GetValues(e, field);
    arr.Clear();
    foreach (var it in items.EnumerateArray()) addOne(arr, it);
}

app.Run();
