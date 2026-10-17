using System.Collections.Concurrent;
using System.Text.Json;
using Mir2ReverseWeb.Models;

namespace Mir2ReverseWeb.Services;

/// <summary>
/// 角色 / NPC / 怪物 动画精灵表导出。
/// 逆向自客户端 main10.25341.1.min.js 的 ResUrl / ActionLoader：
///   sourceId = 1000*模型号 + 10*动作 + 方向(0~7)；
///   URL = {CdnBase}/assets/resource/{版本号}/model/{模型号}/{sourceId}.png?ver={ResourceVersion}
///   「版本号」来自 resourceVersion.json 的 cm.version["model/{模型号}"]（按 ?v= 分版，本游戏用 10.25341.1）。
/// resourceVersion.json 的键形如 "model/{模型号}"（无扩展名、无 sourceId），列出全部模型号。
/// 每个模型号下并无清单，故按动作×方向枚举 sourceId，下载并跳过 404 的缺帧。
/// 解析用 JsonDocument（允许大小写重复键，后者覆盖），规避 PowerShell 所见重复键报错。
/// </summary>
public class AnimExportService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private const int CONCURRENCY = 16;
    private static readonly ConcurrentDictionary<Guid, Job> _jobs = new();

    // 资源名(模型键) -> 版本号。键形如 "model/{模型号}"。
    private Dictionary<string, string> _versions = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    static AnimExportService()
    {
        // 模型帧走独立资源域，部分 CDN 有防盗链，带上 Referer / UA 提高成功率
        try
        {
            _http.DefaultRequestHeaders.Referrer = new Uri(Mir2Config.CdnBase + "/");
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120 Safari/537.36");
        }
        catch { /* 忽略头设置失败 */ }
    }

    /// <summary>下载并解析 resourceVersion.json（?v=10.25341.1 分版，含全部模型号），返回模型号清单（仅内存，不落盘）。</summary>
    public async Task<AnimLoadResult> LoadAsync()
    {
        var res = new AnimLoadResult { ResourceVersionUrl = Mir2Config.ResourceVersionManifestUrl };
        try
        {
            var json = await DownloadVersionJsonAsync(res.Messages);
            if (json == null)
            {
                res.Error = "无法下载 resourceVersion.json（已尝试多个 ?v 参数，详见上方日志）。" +
                            "请确认运行本服务的机器能访问 CDN：" + Mir2Config.CdnBase +
                            "（可在本机浏览器手动打开该地址验证；若浏览器能开但服务端报错，多为 CDN 防盗链/限流——已自动带 Referer/UA 头，仍可重试）。";
                return res;
            }
            var dict = ParseVersions(json, res.Messages);
            lock (_lock) { _versions = dict; }

            var modelKeys = dict.Keys
                .Where(k => k.StartsWith("model/", StringComparison.OrdinalIgnoreCase))
                .ToList();
            res.TotalEntries = modelKeys.Count;   // = 模型号数量

            // 键 "model/{模型号}" → 模型号就是键里的整数
            var models = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var k in modelKeys)
            {
                var r = ModelIdFromKey(k);
                if (r.HasValue) models.Add(r.Value.ToString());
            }
            res.Models = models.ToList();
            res.ModelCount = res.Models.Count;
            res.Messages.Add($"[完成] 共 {res.ModelCount} 个模型号（角色/NPC/怪物/武器…）");
            res.Ok = true;
        }
        catch (Exception ex) { res.Error = ex.Message; }
        return res;
    }

    private async Task<string?> DownloadVersionJsonAsync(List<string> msgs)
    {
        // 按 ?v= 分版：本游戏资源版本为 10.25341.1（含 model/9027 等）；依次尝试，最后回退裸 URL
        string? lastErr = null;
        foreach (var v in new[] { "10.25341.1", "20260922111844", "" })
        {
            var url = string.IsNullOrEmpty(v) ? Mir2Config.ResourceVersionManifestUrl
                                              : Mir2Config.ResourceVersionManifestUrl + "?v=" + v;
            for (int attempt = 1; attempt <= 2; attempt++)
            {
                try
                {
                    msgs.Add($"[下载] {url}（第{attempt}次）");
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
                    var s = await _http.GetStringAsync(url, cts.Token);
                    msgs.Add($"[已载] {url}（{(s.Length / 1024.0):F1} KB）");
                    return s;
                }
                catch (Exception ex) { lastErr = ex.Message; msgs.Add($"[失败] {url}：{ex.Message}"); }
            }
        }
        return null;
    }

    /// <summary>用 JsonDocument 解析（允许重复键；大小写不敏感字典，后者覆盖）。</summary>
    private static Dictionary<string, string> ParseVersions(string json, List<string> msgs)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
                foreach (var p in doc.RootElement.EnumerateObject())
                    if (p.Value.ValueKind == JsonValueKind.String)
                        dict[p.Name] = p.Value.GetString()!;   // 重复键（大小写冲突）后者覆盖
            msgs.Add($"[解析] resourceVersion.json：{dict.Count} 条版本映射（其中 model/ 模型号 {dict.Keys.Count(k => k.StartsWith("model/", StringComparison.OrdinalIgnoreCase))}）");
        }
        catch (Exception ex) { msgs.Add($"[解析失败] {ex.Message}"); }
        return dict;
    }

    /// <summary>从键 "model/{模型号}" 解析模型号。</summary>
    private static int? ModelIdFromKey(string key)
    {
        if (!key.StartsWith("model/", StringComparison.OrdinalIgnoreCase)) return null;
        var id = key.Substring("model/".Length).Trim();
        if (int.TryParse(id, out var r)) return r;
        return null;
    }

    /// <summary>启动异步导出任务，立即返回 jobId 与总「动作组」数；若尚未加载版本表会自动先加载。</summary>
    public async Task<(Guid JobId, int TotalFiles)> StartAsync(AnimExportRequest req)
    {
        Dictionary<string, string> versions;
        lock (_lock) { versions = _versions; }
        if (versions.Count == 0) { await LoadAsync(); lock (_lock) { versions = _versions; } }

        var selected = ParseModelFilter(req.Models);   // null = 全部
        int maxAction = Math.Max(1, Math.Min(req.MaxAction <= 0 ? 60 : req.MaxAction, 200));

        // 以 (模型号, 动作) 为一组；组内先下 dir0，命中再下 dir1-7（避免对不存在的帧发 8 倍无效请求）。
        // 留空 Models 即导出全部模型（resourceVersion.json 里列出的 4774 个模型号）。
        var groups = new List<(long r, string ver, int action, bool json)>();
        foreach (var kv in versions)
        {
            if (!kv.Key.StartsWith("model/", StringComparison.OrdinalIgnoreCase)) continue;
            var r = ModelIdFromKey(kv.Key);
            if (!r.HasValue) continue;
            if (selected != null && !selected.Contains(r.Value.ToString())) continue;
            var ver = kv.Value;
            for (int action = 0; action <= maxAction; action++)
            {
                groups.Add((r.Value, ver, action, false));
                if (req.WithJson) groups.Add((r.Value, ver, action, true));
            }
        }

        var id = Guid.NewGuid();
        var job = new Job { TotalFiles = groups.Count };
        _jobs[id] = job;
        _ = Task.Run(async () => await Run(id, job, groups, req.OutDir));
        return (id, groups.Count);
    }

    private static HashSet<string>? ParseModelFilter(string? models)
    {
        if (string.IsNullOrWhiteSpace(models) || models!.Trim() == "*") return null;
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in models.Split([',', ' ', ';', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries))
            set.Add(p.Trim());
        return set.Count == 0 ? null : set;
    }

    public AnimProgress? GetProgress(Guid id)
    {
        if (!_jobs.TryGetValue(id, out var j)) return null;
        int pct = j.TotalFiles > 0 ? (int)(100L * j.DoneFiles / j.TotalFiles) : (j.Finished ? 100 : 0);
        return new AnimProgress
        {
            JobId = id.ToString(),
            Status = j.Status,
            TotalFiles = j.TotalFiles,
            DoneFiles = j.DoneFiles,
            Skipped = j.Skipped,
            Failed = j.Failed,
            Bytes = j.Bytes,
            Percent = pct,
            Current = j.Current,
            Messages = j.Messages.TakeLast(15).ToList()
        };
    }

    private async Task Run(Guid id, Job job, List<(long r, string ver, int action, bool json)> groups, string? outDir)
    {
        try
        {
            var baseDir = string.IsNullOrWhiteSpace(outDir)
                ? Path.Combine(GameConfigService.OsDownloadDir(), "Mir2Anim")
                : outDir!;
            Directory.CreateDirectory(baseDir);
            var cdnRoot = Mir2Config.CdnBase.TrimEnd('/');          // https://cdn.yscq-wy.yscq.com/fugu_20201203
            var verParam = "?ver=" + Mir2Config.ResourceVersion;     // 缓存破坏参数 ?ver=1.0.2

            var sem = new SemaphoreSlim(CONCURRENCY);
            var tasks = new List<Task>();
            foreach (var g in groups)
            {
                await sem.WaitAsync();
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        // 先下 dir0：成功才继续 dir1-7；404 说明该动作本就不存在，整组跳过其余方向
                        var okDir0 = await DownloadOne(job, baseDir, cdnRoot, verParam, g.r, g.ver, g.action, 0, g.json);
                        if (okDir0)
                            for (int dir = 1; dir < 8; dir++)
                                await DownloadOne(job, baseDir, cdnRoot, verParam, g.r, g.ver, g.action, dir, g.json);
                    }
                    catch { /* 单组异常不中断整体 */ }
                    finally { Interlocked.Increment(ref job.DoneFiles); sem.Release(); }
                }));
            }
            await Task.WhenAll(tasks);
            job.Status = "done";
            job.Messages.Add($"[完成] 动作组 {job.TotalFiles}：成功帧 {job.SuccessFrames} 跳过 {job.Skipped} 失败(缺帧/出错) {job.Failed}，共 {(job.Bytes / 1048576.0):F1} MB -> {baseDir}");
        }
        catch (Exception ex) { job.Status = "error"; job.Messages.Add("错误: " + ex.Message); }
        finally { job.Finished = true; }
    }

    /// <summary>下载单帧，落盘到统一相对路径 assets/resource/{ver}/model/{r}/{srcId}.{ext}（与 CDN 完全一致）。
    /// sourceId = 1000*模型号 + 10*动作 + 方向(0~7)，URL 公式与游戏客户端 JS 一致，各 srcId 下载各自正确的帧。</summary>
    private async Task<bool> DownloadOne(Job job, string baseDir, string cdnRoot, string verParam, long r, string ver, int action, int dir, bool json)
    {
        long srcId = 1000L * r + 10L * action + dir;
        var ext = json ? "json" : "png";
        var relPath = $"assets/resource/{ver}/model/{r}/{srcId}.{ext}";   // 统一相对路径(含版本)，对齐 CDN
        var url = $"{cdnRoot}/{relPath}{verParam}";
        var local = Path.Combine(baseDir, relPath.Replace('/', Path.DirectorySeparatorChar));
        job.Current = relPath;
        if (File.Exists(local) && new FileInfo(local).Length > 0) { Interlocked.Increment(ref job.Skipped); return true; }
        byte[] bytes;
        try { bytes = await _http.GetByteArrayAsync(url); }
        catch { Interlocked.Increment(ref job.Failed); return false; }
        if (bytes == null || bytes.Length == 0) { Interlocked.Increment(ref job.Failed); return false; }
        Directory.CreateDirectory(Path.GetDirectoryName(local)!);
        await File.WriteAllBytesAsync(local, bytes);
        Interlocked.Add(ref job.Bytes, bytes.Length);
        Interlocked.Increment(ref job.SuccessFrames);
        return true;
    }

    private class Job
    {
        public int TotalFiles;       // 动作组数
        public int DoneFiles;        // 已处理动作组数
        public int Skipped;
        public int Failed;
        public int SuccessFrames;
        public long Bytes;
        public string Status = "running";
        public string? Current;
        public bool Finished;
        public List<string> Messages = new();
    }
}
