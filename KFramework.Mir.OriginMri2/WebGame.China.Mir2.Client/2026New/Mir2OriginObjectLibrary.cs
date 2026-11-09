using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Client.MirGraphics;              // DXManager / MirBlend
using KFramework.MonoGame;
using KFramework.MonoGameExtend;       // KSpriteInfo
using Rectangle = KFramework.MonoGame.Rectangle;

/// <summary>需要走原始传奇 CDN 独立图集的对象类型（玩家与怪物/NPC 同一套 model 资源）。</summary>
public enum Mir2ObjectKind
{
    /// <summary>怪物：model/{模型号}/{sourceId}.png|json，按动作+方向分图集。</summary>
    Monster,
    /// <summary>NPC：与怪物同一套 model 资源，独立图集、独立缓存键。</summary>
    NPC,
    /// <summary>物品：icon/tool/{图标号}.png，单张图即一帧，无独立 json。</summary>
    Item,
    /// <summary>玩家角色：与怪物同一套 model 资源（参考模型 9027），按 职业+性别 选模型号。</summary>
    Player
}

/// <summary>
/// 原始传奇「怪物 / NPC / 物品 / 玩家」四类【独立图集】的统一资源管理器。
///
/// 与 Mir2AtlasLibrary（UI 共享图集，按 manifest key + default.res.json 别名解析）是两套体系：
/// UI 图集是「一张大盘 + 很多帧」，而对象资源是「每个模型/每个动作方向 各自一张 png + 一张 json」，
/// 即每个对象一份独立图集；物品则是一图标一张 png。故不复用 Mir2AtlasLibrary，单独在此统一管理。
///
/// 资源 URL 规则（逆向自 main10.25341.1.min.js 的 ResUrl / ActionLoader，与 Mir2ReverseWeb 一致）：
///   • 版本表：{CdnBase}/resourceVersion.json?v=10.25341.1，键形如 "model/{模型号}"、
///     "icon/tool/{图标号}.png"，值形如 "10.1393.1"；无版本段的模型走无版本目录。
///   • 模型帧：assets/resource/{版本}/model/{模型号}/{sourceId}.{png|json}?ver=1.0.2
///     sourceId = 1000*模型号 + 10*动作 + 方向(0..7)；版本为空时 = assets/resource/model/{模型号}/{sourceId}...
///   • 物品图标：assets/resource/{版本}/icon/tool/{图标号}.png?ver=1.0.2
///   • 2config.modelInfo 给出每个模型登记的动作号与 totaldir；待机 action=1（每个模型都有），
///     总方向数通常为 5（真帧 0..4，5..7 镜像），见 GetDir。
/// 取数全部复用 Mir2OriginResource（Cache Storage + 优先级调度 + 浏览器原生解码），绘制走 DXManager.Draw，
/// 与 MirImageControl 的图集绘制通道完全同款。
///
/// 用法（绘制路径绝不等待网络）：
///   1) 启动时 Mir2OriginObjectLibrary.InitializeAsync() 拉版本表；
///   2) 对象出现/换动作时 Request(kind, id, action, dir) 发起异步预取；
///   3) 每帧 DrawSpriteAt(...) / TryGetSprite(...)，未就绪返回 false（不画，不崩）。
/// </summary>
public static class Mir2OriginObjectLibrary
{
    private const string AssetsRoot = "assets/resource/";
    private const string VerQuery = "?ver=" + Mir2OriginResource.ResourceVersion;
    private const string VersionManifest = "resourceVersion.json";

    /// <summary>待机动作号：2config.modelInfo 里每个模型都登记了 action=1。</summary>
    public const int ActionStand = 1;
    /// <summary>其它常见动作号（2config.modelInfo 全表动作频次）。</summary>
    public const int ActionWalk = 2, ActionAttack = 4, ActionStruck = 10, ActionDie = 12;
    /// <summary>怪物默认存储方向数：totaldir=5 → 真帧方向 0..4，5..7 为镜像。</summary>
    public const int DefaultTotalDir = 5;
    /// <summary>NPC 待机动作在 2config.modelInfo 里是 totaldir=1 → 只有 NONE(=4) 一帧。</summary>
    public const int NPCTotalDir = 1;

    // —— 加载优先级层级（对齐原始 ActionLoader 的 急/懒 双队列：自己=急、他人=懒）——
    /// <summary>自己（本体+装备）：最高，先于他人出；低于地图瓦片(10)以保地面先出。</summary>
    public const int PriorityUser = 8;
    /// <summary>当前在屏、正在绘制的他人/怪物/NPC：次高。</summary>
    public const int PriorityVisible = 5;
    /// <summary>后台静默预载（懒队列）：最低，仅在无更急请求时空闲暖缓存。</summary>
    public const int PriorityBackground = 0;
    /// <summary>物品图标（背包/地面），同屏可见。</summary>
    public const int PriorityItem = 5;

    // —— 版本表（resourceVersion.json：资源名 -> 版本号）——
    private static Dictionary<string, string> _versions = new(StringComparer.OrdinalIgnoreCase);
    private static volatile bool _versionsReady;
    private static Task _initTask;

    // —— 独立图集缓存：key = "{kind}:{id}:{action}:{dir}" ——
    // 同类资源按 key 去重在途加载（多个怪物共用同一个 model 号时只发一次 HTTP），与 MirImageControl
    // 的 AtlasNameKey / Mir2AtlasLibrary._sheetTasks 是同一套思路。
    private static readonly ConcurrentDictionary<string, Task<Mir2AtlasSheet>> _sheets =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, Task<Texture2D>> _images =
        new(StringComparer.OrdinalIgnoreCase);
    // 缺失缓存：确认「加载失败」的资源 key 集合（永久，不再重试）。
    // 重试由更底层全部兜底：浏览器原生层 → ContentManager.LoadCacheOrDownloadAsync →
    // BrowserResource.GetBytesAsync(内置 3 次退避重试) → 上层 Draw 每帧重请求。
    // 这里只是“更底层重试全部耗尽后仍失败”的终态硬停，避免每帧对确缺失(真 404/不可达)的资源
    // 再发请求刷屏、拖垮 CDN。一旦进入即永久，不自愈——真缺失不会自己变好。
    private static readonly ConcurrentDictionary<string, byte> _missing =
        new(StringComparer.OrdinalIgnoreCase);
    // 已打印过“加载失败”的 key，避免每帧刷日志。
    private static readonly ConcurrentDictionary<string, byte> _loggedMissing =
        new(StringComparer.OrdinalIgnoreCase);

    // —— 枢轴表：modelInfo[model][action].standx/standy（原始传奇每个部件按自身枢轴对齐）——
    // 键 = model*1000 + action；值 = [standx, standy]。缺失时回落画布中心，不影响渲染。
    private static readonly Dictionary<long, int[]> _stand = new();
    // —— 帧间隔表：modelInfo[model][action].intval（原始传奇逐模型逐动作的每帧毫秒）——
    // 键 = model*1000 + action；值 = 每帧毫秒。玩家动画速度据此跟随原版；缺失时回落 Frame.Interval。
    private static readonly Dictionary<long, int> _intval = new();
    // —— 每模型的动作清单（用于 PreloadModel 全动作预载）与 totaldir（决定预载方向数）——
    private static readonly Dictionary<int, List<int>> _modelActions = new();
    private static readonly Dictionary<long, int> _totaldir = new();
    private static volatile bool _standReady;

    /// <summary>版本表是否就绪。未就绪时仍可用无版本段路径加载，只是可能 404。</summary>
    public static bool IsReady => _versionsReady;

    // ===================== 枢轴表（modelInfo.standx/standy）=====================

    /// <summary>加载内嵌的 modelInfo_stand.json（每模型每动作的 standx/standy 枢轴）。
    /// 原始传奇每个部件按自身枢轴对齐角色点，并绕该点翻面；复刻它的关键是这里。
    /// 解析失败/缺失则回落画布中心（DrawAnchored 内部处理），不影响渲染。</summary>
    private static void LoadStandTable()
    {
        if (_standReady) return;
        _standReady = true;
        try
        {
            var asm = typeof(Mir2OriginObjectLibrary).Assembly;
            var resName = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("modelInfo_stand.json", StringComparison.OrdinalIgnoreCase));
            if (resName == null) return;
            using var stream = asm.GetManifestResourceStream(resName);
            if (stream == null) return;
            using var doc = JsonDocument.Parse(stream);
            foreach (var modelElem in doc.RootElement.EnumerateObject())
            {
                if (!int.TryParse(modelElem.Name, out int model)) continue;
                foreach (var actElem in modelElem.Value.EnumerateObject())
                {
                    if (!int.TryParse(actElem.Name, out int action)) continue;
                    if (actElem.Value.ValueKind == JsonValueKind.Array && actElem.Value.GetArrayLength() >= 2)
                    {
                        int sx = actElem.Value[0].GetInt32();
                        int sy = actElem.Value[1].GetInt32();
                        _stand[(long)model * 1000 + action] = new[] { sx, sy };
                        if (actElem.Value.GetArrayLength() >= 3)
                        {
                            int iv = actElem.Value[2].GetInt32();
                            if (iv > 0) _intval[(long)model * 1000 + action] = iv;
                        }
                        if (actElem.Value.GetArrayLength() >= 4)
                            _totaldir[(long)model * 1000 + action] = actElem.Value[3].GetInt32();
                        if (!_modelActions.TryGetValue(model, out var al)) { al = new List<int>(); _modelActions[model] = al; }
                        al.Add(action);
                    }
                }
            }
        }
        catch
        {
            // 忽略：回落画布中心即可
        }
    }

    /// <summary>查 (model, action) 的枢轴 standx/standy；查不到返回 false（调用方回落 SourceW/2）。</summary>
    public static bool TryGetStand(int model, int action, out int standx, out int standy)
    {
        if (_stand.TryGetValue((long)model * 1000 + action, out var v))
        {
            standx = v[0];
            standy = v[1];
            return true;
        }
        standx = 0;
        standy = 0;
        return false;
    }

    /// <summary>查 (model, action) 的逐帧毫秒 intval（原始传奇逐模型逐动作配置）。
    /// 查不到或值无效返回 false（调用方回落 Frame.Interval）。</summary>
    public static bool TryGetInterval(int model, int action, out int interval)
    {
        return _intval.TryGetValue((long)model * 1000 + action, out interval);
    }

    // ===================== 初始化 =====================

    /// <summary>拉取 resourceVersion.json（并发安全、失败可重试）。失败不抛，后续按无版本段路径兜底。</summary>
    public static Task InitializeAsync(CancellationToken ct = default)
    {
        LoadStandTable();
        if (_versionsReady) return Task.CompletedTask;
        var t = _initTask;
        if (t != null && !t.IsFaulted && !t.IsCanceled) return t;
        t = LoadVersionsAsync(ct);
        _initTask = t;
        return t;
    }

    private static async Task LoadVersionsAsync(CancellationToken ct)
    {
        // 分版参数与 AnimExportService.DownloadVersionJsonAsync 一致：本游戏版本 / manifest 版本 / 裸 URL。
        foreach (var suffix in new[]
                 {
                     "?v=" + Mir2OriginResource.GameVersion,
                     "?v=20260922111844",
                     ""
                 })
        {
            try
            {
                var bytes = await Mir2OriginResource.GetBytesAsync(VersionManifest + suffix, ct);
                if (bytes == null || bytes.Length == 0) continue;

                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                using (var doc = JsonDocument.Parse(bytes))
                {
                    foreach (var p in doc.RootElement.EnumerateObject())
                        if (p.Value.ValueKind == JsonValueKind.String)
                            dict[p.Name] = p.Value.GetString() ?? "";
                }
                if (dict.Count == 0) continue;

                _versions = dict;
                _versionsReady = true;
                return;
            }
            catch (OperationCanceledException) { throw; }
            catch { /* 换下一个分版参数重试 */ }
        }
    }

    /// <summary>查资源版本号；查不到返回 null（走无版本段路径）。</summary>
    public static string GetVersion(string resourceKey)
        => _versions.TryGetValue(resourceKey, out var v) && !string.IsNullOrEmpty(v) ? v : null;

    // ===================== 动作 / 方向（2config.modelInfo + DirectionType）=====================

    /// <summary>Crystal 的 MirAction → 原始传奇动作号。未登记的动作一律回退到待机帧。</summary>
    public static int MapAction(MirAction action)
    {
        switch (action)
        {
            case MirAction.Walking:
            case MirAction.Running:
            case MirAction.Pushed:
            case MirAction.DashL:
            case MirAction.DashR:
            case MirAction.Sneek:
            case MirAction.WalkingBow:
            case MirAction.RunningBow:
                return ActionWalk;

            case MirAction.Attack1:
            case MirAction.Attack2:
            case MirAction.Attack3:
            case MirAction.Attack4:
            case MirAction.Attack5:
            case MirAction.AttackRange1:
            case MirAction.AttackRange2:
            case MirAction.AttackRange3:
            case MirAction.Harvest:
            case MirAction.Mine:
            case MirAction.Spell:
            case MirAction.DashAttack:
            case MirAction.Lunge:
                return ActionAttack;

            case MirAction.Struck:
            case MirAction.Stoned:
                return ActionStruck;

            case MirAction.Die:
            case MirAction.Dead:
                return ActionDie;

            default:
                return ActionStand;
        }
    }

    /// <summary>
    /// 原始传奇 getDir(totaldir, dir)：把 8 个逻辑朝向映射为实际存储朝向。
    /// totaldir=1→NONE(4)；2→{1,3} 两向镜像；5→真帧 0..4，5..7 镜像成 3/2/1；其余→原样 8 向。
    /// 与 Mir2ReverseWeb/Services/Mir2Directions.cs 保持完全一致。
    /// </summary>
    public static int GetDir(int totaldir, int requestedDir)
    {
        int d = ((requestedDir % 8) + 8) % 8;
        switch (totaldir)
        {
            case 1: return 4;
            case 2: return d <= 2 ? 1 : (d <= 5 ? 3 : 1);
            case 5: return d >= 5 ? 8 - d : d;   // 5→3, 6→2, 7→1
            default: return d;
        }
    }

    // ===================== URL 规则（三类唯一出口）=====================

    /// <summary>模型帧（怪物/NPC）相对路径。ext = "png" / "json"。</summary>
    public static string ModelFramePath(int model, int action, int direction, string ext)
    {
        long sourceId = 1000L * model + 10L * action + direction;
        string ver = GetVersion("model/" + model);
        string rel = string.IsNullOrEmpty(ver)
            ? $"{AssetsRoot}model/{model}/{sourceId}.{ext}"
            : $"{AssetsRoot}{ver}/model/{model}/{sourceId}.{ext}";
        return rel + VerQuery;
    }

    /// <summary>物品图标相对路径。iconSubDir 默认 tool（1config.item 的 col19 图标号落在此目录）。</summary>
    public static string ItemIconPath(int iconId, string iconSubDir = "tool")
    {
        string key = $"icon/{iconSubDir}/{iconId}.png";
        string ver = GetVersion(key);
        string rel = string.IsNullOrEmpty(ver) ? AssetsRoot + key : $"{AssetsRoot}{ver}/{key}";
        return rel + VerQuery;
    }

    private static string CacheKey(Mir2ObjectKind kind, int id, int action, int direction)
        => $"{(int)kind}:{id}:{action}:{direction}";

    // ===================== 统一加载（去重 / 缓存 / 失败缓存）=====================

    /// <summary>
    /// 发起异步预取，不等待结果。绘制路径每帧调用本方法是安全的：命中缓存/在途任务直接返回，
    /// 已确认 404 的资源进 _missing 不再重试（避免每帧 404 风暴）。
    /// </summary>
    public static void Request(Mir2ObjectKind kind, int id, int action = ActionStand, int direction = 0, int priority = PriorityBackground)
    {
        if (id <= 0) return;
        var key = CacheKey(kind, id, action, direction);
        if (IsMissing(key)) return;

        var device = DXManager.GDevice;
        if (device == null) return;

        if (kind == Mir2ObjectKind.Item)
            _ = GetItemTextureAsync(id, device, priority: priority);
        else
            _ = GetSheetAsync(kind, id, action, direction, device, priority);
    }

    /// <summary>取得（或异步加载）一份模型独立图集。同 key 并发只发一次下载。</summary>
    public static Task<Mir2AtlasSheet> GetSheetAsync(Mir2ObjectKind kind, int id, int action, int direction,
                                                     GraphicsDevice device, int priority = PriorityBackground, CancellationToken ct = default)
    {
        if (kind == Mir2ObjectKind.Item || id <= 0 || device == null)
            return Task.FromResult<Mir2AtlasSheet>(null);

        var key = CacheKey(kind, id, action, direction);
        if (IsMissing(key)) return Task.FromResult<Mir2AtlasSheet>(null);
        if (_sheets.TryGetValue(key, out var cached) && cached != null) return cached;

        return _sheets.GetOrAdd(key, k => LoadSheetAsync(k, id, action, direction, device, priority));
    }

    private static async Task<Mir2AtlasSheet> LoadSheetAsync(string key, int model, int action, int direction,
                                                             GraphicsDevice device, int priority = PriorityBackground)
    {
        try
        {
            if (!_versionsReady) await InitializeAsync(CancellationToken.None);

            string pngPath = ModelFramePath(model, action, direction, "png");
            string jsonPath = ModelFramePath(model, action, direction, "json");

            var tex = await Mir2OriginResource.GetTextureAsync(pngPath, device, priority, CancellationToken.None);
            if (tex == null)
            {
                if (!_loggedMissing.ContainsKey(key))
                {
                    _loggedMissing[key] = 1;
                    KFramework.MonoGame.PrintTool.LogError(
                        $"[Mir2ObjectAtlas] 模型图集加载失败(404/不可达) key={key} url={Mir2OriginResource.ImageBaseUrl}/{pngPath}");
                }
                RecordFailure(key);
                return null;
            }

            byte[] jsonBytes = null;
            try { jsonBytes = await Mir2OriginResource.GetBytesAsync(jsonPath, priority, CancellationToken.None); }
            catch { /* 无 json 视为整图单帧 */ }

            string json;
            if (jsonBytes != null && jsonBytes.Length > 0)
            {
                json = Encoding.UTF8.GetString(jsonBytes);
            }
            else
            {
                // 没有配套帧表：把整张 png 当作只有 "0" 一帧的图集，复用 Mir2AtlasSheet 的解析/构建。
                json = $"{{\"file\":\"f.png\",\"frames\":{{\"0\":{{\"x\":0,\"y\":0,\"w\":{tex.Width},\"h\":{tex.Height}," +
                       $"\"sourceW\":{tex.Width},\"sourceH\":{tex.Height}}}}}}}";
            }

            var sheet = Mir2AtlasSheet.Parse(json);
            sheet.Build(tex);
            return sheet;
        }
        catch (Exception ex)
        {
            KFramework.MonoGame.PrintTool.LogError($"[Mir2ObjectAtlas] 图集加载失败 key={key}：{ex.Message}");
            RecordFailure(key);
            return null;
        }
    }

    /// <summary>取得（或异步加载）一张物品图标纹理。同 iconId 并发只发一次下载。</summary>
    public static Task<Texture2D> GetItemTextureAsync(int iconId, GraphicsDevice device,
                                                      string iconSubDir = "tool", int priority = PriorityItem, CancellationToken ct = default)
    {
        var key = CacheKey(Mir2ObjectKind.Item, iconId, 0, 0);
        if (iconId <= 0 || device == null) return Task.FromResult<Texture2D>(null);
        if (IsMissing(key)) return Task.FromResult<Texture2D>(null);
        if (_images.TryGetValue(key, out var cached) && cached != null) return cached;

        return _images.GetOrAdd(key, k => LoadItemAsync(k, iconId, device, iconSubDir, priority));
    }

    private static async Task<Texture2D> LoadItemAsync(string key, int iconId, GraphicsDevice device, string iconSubDir, int priority = PriorityItem)
    {
        try
        {
            if (!_versionsReady) await InitializeAsync(CancellationToken.None);
            var tex = await Mir2OriginResource.GetTextureAsync(ItemIconPath(iconId, iconSubDir), device, priority, CancellationToken.None);
            if (tex == null)
            {
                if (!_loggedMissing.ContainsKey(key))
                {
                    _loggedMissing[key] = 1;
                    KFramework.MonoGame.PrintTool.LogError(
                        $"[Mir2ObjectAtlas] 物品图标加载失败(404/不可达) key={key}");
                }
                RecordFailure(key);
            }
            return tex;
        }
        catch
        {
            RecordFailure(key);
            return null;
        }
    }

    /// <summary>判定某 key 是否已被确认缺失（不参与加载/绘制）。永久，不重试。</summary>
    private static bool IsMissing(string key) => _missing.ContainsKey(key);

    /// <summary>记录一次「确认缺失」：仅加入永久黑名单，不再发起任何请求。
    /// 本层不做重试——重试已由 浏览器原生层 / ContentManager.LoadCacheOrDownloadAsync /
    /// BrowserResource(内置 3 次退避) / 上层 Draw 每帧重请求 全部兜底；这里只是更底层重试耗尽后的终态。
    /// 日志去重由各调用点用 _loggedMissing 控制，避免每帧刷屏。</summary>
    private static void RecordFailure(string key)
    {
        _missing.TryAdd(key, 0);
        _sheets.TryRemove(key, out _);
        _images.TryRemove(key, out _);
    }

    // ===================== 预加载（对齐原始 ActionLoader 急/懒双队列）=====================

    /// <summary>预载某模型所有登记动作 × 所需方向（自己=急队列，出场/换装即齐，不等待绘制）。
    /// 方向数按 modelInfo.totaldir（1→仅 NONE(4)；5→0..4；其余→0..7）；模型不在内嵌表时兜底预载常见动作 0..7。
    /// 仅发起异步请求（命中缓存/在途任务直接返回），不阻塞；缺失帧由 _missing 缓存，不会重复请求。</summary>
    public static void PreloadModel(Mir2ObjectKind kind, int id, int priority = PriorityUser)
    {
        if (id <= 0) return;
        if (_modelActions.TryGetValue(id, out var acts))
        {
            foreach (var a in acts)
            {
                int td = _totaldir.TryGetValue((long)id * 1000 + a, out var t) ? t : 8;
                int maxDir = td == 1 ? 4 : 7;
                for (int d = 0; d <= maxDir; d++) Request(kind, id, a, d, priority);
            }
        }
        else
        {
            // 兜底只预载「各模型必有」的通用动作：1站 2走 3转 4攻 5跑 10受击 12死亡。
            // 原列表里的 21/23/99 在 CDN 上根本不存在（已 HEAD 核验 99→404），整批预载只会刷 404 并挤占
            // 浏览器到 CDN 的并发连接（见 SoundManager 注释“占满单域名”），间接导致死亡帧 action12 在怪物死亡
            // 那一刻因连接被占满而超时→被 _missing 拉黑，表现为“多杀几只后死亡动画就没了”。
            // 把 12 加进来：死亡帧在进场时即预载好，死亡瞬间不再临时请求，从根上避免该问题。
            foreach (var a in new[] { 1, 2, 3, 4, 5, 10, 12 })
                for (int d = 0; d <= 7; d++) Request(kind, id, a, d, priority);
        }
    }

    /// <summary>预载某模型单个动作的全部方向（后台懒队列用）：对齐原始 ActionLoader，实体转身/切换动作前先把同动作各方向暖好，避免转向跳帧。
    /// 方向数按该动作的 totaldir（1→仅 NONE(4)；5→0..4；其余→0..7）。</summary>
    public static void PreloadAction(Mir2ObjectKind kind, int id, int action, int priority = PriorityBackground)
    {
        if (id <= 0) return;
        int td = _totaldir.TryGetValue((long)id * 1000 + action, out var t) ? t : DefaultTotalDir;
        int maxDir = td == 1 ? 4 : (td == 5 ? 4 : 7);
        for (int d = 0; d <= maxDir; d++) Request(kind, id, action, d, priority);
    }

    /// <summary>该资源当前是否缺失（瞬时缺失在 TTL 内 true；永久缺失永远 true；超时/未缺失 false）。</summary>
    public static bool IsMissing(Mir2ObjectKind kind, int id, int action = ActionStand, int direction = 0)
        => IsMissing(CacheKey(kind, id, action, direction));

    // ===================== 统一取帧 / 绘制 =====================

    /// <summary>
    /// 从已就绪的独立图集里取第 frameIndex 帧。
    /// MovieClip（mc/res）图集按 PlayOrder 时间线取；普通 frames 图集按帧名排序取；物品返回整图单帧。
    /// 未加载完成返回 false —— 调用方直接跳过绘制即可，绝不在这里同步等网络。
    /// </summary>
    public static bool TryGetSprite(Mir2ObjectKind kind, int id, int action, int direction, int frameIndex,
                                    out KSpriteInfo sprite)
    {
        sprite = null;
        var key = CacheKey(kind, id, action, direction);
        if (IsMissing(key)) return false;

        if (kind == Mir2ObjectKind.Item)
        {
            if (!_images.TryGetValue(key, out var it) || it == null || !it.IsCompletedSuccessfully) return false;
            var tex = it.Result;
            if (tex == null) return false;
            sprite = new KSpriteInfo(key, tex, new Rectangle(0, 0, tex.Width, tex.Height),
                                     new Vector2(tex.Width, tex.Height), Vector2.Zero, false);
            return true;
        }

        if (!_sheets.TryGetValue(key, out var st) || st == null || !st.IsCompletedSuccessfully) return false;
        var sheet = st.Result;
        if (sheet == null || sheet.Frames == null || sheet.Frames.Count == 0) return false;

        if (!sheet.TryGetFrameName(frameIndex, out var frameName)) return false;
        return sheet.Frames.TryGetValue(frameName, out sprite);
    }

    /// <summary>
    /// 取已就绪的独立图集帧数（死亡/各动作动画推进需要知道“共几帧”）。
    /// 未加载完成 / 缺失返回 false（调用方回落默认帧数）。各方向同模型帧数一致，
    /// 优先取请求方向，缺失时回落 dir=0。
    /// </summary>
    public static bool TryGetFrameCount(Mir2ObjectKind kind, int id, int action, int direction, out int count)
    {
        count = 0;
        var key = CacheKey(kind, id, action, direction);
        if (_sheets.TryGetValue(key, out var st) && st != null && st.IsCompletedSuccessfully
            && st.Result != null && st.Result.Frames != null)
        {
            count = st.Result.Frames.Count;
            return true;
        }
        // 回落 dir=0（同模型各方向帧数相同）
        key = CacheKey(kind, id, action, 0);
        if (_sheets.TryGetValue(key, out st) && st != null && st.IsCompletedSuccessfully
            && st.Result != null && st.Result.Frames != null)
        {
            count = st.Result.Frames.Count;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 按注册点绘制模型帧（怪物/NPC/玩家）。该页游资源为 8 方向独立精灵（CDN 上 0..7 各自有真帧），
    /// 因此翻面请求优先改用对应 8 向真帧直接绘制（不再运行时翻面），避免“翻出的 3/2/1 镜像”与
    /// “真正的 5/6/7 姿势”不一致造成的转身跳变；仅当 8 向真帧确实缺失（5 向模型）才回落镜像逻辑。
    /// 枢轴取该模型该动作的 standx/standy（来自 2config.modelInfo，见内嵌 modelInfo_stand.json），
    /// 使脚底对齐角色点；查不到时回落画布中心。物品自动退化为直接按位置绘制。
    /// 数据由 Mir2AtlasSheet 解析（Anchors / SourceW / SourceH），本类只负责统一调度。
    /// </summary>
    public static bool DrawAnchored(Mir2ObjectKind kind, int id, int action, int direction, int frameIndex,
                                    int screenX, int screenY, KFramework.MonoGame.Color? colour = null,
                                    SpriteEffects effects = SpriteEffects.None, float scale = 1f)
    {
        // 该页游资源为 8 方向独立精灵：逻辑方向 5/6/7 在 CDN 上各自有真帧，并非“5 向 + 运行时翻面”。
        // 翻面请求时，若对应的 8 向真帧已就绪（或尚未拉取），则直接用它、不再运行时翻面，
        // 避免“翻出来的 3/2/1 镜像”与“真正的 5/6/7 姿势”不一致导致的转身跳变。
        // 若 8 向真帧确实缺失（5 向模型），回落到原 5 向镜像逻辑，行为不变。
        int loadDir = direction;
        bool flip = (effects & SpriteEffects.FlipHorizontally) != 0;
        if (flip)
        {
            // 8 向独立精灵：逻辑方向 5/6/7 在 CDN 各自有真帧。把镜像请求改写成对应 8 向真帧（rawDir），
            // 交给下方统一逻辑加载/绘制；若该 8 向真帧确已 404（5 向模型），_missing 命中后回落镜像。
            int rawDir = 8 - direction;
            var key8 = CacheKey(kind, id, action, rawDir);
            bool has8 = _sheets.TryGetValue(key8, out var s8) && s8 != null && s8.IsCompletedSuccessfully
                        && s8.Result != null && s8.Result.Frames != null && s8.Result.Frames.Count > 0;
            if (has8 || !IsMissing(key8))
            {
                loadDir = rawDir;
                flip = false;
            }
        }
        var drawEffects = flip ? effects : (effects & ~SpriteEffects.FlipHorizontally);

        var key = CacheKey(kind, id, action, loadDir);
        if (IsMissing(key)) return false;

        if (kind == Mir2ObjectKind.Item)
            return DrawSpriteAt(kind, id, action, loadDir, frameIndex, screenX, screenY, colour, drawEffects);

        if (!_sheets.TryGetValue(key, out var st) || st == null || !st.IsCompletedSuccessfully) { Request(kind, id, action, loadDir); return false; }
        var sheet = st.Result;
        if (sheet == null || sheet.Frames == null || sheet.Frames.Count == 0) return false;
        if (!sheet.TryGetFrameName(frameIndex, out var frameName)) return false;
        if (!sheet.Frames.TryGetValue(frameName, out var sprite) || sprite == null) return false;

        int ox = 0, oy = 0;
        if (sheet.Anchors != null && sheet.Anchors.TryGetValue(frameName, out var a))
        {
            ox = (int)a.X;
            oy = (int)a.Y;
        }
        var src = sprite.SourceRectangle;
        // 枢轴取该模型该动作的 standx/standy（原始传奇每个部件按自身枢轴对齐角色点，
        // 见 2config.modelInfo）；查不到时回落画布中心，行为与旧逻辑一致。
        int cx, cy;
        if (!TryGetStand(id, action, out cx, out cy))
        {
            cx = sheet.SourceW > 0 ? sheet.SourceW / 2 : src.Width / 2;
            cy = sheet.SourceH > 0 ? sheet.SourceH / 2 : src.Height / 2;
        }
        int x = screenX + ox - cx;
        int y = screenY + oy - cy;

        // 按 scale 缩放：水平中心保持不变，脚底(底边)锚定到 (screenX,screenY)，缩放时不上下漂移。
        int dw = (int)Math.Round(src.Width * scale);
        int dh = (int)Math.Round(src.Height * scale);
        int dx = x + (src.Width - dw) / 2;
        // 镜像（仅 5 向模型回落路径会走到这里）：把目标矩形整体以 screenX 为轴镜像，脚底保持不动；
        // DXManager.Draw 的 FlipHorizontally 在矩形内翻面，二者合起来即绕角色点翻。
        if (flip)
            dx = 2 * screenX - dx - dw;
        int dy = y + (src.Height - dh);
        DXManager.Draw(sprite.Texture, src, new Rectangle(dx, dy, Math.Max(1, dw), Math.Max(1, dh)),
                       colour ?? KFramework.MonoGame.Color.White, MirBlend.Normal.ToBlendState(), drawEffects);
        return true;
    }

    /// <summary>
    /// 按位置绘制：目标尺寸取帧的原始尺寸（物品格子最常用）。
    /// 统一绘制出口，走与 MirImageControl 图集分支同一条 DXManager.Draw 重载。
    /// 资源未就绪返回 false，调用方无需任何额外处理。
    /// </summary>
    public static bool DrawSpriteAt(Mir2ObjectKind kind, int id, int action, int direction, int frameIndex,
                                    int x, int y, KFramework.MonoGame.Color? colour = null,
                                    SpriteEffects effects = SpriteEffects.None)
    {
        if (!TryGetSprite(kind, id, action, direction, frameIndex, out var sprite) || sprite == null)
            return false;

        var src = sprite.SourceRectangle;
        DXManager.Draw(sprite.Texture, src, new Rectangle(x, y, src.Width, src.Height),
                       colour ?? KFramework.MonoGame.Color.White, MirBlend.Normal.ToBlendState(), effects);
        return true;
    }

    /// <summary>获取模型帧的实际绘制矩形（屏幕坐标），用于鼠标命中检测；与 DrawAnchored 完全一致的坐标公式
    /// （8 向真帧优先、standx/standy 枢轴、镜像绕角色点），保证命中框与显示像素完全对齐。
    /// 资源未就绪或缺失返回 false。mirror 表示逻辑方向 5..7（需绕角色点翻面）。</summary>
    public static bool TryGetDrawRect(Mir2ObjectKind kind, int id, int action, int direction, int frameIndex,
                                      int screenX, int screenY, out Rectangle rect, float scale = 1f, bool mirror = false)
    {
        rect = default(Rectangle);
        if (kind == Mir2ObjectKind.Item) return false;

        // 与 DrawAnchored 一致：优先用 8 向真帧，否则回落 5 向镜像。
        int loadDir = direction;
        bool flip = mirror;
        if (flip)
        {
            int rawDir = 8 - direction;
            var key8 = CacheKey(kind, id, action, rawDir);
            bool has8 = _sheets.TryGetValue(key8, out var s8) && s8 != null && s8.IsCompletedSuccessfully
                        && s8.Result != null && s8.Result.Frames != null && s8.Result.Frames.Count > 0;
            if (has8) { loadDir = rawDir; flip = false; }
            else if (!IsMissing(key8)) { loadDir = rawDir; flip = false; }
        }

        var key = CacheKey(kind, id, action, loadDir);
        if (IsMissing(key)) return false;
        if (!_sheets.TryGetValue(key, out var st) || st == null || !st.IsCompletedSuccessfully) return false;
        var sheet = st.Result;
        if (sheet == null || sheet.Frames == null || sheet.Frames.Count == 0) return false;
        if (!sheet.TryGetFrameName(frameIndex, out var frameName)) return false;
        if (!sheet.Frames.TryGetValue(frameName, out var sprite) || sprite == null) return false;
        int ox = 0, oy = 0;
        if (sheet.Anchors != null && sheet.Anchors.TryGetValue(frameName, out var a))
        { ox = (int)a.X; oy = (int)a.Y; }
        var src = sprite.SourceRectangle;
        int cx, cy;
        if (!TryGetStand(id, action, out cx, out cy))
        {
            cx = sheet.SourceW > 0 ? sheet.SourceW / 2 : src.Width / 2;
            cy = sheet.SourceH > 0 ? sheet.SourceH / 2 : src.Height / 2;
        }
        int x = screenX + ox - cx;
        int y = screenY + oy - cy;
        int dw = (int)Math.Round(src.Width * scale);
        int dh = (int)Math.Round(src.Height * scale);
        int dx = x + (src.Width - dw) / 2;
        if (flip) dx = 2 * screenX - dx - dw;
        int dy = y + (src.Height - dh);
        rect = new Rectangle(dx, dy, Math.Max(1, dw), Math.Max(1, dh));
        return true;
    }
}
