using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using Microsoft.AspNetCore.Hosting;
using Mir2.Direction;
using Mir2ReverseWeb.Models;

namespace Mir2ReverseWeb.Services;

/// <summary>
/// 角色 / NPC / 怪物 / 特效 动画精灵表导出。
/// 逆向自客户端 main10.25341.1.min.js 的 ResUrl / ActionLoader / DirectionType：
///   sourceId = 1000*模型号 + 10*动作 + 方向(0~7)；
///   URL = {CdnBase}/assets/resource/{版本号}/model/{模型号}/{sourceId}.png?ver={ResourceVersion}
///   「版本号」来自 resourceVersion.json 的 cm.version["model/{模型号}"]（按 ?v= 分版，本游戏用 10.25341.1）。
/// resourceVersion.json 的键形如 "model/{模型号}"（无扩展名、无 sourceId），列出全部模型号。
/// 每个模型号下并无清单，故按动作枚举、每个动作按游戏方向系统只请求“实际存在的朝向”。
///
/// 方向系统（见 Mir2.Direction.Mir2Directions，工具与 WebGame.China.Mir2.Client 共用）：
///   每个动作在配置 modelInfo / effect 里有 totaldir(1/2/5/8)，getDir 据此把 8 个逻辑朝向映射为实际存储朝向，
///   引擎只加载这些实际朝向的帧。工具据此只下载真实帧（如特效 totaldir=1 仅 NONE=4 一帧；totaldir=5 仅 0..4），
///   避免对镜像位/缺帧发无效请求。模型不在 resourceVersion(无版本段，如 14031)时 ver 留空走 assets/resource/model/{id}/...。
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

    private readonly IWebHostEnvironment _env;
    private readonly GameConfigService _config;

    /// <summary>模型号 -> 名字（怪物/NPC 名，离线生成于 wwwroot/data/modelNames.json，源自原版客户端配置表）。</summary>
    private readonly Dictionary<string, string> _modelNames = new();

    /// <summary>模型号 -> (动作号 -> totaldir)，来自 2config 的 modelInfo；用于按游戏方向系统只请求实际朝向。</summary>
    private Dictionary<long, Dictionary<int, int>> _modelTotalDir = new();
    private bool _modelInfoLoaded;

    public AnimExportService(IWebHostEnvironment env, GameConfigService config)
    {
        _env = env;
        _config = config;
        // 加载 model -> 名字 映射（怪/NPC 名来自客户端配置 monster/npc 表，构建期由脚本从客户端配置分片抽出）
        try
        {
            var path = Path.Combine(env.WebRootPath, "data", "modelNames.json");
            if (File.Exists(path))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                foreach (var p in doc.RootElement.EnumerateObject())
                    if (p.Value.ValueKind == JsonValueKind.String)
                        _modelNames[p.Name] = p.Value.GetString()!;
            }
        }
        catch { /* 名字映射缺失不影响导出 */ }
    }

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

            // 键 "model/{模型号}" → 模型号就是键里的整数；版本号取 dict[k]
            var entries = new List<AnimModelEntry>();
            foreach (var k in modelKeys)
            {
                var r = ModelIdFromKey(k);
                if (r.HasValue)
                    entries.Add(new AnimModelEntry
                    {
                        Id = r.Value.ToString(),
                        Name = _modelNames.TryGetValue(r.Value.ToString(), out var nm) ? nm : null,
                        Version = dict.TryGetValue(k, out var v) ? v : null,
                    });
            }
            entries.Sort((a, b) => int.Parse(a.Id).CompareTo(int.Parse(b.Id)));
            res.Models = entries;
            res.ModelCount = entries.Count;
            res.Messages.Add($"[完成] 共 {res.ModelCount} 个模型号（角色/NPC/怪物/武器…），各自资源版本见条目列表");
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

        // 加载 modelInfo（动作 totaldir），供按游戏方向系统只请求实际存在的帧
        await EnsureModelInfoAsync();

        // 以 (模型号, 动作) 为一组。留空 Models 导出全部（resourceVersion 里的模型号）；
        // 指定 Models 时：即便模型不在 resourceVersion（无版本段路径 assets/resource/model/{id}/...，如 14031）
        // 也照常处理，ver 留空。
        var groups = new List<(long r, string ver, int action, bool json)>();
        if (selected == null)
        {
            foreach (var kv in versions)
            {
                if (!kv.Key.StartsWith("model/", StringComparison.OrdinalIgnoreCase)) continue;
                var r = ModelIdFromKey(kv.Key);
                if (!r.HasValue) continue;
                AddModelGroups(groups, r.Value, kv.Value, maxAction, req.WithJson);
            }
        }
        else
        {
            foreach (var idStr in selected)
            {
                if (!long.TryParse(idStr, out var r)) continue;
                versions.TryGetValue("model/" + idStr, out var ver);   // 不在版本表则 ver 留空（无版本段路径）
                AddModelGroups(groups, r, ver ?? "", maxAction, req.WithJson);
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

    /// <summary>把单个模型的动作组加入列表。模型在 modelInfo 中时只导出「登记的动作」（与游戏一致，避免对不存在的动作全向枚举发 404）；
    /// 不在 modelInfo 时回退扫描 0..maxAction（兜底，保持旧行为）。动作方向由 Run 内按 totaldir 决定。</summary>
    private void AddModelGroups(List<(long r, string ver, int action, bool json)> groups, long r, string ver, int maxAction, bool withJson)
    {
        if (_modelTotalDir.TryGetValue(r, out var acts) && acts.Count > 0)
        {
            // 仅导出配置里实际存在的动作；每个动作按 totaldir 只请求实际朝向（Run 内处理）
            foreach (var a in acts.Keys)
            {
                groups.Add((r, ver, a, false));
                if (withJson) groups.Add((r, ver, a, true));
            }
        }
        else
        {
            for (int a = 0; a <= maxAction; a++)
            {
                groups.Add((r, ver, a, false));
                if (withJson) groups.Add((r, ver, a, true));
            }
        }
    }

    /// <summary>取某模型某动作的 totaldir；modelInfo 无记录时默认 8 向（与游戏 getActionCfg 缺配置行为一致）。</summary>
    private int GetTotalDir(long r, int action)
    {
        if (_modelTotalDir.TryGetValue(r, out var acts) && acts.TryGetValue(action, out var td)) return td;
        return 8;
    }

    /// <summary>下载并解析 2config 的 modelInfo（模型→动作→totaldir），缓存到 _modelTotalDir；失败则退化为 8 向全扫。</summary>
    private async Task EnsureModelInfoAsync()
    {
        bool need;
        lock (_lock) { need = !_modelInfoLoaded; }
        if (!need) return;
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
        catch { /* modelInfo 缺失不影响导出，仅退化为 8 向全扫 */ }
        finally { _modelInfoLoaded = true; }
    }

    /// <summary>从原始配置（CDN 5config）重新生成 modelNames.json 的「特效」条目：skillEffects→effect→model，按 type 标注施法/命中/飞弹特效。
    /// 怪物/NPC/称号等其它名字保留原文件不变。生成后同步刷新内存 _modelNames，下次加载模型清单即生效。</summary>
    public async Task<object> RefreshModelNamesAsync()
    {
        var url = $"{Mir2Config.CdnBase}/5config{Mir2Config.GameVersion}.json?v={Mir2Config.GameVersion}";
        string json;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            json = await _http.GetStringAsync(url, cts.Token);
        }
        catch (Exception ex) { return new { ok = false, error = "下载 5config 失败：" + ex.Message }; }

        var dataPath = Path.Combine(_env.WebRootPath, "data", "modelNames.json");
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(dataPath))
        {
            using var ed = JsonDocument.Parse(File.ReadAllText(dataPath));
            foreach (var p in ed.RootElement.EnumerateObject())
                if (p.Value.ValueKind == JsonValueKind.String)
                    names[p.Name] = p.Value.GetString()!;
        }

        int updated = 0;
        using (var doc = JsonDocument.Parse(json))
        {
            var root = doc.RootElement;
            if (!root.TryGetProperty("skillEffects", out var se) || se.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("effect", out var eff) || eff.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("skill", out var skills) || skills.ValueKind != JsonValueKind.Object)
                return new { ok = false, error = "5config 缺少 skillEffects/effect/skill 表" };

            foreach (var kv in se.EnumerateObject())
            {
                if (!int.TryParse(kv.Name, out var k)) continue;
                int skillId = k / 100, level = k % 100;
                if (skillId == 0) { skillId = k; level = 0; }
                var sName = SkillName(skills, skillId) ?? SkillName(skills, k);
                if (sName == null) continue; // 解析不到技能名：保留旧名，不覆盖
                var arr = kv.Value.ValueKind == JsonValueKind.Array
                    ? kv.Value.EnumerateArray().ToList()
                    : new List<JsonElement> { kv.Value };
                var roles = AssignRoles(arr);
                for (int i = 0; i < arr.Count; i++)
                {
                    if (!arr[i].TryGetProperty("effect", out var ef) || !ef.TryGetInt32(out var eid)) continue;
                    if (!eff.TryGetProperty(eid.ToString(), out var em) || em.ValueKind != JsonValueKind.Object) continue;
                    if (!em.TryGetProperty("model", out var me) || !me.TryGetInt32(out var model)) continue;
                    var mkey = model.ToString();
                    var lv = level > 0 ? $"(Lv{level})" : "";
                    var nn = $"特效·{sName}{lv}·{roles[i]}";
                    if (nn.StartsWith("技能")) continue; // 退化名不覆盖
                    names[mkey] = nn; updated++;
                }
            }
        }

        File.WriteAllText(dataPath, JsonSerializer.Serialize(names));
        _modelNames.Clear();
        foreach (var kv in names) _modelNames[kv.Key] = kv.Value;
        return new { ok = true, updated, total = names.Count };
    }

    private static string? SkillName(JsonElement skills, int id)
    {
        if (skills.TryGetProperty(id.ToString(), out var s) && s.ValueKind == JsonValueKind.Object
            && s.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
            return n.GetString();
        return null;
    }

    /// <summary>按 skillEffects 条目 type/pos 推导组内角色：type0=施法特效(立即落点)、type1=命中特效(目标)、type2=飞弹特效(弹道)；pos0=自身。</summary>
    private static List<string> AssignRoles(List<JsonElement> arr)
    {
        var labels = new List<string>();
        for (int i = 0; i < arr.Count; i++)
        {
            int type = arr[i].TryGetProperty("type", out var t) && t.TryGetInt32(out var tv) ? tv : 0;
            int pos = arr[i].TryGetProperty("pos", out var p) && p.TryGetInt32(out var pv) ? pv : 1;
            string r = i == 0
                ? (type == 0 ? "施法特效" : type == 1 ? "命中特效" : type == 2 ? "飞弹特效" : "特效" + type)
                : (type == 1 ? "命中特效" : type == 2 ? "飞弹特效" : "追加特效");
            if (pos == 0) r = "自身·" + r;
            labels.Add(r);
        }
        var seen = new Dictionary<string, int>();
        var outL = new List<string>();
        foreach (var r in labels)
        {
            if (!seen.ContainsKey(r)) { seen[r] = 0; outL.Add(r); }
            else { seen[r]++; outL.Add(r + "·" + (seen[r] + 1)); }
        }
        return outL;
    }

    // ---------------- 统一资源提取（按配置表分类） ----------------
    /// <summary>一个资源分类：id/显示名/后端配置表名/名字取法/模型取法。</summary>
    public record ResourceCategory(string Id, string Label, string Table, string NameRule, string ModelRule);

    /// <summary>下拉分类。怪物/NPC/称号/特效 直接取 model 字段；技能特效 经 skillEffects→effect→model；物品 为数组按列取外观。</summary>
    private static readonly ResourceCategory[] _resourceCategories =
    {
        new("monster", "怪物", "monster", "name", "model"),
        new("npc", "NPC", "npc", "name", "model"),
        new("title", "称号", "title", "name", "model"),
        new("effect", "特效(全部 effect)", "effect", "id", "model"),
        new("skillEffects", "技能特效", "skillEffects", "__skill", "__skillEffects"),
        new("item", "物品(外观·实验)", "item", "__itemName", "__itemLooks"),
    };

    public object GetResourceCategories()
        => _resourceCategories.Select(c => new { id = c.Id, label = c.Label }).ToArray();

    /// <summary>返回某分类下的条目（模型号 + 名字），可按关键字过滤。配置表未加载时自动尝试加载。</summary>
    public async Task<object> GetResourceEntriesAsync(string catId, string? q)
    {
        var cat = _resourceCategories.FirstOrDefault(c => c.Id == catId);
        if (cat == null) return new { ok = false, error = "未知分类：" + catId };
        if (_config == null || !_config.IsConfigLoaded)
        {
            try { await _config.LoadConfigTablesAsync(); }
            catch (Exception ex) { return new { ok = false, error = "加载配置表失败：" + ex.Message }; }
        }
        var table = _config.GetTable(cat.Table);
        if (table == null) return new { ok = false, error = $"配置表 {cat.Table} 未找到（请先在配置页加载配置表）" };

        var rows = new List<object>();
        var query = (q ?? "").Trim();
        if (cat.Id == "skillEffects")
            BuildSkillEffectRows(table, query, rows);
        else
            BuildTableRows(cat, table, query, rows);
        return new { ok = true, count = rows.Count, rows };
    }

    private void BuildSkillEffectRows(JsonNode seNode, string q, List<object> rows)
    {
        if (seNode is not JsonObject se) return;
        var skills = _config.GetTable("skill") as JsonObject;
        var eff = _config.GetTable("effect") as JsonObject;
        foreach (var kv in se)
        {
            if (!int.TryParse(kv.Key, out var k)) continue;
            int skillId = k / 100, level = k % 100;
            if (skillId == 0) { skillId = k; level = 0; }
            var sName = SkillNameNode(skills, skillId) ?? SkillNameNode(skills, k);
            if (sName == null) continue;
            var arr = kv.Value is JsonArray ja ? ja : new JsonArray { kv.Value! };
            var roles = AssignRolesNode(arr);
            for (int i = 0; i < arr.Count; i++)
            {
                if (arr[i] is not JsonObject e) continue;
                var eid = GetIntFlex(e, "effect");
                if (!eid.HasValue) continue;
                var model = (eff?.TryGetPropertyValue(eid.Value.ToString(), out var em) == true && em is JsonObject emo)
                    ? GetIntFlex(emo, "model") : null;
                if (!model.HasValue) continue;
                var name = $"特效·{sName}{(level > 0 ? $"(Lv{level})" : "")}·{roles[i]}";
                if (q.Length > 0 && !(name.Contains(q) || model.Value.ToString().Contains(q))) continue;
                rows.Add(new { key = model.Value.ToString(), name, models = new[] { model.Value } });
            }
        }
    }

    private void BuildTableRows(ResourceCategory cat, JsonNode table, string q, List<object> rows)
    {
        foreach (var e in NormalizeNode(table))
        {
            if (e is not JsonObject o) continue;
            var name = ResolveName(cat.NameRule, o);
            var models = ResolveModels(cat.ModelRule, o);
            if (models.Count == 0) continue;
            var key = GetIntFlex(o, "id")?.ToString() ?? models[0].ToString();
            if (q.Length > 0 && !(name.Contains(q) || string.Join(",", models).Contains(q))) continue;
            rows.Add(new { key, name, models });
        }
    }

    private static List<JsonNode> NormalizeNode(JsonNode node)
    {
        if (node is JsonObject o) return o.Select(p => p.Value!).ToList();
        if (node is JsonArray a) return a.Select(x => x!).ToList();
        return new List<JsonNode>();
    }

    private static string ResolveName(string rule, JsonObject o)
    {
        return rule switch
        {
            "name" => o["name"]?.GetValue<string>() ?? "",
            "id" => "特效" + (GetIntFlex(o, "id")?.ToString() ?? "?"),
            "__itemName" => o["2"]?.GetValue<string>() ?? "",
            _ => o["name"]?.GetValue<string>() ?? ""
        };
    }

    private static List<int> ResolveModels(string rule, JsonObject o)
    {
        var list = new List<int>();
        if (rule == "model")
        {
            var m = GetIntFlex(o, "model");
            if (m.HasValue) list.Add(m.Value);
        }
        else if (rule == "__itemLooks")
        {
            foreach (var idx in new[] { 20, 17 })
            {
                var v = GetIntFlex(o, idx.ToString());
                if (v.HasValue && v.Value > 0 && !list.Contains(v.Value)) list.Add(v.Value);
            }
        }
        return list;
    }

    private static int? GetIntFlex(JsonObject o, string key)
    {
        var v = o[key];
        if (v is not JsonValue jv) return null;
        if (jv.TryGetValue<int>(out var i)) return i;
        if (jv.TryGetValue<string>(out var s) && int.TryParse(s, out var si)) return si;
        return null;
    }

    private static string? SkillNameNode(JsonObject? skills, int id)
    {
        if (skills == null) return null;
        if (skills.TryGetPropertyValue(id.ToString(), out var s) && s is JsonObject so
            && so.TryGetPropertyValue("name", out var n) && n is JsonValue nv)
            return nv.GetValue<string>();
        return null;
    }

    private static List<string> AssignRolesNode(JsonArray arr)
    {
        var labels = new List<string>();
        for (int i = 0; i < arr.Count; i++)
        {
            if (arr[i] is not JsonObject e) { labels.Add("特效" + i); continue; }
            int type = GetIntFlex(e, "type") ?? 0;
            int pos = GetIntFlex(e, "pos") ?? 1;
            string r = i == 0
                ? (type == 0 ? "施法特效" : type == 1 ? "命中特效" : type == 2 ? "飞弹特效" : "特效" + type)
                : (type == 1 ? "命中特效" : type == 2 ? "飞弹特效" : "追加特效");
            if (pos == 0) r = "自身·" + r;
            labels.Add(r);
        }
        var seen = new Dictionary<string, int>();
        var outL = new List<string>();
        foreach (var r in labels)
        {
            if (!seen.ContainsKey(r)) { seen[r] = 0; outL.Add(r); }
            else { seen[r]++; outL.Add(r + "·" + (seen[r] + 1)); }
        }
        return outL;
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
            // 帧资源基址：默认 CdnBase+"/assets/resource"（旧行为），但真实帧主机是游戏运行时注入的
            // js_gameVars.resUrl / cdnServer（与 resourceVersion.json 所在主机不同）。若此主机下取帧 404，
            // 用环境变量 MIR2_FRAME_BASE 覆盖为真实 resUrl 基址（含 /resource 或 /assets/resource 段）。
            var frameBase = Mir2Config.FrameBase.TrimEnd('/');
            var verParam = Mir2Config.FrameQuery;                    // 客户端帧无 ?ver 参数，默认空串

            var sem = new SemaphoreSlim(CONCURRENCY);
            var tasks = new List<Task>();
            foreach (var g in groups)
            {
                await sem.WaitAsync();
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        // 每个动作按 totaldir 仅请求“实际存在的朝向”（见 Mir2Directions）：
                        // 游戏 getDir 依据 totaldir(1/2/5/8) 把 8 个逻辑朝向映射为实际存储朝向，引擎只加载这些帧。
                        int totaldir = GetTotalDir(g.r, g.action);
                        foreach (var dir in Mir2Directions.ActualDirs(totaldir))
                            await DownloadOne(job, baseDir, frameBase, verParam, g.r, g.ver, g.action, dir, g.json);
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

    /// <summary>下载单帧，落盘到统一相对路径（与 CDN 完全一致）。
    /// sourceId = 1000*模型号 + 10*动作 + 方向(0~7)，URL 公式与游戏客户端 JS 一致，各 srcId 下载各自正确的帧。
    /// version 为空（无版本段模型，如 14031）时走 assets/resource/model/{模型号}/...。</summary>
    private async Task<bool> DownloadOne(Job job, string baseDir, string frameBase, string verParam, long r, string ver, int action, int dir, bool json)
    {
        long srcId = 1000L * r + 10L * action + dir;
        var ext = json ? "json" : "png";
        // 相对路径：version 为空（无版本段模型，如 14031）时走 assets/resource/model/{模型号}/...，
        // 否则 {版本}/model/{模型号}/{sourceId}.{ext}；frameBase 已包含 /resource 或 /assets/resource 段
        var relPath = string.IsNullOrEmpty(ver)
            ? $"model/{r}/{srcId}.{ext}"
            : $"{ver}/model/{r}/{srcId}.{ext}";
        var url = $"{frameBase}/{relPath}{verParam}";
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
