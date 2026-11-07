using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Client.MirGraphics;              // DXManager / MirBlend
using KFramework.MonoGame;
using KFramework.MonoGameExtend;       // KSpriteInfo
using Rectangle = KFramework.MonoGame.Rectangle;

/// <summary>需要走原始传奇 CDN 独立图集的三类对象。</summary>
public enum Mir2ObjectKind
{
    /// <summary>怪物：model/{模型号}/{sourceId}.png|json，按动作+方向分图集。</summary>
    Monster,
    /// <summary>NPC：与怪物同一套 model 资源，独立图集、独立缓存键。</summary>
    NPC,
    /// <summary>物品：icon/tool/{图标号}.png，单张图即一帧，无独立 json。</summary>
    Item
}

/// <summary>
/// 原始传奇「怪物 / NPC / 物品」三类【独立图集】的统一资源管理器。
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
    private static readonly ConcurrentDictionary<string, byte> _missing =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>版本表是否就绪。未就绪时仍可用无版本段路径加载，只是可能 404。</summary>
    public static bool IsReady => _versionsReady;

    // ===================== 初始化 =====================

    /// <summary>拉取 resourceVersion.json（并发安全、失败可重试）。失败不抛，后续按无版本段路径兜底。</summary>
    public static Task InitializeAsync(CancellationToken ct = default)
    {
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
    public static void Request(Mir2ObjectKind kind, int id, int action = ActionStand, int direction = 0)
    {
        if (id <= 0) return;
        var key = CacheKey(kind, id, action, direction);
        if (_missing.ContainsKey(key)) return;

        var device = DXManager.GDevice;
        if (device == null) return;

        if (kind == Mir2ObjectKind.Item)
            _ = GetItemTextureAsync(id, device);
        else
            _ = GetSheetAsync(kind, id, action, direction, device);
    }

    /// <summary>取得（或异步加载）一份模型独立图集。同 key 并发只发一次下载。</summary>
    public static Task<Mir2AtlasSheet> GetSheetAsync(Mir2ObjectKind kind, int id, int action, int direction,
                                                     GraphicsDevice device, CancellationToken ct = default)
    {
        if (kind == Mir2ObjectKind.Item || id <= 0 || device == null)
            return Task.FromResult<Mir2AtlasSheet>(null);

        var key = CacheKey(kind, id, action, direction);
        if (_missing.ContainsKey(key)) return Task.FromResult<Mir2AtlasSheet>(null);
        if (_sheets.TryGetValue(key, out var cached) && cached != null) return cached;

        return _sheets.GetOrAdd(key, k => LoadSheetAsync(k, id, action, direction, device));
    }

    private static async Task<Mir2AtlasSheet> LoadSheetAsync(string key, int model, int action, int direction,
                                                             GraphicsDevice device)
    {
        try
        {
            if (!_versionsReady) await InitializeAsync(CancellationToken.None);

            string pngPath = ModelFramePath(model, action, direction, "png");
            string jsonPath = ModelFramePath(model, action, direction, "json");

            var tex = await Mir2OriginResource.GetTextureAsync(pngPath, device, CancellationToken.None);
            if (tex == null)
            {
                MarkMissing(key);
                return null;
            }

            byte[] jsonBytes = null;
            try { jsonBytes = await Mir2OriginResource.GetBytesAsync(jsonPath, CancellationToken.None); }
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
            MarkMissing(key);
            return null;
        }
    }

    /// <summary>取得（或异步加载）一张物品图标纹理。同 iconId 并发只发一次下载。</summary>
    public static Task<Texture2D> GetItemTextureAsync(int iconId, GraphicsDevice device,
                                                      string iconSubDir = "tool", CancellationToken ct = default)
    {
        var key = CacheKey(Mir2ObjectKind.Item, iconId, 0, 0);
        if (iconId <= 0 || device == null) return Task.FromResult<Texture2D>(null);
        if (_missing.ContainsKey(key)) return Task.FromResult<Texture2D>(null);
        if (_images.TryGetValue(key, out var cached) && cached != null) return cached;

        return _images.GetOrAdd(key, k => LoadItemAsync(k, iconId, device, iconSubDir));
    }

    private static async Task<Texture2D> LoadItemAsync(string key, int iconId, GraphicsDevice device, string iconSubDir)
    {
        try
        {
            if (!_versionsReady) await InitializeAsync(CancellationToken.None);
            var tex = await Mir2OriginResource.GetTextureAsync(ItemIconPath(iconId, iconSubDir), device, CancellationToken.None);
            if (tex == null) MarkMissing(key);
            return tex;
        }
        catch
        {
            MarkMissing(key);
            return null;
        }
    }

    private static void MarkMissing(string key)
    {
        _missing[key] = 1;
        _sheets.TryRemove(key, out _);
        _images.TryRemove(key, out _);
    }

    /// <summary>该资源已确认缺失（不再重试）。</summary>
    public static bool IsMissing(Mir2ObjectKind kind, int id, int action = ActionStand, int direction = 0)
        => _missing.ContainsKey(CacheKey(kind, id, action, direction));

    /// <summary>该资源是否还在下载（未就绪、未失败）。用于上层决定是否等待重绘。</summary>
    public static bool IsPending(Mir2ObjectKind kind, int id, int action = ActionStand, int direction = 0)
    {
        var key = CacheKey(kind, id, action, direction);
        if (_missing.ContainsKey(key)) return false;
        if (_sheets.TryGetValue(key, out var t)) return t != null && !t.IsCompleted;
        if (_images.TryGetValue(key, out var i)) return i != null && !i.IsCompleted;
        return true;
    }

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
        if (_missing.ContainsKey(key)) return false;

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

        int n = Math.Abs(frameIndex);
        string frameName;
        if (sheet.PlayOrder != null && sheet.PlayOrder.Count > 0)
        {
            frameName = sheet.PlayOrder[n % sheet.PlayOrder.Count];
        }
        else
        {
            var names = new List<string>(sheet.Frames.Keys);
            names.Sort(StringComparer.Ordinal);
            frameName = names[n % names.Count];
        }
        return sheet.Frames.TryGetValue(frameName, out sprite);
    }

    /// <summary>
    /// 按注册点绘制模型帧（怪物/NPC）：模型图集 frames 数组里的 offX/offY 是帧在虚拟画布
    /// sourceW×sourceH 中的位置，以画布中心作为角色脚底对齐到 (screenX, screenY)，
    /// 保证人物站在格子上而不是整张画布左上角漂出去。物品自动退化为直接按位置绘制。
    /// 数据由 Mir2AtlasSheet 解析（Anchors / SourceW / SourceH），本类只负责统一调度。
    /// </summary>
    public static bool DrawAnchored(Mir2ObjectKind kind, int id, int action, int direction, int frameIndex,
                                    int screenX, int screenY, KFramework.MonoGame.Color? colour = null,
                                    SpriteEffects effects = SpriteEffects.None)
    {
        var key = CacheKey(kind, id, action, direction);
        if (_missing.ContainsKey(key)) return false;

        if (kind == Mir2ObjectKind.Item)
            return DrawSpriteAt(kind, id, action, direction, frameIndex, screenX, screenY, colour, effects);

        if (!_sheets.TryGetValue(key, out var st) || st == null || !st.IsCompletedSuccessfully) return false;
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
        int cx = sheet.SourceW > 0 ? sheet.SourceW / 2 : src.Width / 2;
        int cy = sheet.SourceH > 0 ? sheet.SourceH / 2 : src.Height / 2;
        int x = screenX + ox - cx;
        int y = screenY + oy - cy;

        DXManager.Draw(sprite.Texture, src, new Rectangle(x, y, src.Width, src.Height),
                       colour ?? KFramework.MonoGame.Color.White, MirBlend.Normal.ToBlendState(), effects);
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

    /// <summary>按指定目标矩形绘制（缩放/拉伸用）。</summary>
    public static bool DrawSprite(Mir2ObjectKind kind, int id, int action, int direction, int frameIndex,
                                  Rectangle dest, KFramework.MonoGame.Color? colour = null,
                                  SpriteEffects effects = SpriteEffects.None)
    {
        if (!TryGetSprite(kind, id, action, direction, frameIndex, out var sprite) || sprite == null)
            return false;

        DXManager.Draw(sprite.Texture, sprite.SourceRectangle, dest,
                       colour ?? KFramework.MonoGame.Color.White, MirBlend.Normal.ToBlendState(), effects);
        return true;
    }

    /// <summary>帧的原始尺寸（用于布局/命中框）；未就绪返回 Empty。</summary>
    public static KFramework.MonoGame.Size GetSpriteSize(Mir2ObjectKind kind, int id, int action, int direction, int frameIndex)
    {
        if (!TryGetSprite(kind, id, action, direction, frameIndex, out var sprite) || sprite == null)
            return KFramework.MonoGame.Size.Empty;
        var r = sprite.SourceRectangle;
        return new KFramework.MonoGame.Size(r.Width, r.Height);
    }
}
