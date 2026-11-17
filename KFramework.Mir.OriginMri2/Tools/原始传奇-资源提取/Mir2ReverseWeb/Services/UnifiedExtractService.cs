using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mir2.Direction;
using Mir2ReverseWeb.Models;

namespace Mir2ReverseWeb.Services;

/// <summary>
/// “按配置表提取资源（统一模式）”专用批量提取器：一次扫描所有配置表，导出三类资源：
///   1) model 帧：所有配置表里字段名 = model 的整数值（monster/npc/title/effect…）+ item 表外观列 20/17，
///      落地 {ModelOutDir}/{模型号}/{sourceId}.png；
///   2) 物品图标：item 表第 19 列图标 ID，落地 {ItemIconOutDir}/{iconId}.png；
///   3) 技能图标：skill 表 icon 字段，落地 {SkillIconOutDir}/{iconId}.png。
/// 版本表（resourceVersion.json）在此服务内独立下载解析，不依赖也不修改 AnimExportService。
/// </summary>
public class UnifiedExtractService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private const int CONCURRENCY = 16;
    private static readonly ConcurrentDictionary<Guid, Job> _jobs = new();

    private readonly GameConfigService _config;

    private Dictionary<string, string> _versions = new(StringComparer.OrdinalIgnoreCase);
    private bool _versionsLoaded;
    private readonly object _lock = new();

    private Dictionary<long, Dictionary<int, int>> _modelTotalDir = new();
    private bool _modelInfoLoaded;

    public UnifiedExtractService(GameConfigService config)
    {
        _config = config;
        try
        {
            _http.DefaultRequestHeaders.Referrer = new Uri(Mir2Config.CdnBase + "/");
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120 Safari/537.36");
        }
        catch { }
    }

    /// <summary>异步启动统一提取，返回 jobId 与总任务数（模型动作组 + 图标数）。</summary>
    public async Task<(Guid JobId, int Total)> StartAsync(UnifiedExtractRequest req)
    {
        if (!_config.IsConfigLoaded) await _config.LoadConfigTablesAsync();
        await EnsureVersionsAsync();
        await EnsureModelInfoAsync();

        int maxAction = Math.Max(1, Math.Min(req.MaxAction <= 0 ? 60 : req.MaxAction, 200));
        var allTables = _config.GetLoadedTableNames().Select(t => _config.GetTable(t)).Where(n => n != null).Select(n => n!).ToList();

        // 1) 扫描所有配置表里字段名 = model 的整数值
        var models = new HashSet<long>();
        foreach (var table in allTables) CollectModelFields(table, models);

        // item 外观列 20/17 也当模型号
        var item = _config.GetTable("item");
        if (item != null)
            foreach (var node in Normalize(item))
                if (node is JsonObject o)
                    foreach (var col in new[] { 20, 17 })
                    {
                        var v = GetInt(o, col.ToString());
                        if (v.HasValue && v.Value > 0) models.Add(v.Value);
                    }

        // 2) 物品图标 item[col19] -> icon/tool
        var itemIcons = new HashSet<int>();
        if (item != null)
            foreach (var node in Normalize(item))
                if (node is JsonObject o)
                {
                    var v = GetInt(o, "19");
                    if (v.HasValue && v.Value > 0) itemIcons.Add(v.Value);
                }

        // 3) 技能图标 skill.icon -> icon/skills
        var skillIcons = new HashSet<int>();
        var skill = _config.GetTable("skill");
        if (skill != null)
            foreach (var node in Normalize(skill))
                if (node is JsonObject o)
                {
                    var s = GetStr(o, "icon");
                    if (!string.IsNullOrWhiteSpace(s) && int.TryParse(s, out var iv) && iv > 0) skillIcons.Add(iv);
                }

        // 组装 model 动作组
        var modelGroups = new List<(long r, string ver, int action, bool json)>();
        foreach (var r in models.OrderBy(x => x))
        {
            _versions.TryGetValue("model/" + r, out var ver);
            ver ??= "";
            if (_modelTotalDir.TryGetValue(r, out var acts) && acts.Count > 0)
            {
                foreach (var a in acts.Keys.OrderBy(x => x))
                {
                    modelGroups.Add((r, ver, a, false));
                    if (req.WithJson) modelGroups.Add((r, ver, a, true));
                }
            }
            else
            {
                for (int a = 0; a <= maxAction; a++)
                {
                    modelGroups.Add((r, ver, a, false));
                    if (req.WithJson) modelGroups.Add((r, ver, a, true));
                }
            }
        }

        var total = modelGroups.Count + itemIcons.Count + skillIcons.Count;
        var job = new Job { TotalFiles = total };
        job.Messages.Add($"[扫描] model 模型号 {models.Count}、动作组 {modelGroups.Count}；物品图标 {itemIcons.Count}；技能图标 {skillIcons.Count}");
        var id = Guid.NewGuid();
        _jobs[id] = job;

        var modelDir = Pick(req.ModelOutDir, Path.Combine(Mir2Config.LocalResourceRoot, "assets", "resource", "model"));
        var itemDir = Pick(req.ItemIconOutDir, Path.Combine(Mir2Config.LocalResourceRoot, "assets", "resource", "icon", "tool"));
        var skillDir = Pick(req.SkillIconOutDir, Path.Combine(Mir2Config.LocalResourceRoot, "assets", "resource", "icon", "skills"));

        _ = Task.Run(async () => await Run(job, modelGroups, itemIcons, skillIcons, modelDir, itemDir, skillDir));
        return (id, total);
    }

    private static string Pick(string? v, string def) => string.IsNullOrWhiteSpace(v) ? def : v!;

    private async Task Run(Job job,
        List<(long r, string ver, int action, bool json)> groups,
        HashSet<int> itemIcons, HashSet<int> skillIcons,
        string modelDir, string itemDir, string skillDir)
    {
        try
        {
            Directory.CreateDirectory(modelDir);
            Directory.CreateDirectory(itemDir);
            Directory.CreateDirectory(skillDir);
            var frameBase = Mir2Config.FrameBase.TrimEnd('/');
            var verParam = Mir2Config.FrameQuery;
            var sem = new SemaphoreSlim(CONCURRENCY);
            var tasks = new List<Task>();

            foreach (var g in groups)
            {
                await sem.WaitAsync();
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        int td = GetTotalDir(g.r, g.action);
                        foreach (var dir in Mir2Directions.ActualDirs(td))
                            await DownloadModel(job, modelDir, frameBase, verParam, g.r, g.ver, g.action, dir, g.json);
                    }
                    catch { }
                    finally { Interlocked.Increment(ref job.DoneFiles); sem.Release(); }
                }));
            }
            foreach (var iconId in itemIcons)
            {
                await sem.WaitAsync();
                tasks.Add(Task.Run(async () =>
                {
                    try { await DownloadIcon(job, itemDir, frameBase, verParam, iconId, "tool"); }
                    catch { }
                    finally { Interlocked.Increment(ref job.DoneFiles); sem.Release(); }
                }));
            }
            foreach (var iconId in skillIcons)
            {
                await sem.WaitAsync();
                tasks.Add(Task.Run(async () =>
                {
                    try { await DownloadIcon(job, skillDir, frameBase, verParam, iconId, "skills", noVersion: true); }
                    catch { }
                    finally { Interlocked.Increment(ref job.DoneFiles); sem.Release(); }
                }));
            }
            await Task.WhenAll(tasks);
            job.Status = "done";
            job.Messages.Add($"[完成] 成功 {job.Success} 跳过 {job.Skipped} 失败 {job.Failed}，共 {(job.Bytes / 1048576.0):F1} MB");
            job.Messages.Add($"[落地] model={modelDir}；物品Icon={itemDir}；技能Icon={skillDir}");
        }
        catch (Exception ex) { job.Status = "error"; job.Messages.Add("错误: " + ex.Message); }
        finally { job.Finished = true; }
    }

    private async Task DownloadModel(Job job, string baseDir, string frameBase, string verParam, long r, string ver, int action, int dir, bool json)
    {
        long srcId = 1000L * r + 10L * action + dir;
        var ext = json ? "json" : "png";
        var urlRel = string.IsNullOrEmpty(ver) ? $"model/{r}/{srcId}.{ext}" : $"{ver}/model/{r}/{srcId}.{ext}";
        var url = $"{frameBase}/{urlRel}{verParam}";
        var local = Path.Combine(baseDir, r.ToString(), srcId + "." + ext);
        job.Current = $"model/{r}/{srcId}.{ext}";
        await Fetch(job, url, local);
    }

    private async Task DownloadIcon(Job job, string baseDir, string frameBase, string verParam, int iconId, string subDir, bool noVersion = false)
    {
        string key = $"icon/{subDir}/{iconId}.png";
        _versions.TryGetValue(key, out var ver);
        string urlRel = (!noVersion && !string.IsNullOrEmpty(ver)) ? $"{ver}/{key}" : key;
        var url = $"{frameBase}/{urlRel}{verParam}";
        var local = Path.Combine(baseDir, iconId + ".png");
        job.Current = key;
        await Fetch(job, url, local);
    }

    private async Task Fetch(Job job, string url, string local)
    {
        if (File.Exists(local) && new FileInfo(local).Length > 0) { Interlocked.Increment(ref job.Skipped); return; }
        byte[] bytes;
        try { bytes = await _http.GetByteArrayAsync(url); }
        catch { Interlocked.Increment(ref job.Failed); return; }
        if (bytes == null || bytes.Length == 0) { Interlocked.Increment(ref job.Failed); return; }
        Directory.CreateDirectory(Path.GetDirectoryName(local)!);
        await File.WriteAllBytesAsync(local, bytes);
        Interlocked.Add(ref job.Bytes, bytes.Length);
        Interlocked.Increment(ref job.Success);
    }

    public object GetProgress(Guid id)
    {
        if (!_jobs.TryGetValue(id, out var j)) return null!;
        return new
        {
            jobId = id.ToString(),
            status = j.Status,
            totalFiles = j.TotalFiles,
            doneFiles = j.DoneFiles,
            success = j.Success,
            skipped = j.Skipped,
            failed = j.Failed,
            bytes = j.Bytes,
            percent = j.TotalFiles > 0 ? (int)Math.Round(100.0 * j.DoneFiles / j.TotalFiles) : (j.Status == "done" ? 100 : 0),
            current = j.Current,
            messages = j.Messages.ToArray()
        };
    }

    // ---------------- 版本表 / modelInfo / 配置表工具 ----------------

    private async Task EnsureVersionsAsync()
    {
        bool need;
        lock (_lock) { need = !_versionsLoaded; }
        if (!need) return;
        foreach (var v in new[] { Mir2Config.GameVersion, "20260922111844", "" })
        {
            var url = string.IsNullOrEmpty(v) ? Mir2Config.ResourceVersionManifestUrl : Mir2Config.ResourceVersionManifestUrl + "?v=" + v;
            for (int attempt = 1; attempt <= 2; attempt++)
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
                    var s = await _http.GetStringAsync(url, cts.Token);
                    using var doc = JsonDocument.Parse(s);
                    var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                        foreach (var p in doc.RootElement.EnumerateObject())
                            if (p.Value.ValueKind == JsonValueKind.String) dict[p.Name] = p.Value.GetString()!;
                    lock (_lock) { _versions = dict; _versionsLoaded = true; }
                    return;
                }
                catch { }
            }
        }
        lock (_lock) { _versionsLoaded = true; }
    }

    private async Task EnsureModelInfoAsync()
    {
        if (_modelInfoLoaded) return;
        try
        {
            var url = $"{Mir2Config.CdnBase}/2config{Mir2Config.GameVersion}.json?v={Mir2Config.GameVersion}";
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var json = await _http.GetStringAsync(url, cts.Token);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("modelInfo", out var mi) && mi.ValueKind == JsonValueKind.Object)
            {
                var dict = new Dictionary<long, Dictionary<int, int>>();
                foreach (var m in mi.EnumerateObject())
                {
                    if (!long.TryParse(m.Name, out var modelId)) continue;
                    if (m.Value.ValueKind != JsonValueKind.Object) continue;
                    var acts = new Dictionary<int, int>();
                    foreach (var a in m.Value.EnumerateObject())
                    {
                        if (!int.TryParse(a.Name, out var act)) continue;
                        if (a.Value.TryGetProperty("totaldir", out var td) && td.TryGetInt32(out var t)) acts[act] = t;
                    }
                    dict[modelId] = acts;
                }
                _modelTotalDir = dict;
            }
        }
        catch { }
        finally { _modelInfoLoaded = true; }
    }

    private int GetTotalDir(long r, int action)
        => _modelTotalDir.TryGetValue(r, out var acts) && acts.TryGetValue(action, out var td) ? td : 8;

    private static void CollectModelFields(JsonNode? node, HashSet<long> into)
    {
        if (node is JsonObject o)
        {
            foreach (var kv in o)
            {
                if (kv.Key.Equals("model", StringComparison.OrdinalIgnoreCase))
                {
                    if (kv.Value is JsonValue jv)
                    {
                        if (jv.TryGetValue<int>(out var iv) && iv > 0) into.Add(iv);
                        else if (jv.TryGetValue<string>(out var sv) && int.TryParse(sv, out var svv) && svv > 0) into.Add(svv);
                    }
                }
                else CollectModelFields(kv.Value, into);
            }
        }
        else if (node is JsonArray a)
            foreach (var it in a) CollectModelFields(it, into);
    }

    private static IEnumerable<JsonNode> Normalize(JsonNode node)
    {
        if (node is JsonObject o && o.TryGetPropertyValue("$values", out var vals) && vals is JsonArray va)
            foreach (var v in va) yield return v;
        else if (node is JsonArray arr)
            foreach (var v in arr) yield return v;
        else if (node is JsonObject ob)
            foreach (var kv in ob) yield return kv.Value;
    }

    private static int? GetInt(JsonObject o, string key)
    {
        if (!o.TryGetPropertyValue(key, out var n) || n is not JsonValue jv) return null;
        if (jv.TryGetValue<int>(out var iv)) return iv;
        if (jv.TryGetValue<string>(out var sv) && int.TryParse(sv, out var svv)) return svv;
        return null;
    }

    private static string? GetStr(JsonObject o, string key)
    {
        if (!o.TryGetPropertyValue(key, out var n) || n is not JsonValue jv) return null;
        if (jv.TryGetValue<string>(out var sv)) return sv;
        if (jv.TryGetValue<int>(out var iv)) return iv.ToString();
        return null;
    }

    private class Job
    {
        public int TotalFiles;
        public int DoneFiles;
        public int Success;
        public int Skipped;
        public int Failed;
        public long Bytes;
        public string Status = "running";
        public string? Current;
        public bool Finished;
        public List<string> Messages = new();
    }
}
