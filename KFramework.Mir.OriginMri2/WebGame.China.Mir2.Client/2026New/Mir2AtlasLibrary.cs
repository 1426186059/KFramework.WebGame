using KFramework.MonoGame;
using KFramework.MonoGameExtend;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json.Nodes;
using Color = KFramework.MonoGame.Color;
using Rectangle = KFramework.MonoGame.Rectangle;

public static class Mir2AtlasLibrary
{
    private static readonly Dictionary<string, Mir2AtlasSheet> _sheets =
        new Dictionary<string, Mir2AtlasSheet>(StringComparer.OrdinalIgnoreCase);

    // 在途图集加载任务（按 sheet key 去重）：多个控件/调用方同时请求同一图集时，只发起一次下载，
    // 大家都 await 同一个 Task<Mir2AtlasSheet>，避免重复 HTTP 拉取。
    private static readonly ConcurrentDictionary<string, Task<Mir2AtlasSheet>> _sheetTasks =
        new ConcurrentDictionary<string, Task<Mir2AtlasSheet>>(StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, Mir2ResItem> _fsData;
    private static Dictionary<string, string> _alias;
    private static Dictionary<string, string[]> _groups;
    private const string ResourceRoot = "resource/";
    private static Task _initTask;
    public static Mir2AtlasManifest Manifest => Mir2OriginResource.Manifest;

    public sealed class Mir2ResItem
    {
        public string Name;     // "advance-sheet_json"
        public string Url;      // "assets/gameui4/window-sheet/advance-sheet.json"（相对 resource/）
        public string Type;     // "sheet" / "image" / ...
        public string[] Subkeys;// ["adv_fail","adv_per","adv_suc"]
        public string Root = ResourceRoot;
        public string ManifestKey => Root + Url.TrimStart('/'); // -> "resource/assets/gameui4/..."
    }

    public static async Task EnsureManifestAsync(CancellationToken ct = default)
    {
        if (Mir2OriginResource.Manifest == null)
            await Mir2OriginResource.LoadManifestAsync(ct);
    }
    
    private static async Task EnsureResConfigAsync(CancellationToken ct)
    {
        if (_fsData != null)
        {
            return;
        }
        
        _fsData = new Dictionary<string, Mir2ResItem>(StringComparer.OrdinalIgnoreCase);
        _alias = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _groups = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var bytes = await Mir2OriginResource.GetBytesAsync(
                Mir2OriginResource.ResolveManifestAssetPath("resource/default.res.json"), ct: ct);
            
            if (bytes == null || bytes.Length == 0)
            {
                return;
            }

            if (JsonNode.Parse(Encoding.UTF8.GetString(bytes)) is JsonObject root)
            {
                if (root["resources"] is JsonArray rs)
                {
                    foreach (var item in rs)
                    {
                        var name = item["name"]?.GetValue<string>();
                        var url = item["url"]?.GetValue<string>();
                        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(url))
                        {
                            continue;
                        }

                        var type = item["type"]?.GetValue<string>() ?? "";
                        var subRaw = item["subkeys"]?.GetValue<string>();
                        var subs = string.IsNullOrEmpty(subRaw)
                            ? Array.Empty<string>()
                            : subRaw.Split(',');

                        var it = new Mir2ResItem { Name = name, Url = url, Type = type, Subkeys = subs };
                        _fsData[name] = it;
                        foreach (var t in subs)
                        {
                            if (string.IsNullOrEmpty(t)) continue;
                            _alias[t] = name + "#" + t;
                            _alias[name + "." + t] = name + "#" + t;
                        }
                    }
                }

                if (root["groups"] is JsonArray gs)
                {
                    foreach (var item in gs)
                    {
                        var gname = item["name"]?.GetValue<string>();
                        var keys = item["keys"]?.GetValue<string>() ?? "";
                        if (!string.IsNullOrEmpty(gname))
                        {
                            _groups[gname] = keys == "" ? Array.Empty<string>() : keys.Split(',');

                        }
                    }
                }
            }
            //PrintTool.Log($"[Mir2Atlas] RES 配置已加载：资源 {_fsData.Count}，分组 {_groups.Count}，子资源别名 {_alias.Count}");
        }
        catch (Exception ex)
        {
            //PrintTool.LogError("[Mir2Atlas] RES 配置(default.res.json)加载失败，回退为直接用原始 key：" + ex);
        }
    }

    public static Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initTask != null && !_initTask.IsFaulted && !_initTask.IsCanceled)
        {
            return _initTask;
        }

        _initTask = Task.Run(async () =>
        {
            try
            {
                await EnsureManifestAsync(ct).ConfigureAwait(false);
                await EnsureResConfigAsync(ct).ConfigureAwait(false);
                //PrintTool.Log("[Mir2Atlas] 初始化完成（manifest + RES 配置）。");
            }
            catch (Exception ex)
            {
                //PrintTool.LogError("[Mir2Atlas] 初始化失败（manifest/RES 配置）：" + ex);
                _initTask = null;
            }
        }, ct);
        return _initTask;
    }
    
    internal static (string key, string frame) ResolveName(string nameOrKey)
    {
        // 先按 # 拆分出 sheet 名与帧名：无论是否命中别名表都先拆，
        // 否则 "sheet#frame" 会被整体当成 key（ResolveManifestAssetPath 原样返回 -> 404）。
        string sheetName = nameOrKey;
        string frame = null;
        int h = nameOrKey.IndexOf('#');
        if (h >= 0)
        {
            sheetName = nameOrKey.Substring(0, h);
            frame = nameOrKey.Substring(h + 1);
        }

        // 别名表命中（如 "帧名" -> "sheetJsonKey#帧名"）：用别名指向的 sheetJsonKey 查 _fsData 得到 ManifestKey。
        if (_alias != null && _alias.TryGetValue(nameOrKey, out var spec) && spec != null)
        {
            int ah = spec.IndexOf('#');
            if (ah >= 0)
            {
                var aliasSheet = spec.Substring(0, ah);
                var aliasFrame = spec.Substring(ah + 1);
                if (_fsData.TryGetValue(aliasSheet, out var it))
                    return (it.ManifestKey, aliasFrame);
            }
        }

        // 直接按 sheet 名查 _fsData；裸名（如 y_homeUI-sheet）没有 _json 后缀时，
        // 兜底补 _json（如 y_homeUI-sheet_json）以匹配 RES 配置里的实际键。
        if (_fsData != null)
        {
            if (_fsData.TryGetValue(sheetName, out var item))
                return (item.ManifestKey, frame);
            if (_fsData.TryGetValue(sheetName + "_json", out var item2))
                return (item2.ManifestKey, frame);
        }

        return (nameOrKey, frame);
    }
    
    // 按 sheet key 去重在途加载：多个控件/调用方同时请求同一图集时，只发起一次下载，
    // 大家都 await 同一个 Task<Mir2AtlasSheet>，避免重复 HTTP 拉取（见 _sheetTasks）。
    // 注意：共享任务不使用调用方的 ct —— 一个控件取消不应连累其他共享同一图集的控件；
    // 各控件的「取消是否应用结果」在其自身层（见 MirImageControl.LoadAtlasAsync）处理。
    public static Task<Mir2AtlasSheet> GetSheetAsync(string nameOrKey, GraphicsDevice device, CancellationToken ct = default)
    {
        var (key, _) = ResolveName(nameOrKey);
        if (_sheets.TryGetValue(key, out var existing))
            return Task.FromResult(existing);
        return _sheetTasks.GetOrAdd(key, k => LoadSheetCoreAsync(k, device));
    }

    // 实际加载（被 GetOrAdd 合并调用，保证同一 key 同时只下载一次）。
    private static async Task<Mir2AtlasSheet> LoadSheetCoreAsync(string key, GraphicsDevice device)
    {
        try
        {
            await EnsureManifestAsync(CancellationToken.None);
            await EnsureResConfigAsync(CancellationToken.None);
            if (_sheets.TryGetValue(key, out var existing))
                return existing;

            var jsonPath = Mir2OriginResource.ResolveManifestAssetPath(key);
            var jsonBytes = await Mir2OriginResource.GetBytesAsync(jsonPath, ct: CancellationToken.None);
            if (jsonBytes == null || jsonBytes.Length == 0)
            {
                throw new InvalidOperationException(
                    $"[Mir2Atlas] 图集 JSON 下载为空：key={key}，resolvedPath={jsonPath}。" +
                    $"若 resolvedPath 仍是逻辑路径(含 resource/assets/...)且 404，说明 LocalMirrorUsesKeyPath 与 base 不匹配；" +
                    $"若已是哈希路径(resource/xx/...)仍空，说明该 key 不在 manifest 或资源缺失。");
            }

            var sheet = Mir2AtlasSheet.Parse(Encoding.UTF8.GetString(jsonBytes));
            var pngKey = SheetPngKey(key, sheet.File);
            var pngPath = Mir2OriginResource.ResolveManifestAssetPath(pngKey);
            var texture = await Mir2OriginResource.GetTextureAsync(pngPath, device, ct: CancellationToken.None);
            if (texture == null)
            {
                //KFramework.MonoGame.PrintTool.LogError(
                //    $"[Mir2Atlas] 图集 PNG 贴图为空：pngPath={pngPath}（resolvedPath={Mir2OriginResource.ResolveManifestAssetPath(pngKey)}，检查是否在 manifest / CDN 404 或解码失败）");
            }

            sheet.Build(texture);
            _sheets[key] = sheet;
            //PrintTool.Log($"[Mir2Atlas] 已加载图集 {key}：{sheet.Frames.Count} 帧");
            return sheet;
        }
        catch (Exception)
        {
            _sheetTasks.TryRemove(key, out _); // 失败移除，允许下次重试
            throw;
        }
    }
    
    public static async Task<KSpriteInfo> GetResSpriteAsync(string nameOrFrame, GraphicsDevice device, CancellationToken ct = default)
    {
        await EnsureResConfigAsync(ct);
        await EnsureManifestAsync(ct);
        var (key, frame) = ResolveName(nameOrFrame);
        var sheet = await GetSheetAsync(key, device, ct);
        if (frame != null && sheet.Frames.TryGetValue(frame, out var fi))
        {
            return fi;
        }
        return null;
    }
    
    public static async Task<Texture2D> GetImageTextureAsync(string name, GraphicsDevice device, CancellationToken ct = default)
    {
        await EnsureManifestAsync(ct);
        await EnsureResConfigAsync(ct);
        var (key, _) = ResolveName(name); // 图片资源 -> 其 manifestKey（无帧）
        return await Mir2OriginResource.GetManifestAssetTextureAsync(key, device, ct);
    }
    
    public static async Task<Mir2AtlasSheet> GetSheetByPathsAsync(string jsonRelativePath, string pngRelativePath, GraphicsDevice device, CancellationToken ct = default)
    {
        await EnsureManifestAsync(ct); // 仅保证 Manifest 字段存在（不影响直链解析）
        var jsonBytes = await Mir2OriginResource.GetBytesAsync(jsonRelativePath, ct: ct);
        if (jsonBytes == null || jsonBytes.Length == 0) return null;
        var sheet = Mir2AtlasSheet.Parse(Encoding.UTF8.GetString(jsonBytes));
        var texture = await Mir2OriginResource.GetTextureAsync(pngRelativePath, device, ct: ct);
        sheet.Build(texture);
        //PrintTool.Log($"[Mir2Atlas] 已加载直链图集 {jsonRelativePath}：{sheet.Frames.Count} 帧");
        return sheet;
    }

    public static bool TryGetFrame(string sheetOrFrameName, string frameName, out KSpriteInfo frame)
    {
        var (key, aliasFrame) = ResolveName(sheetOrFrameName);
        frame = null;
        if (!_sheets.TryGetValue(key, out var s)) return false;
        var fn = frameName ?? aliasFrame;
        return fn != null && s.Frames.TryGetValue(fn, out frame);
    }

    public static string[] GetGroupResourceNames(string groupName)
    { 
        return (_groups != null && _groups.TryGetValue(groupName, out var names)) ? names : Array.Empty<string>();
    }

    public static void DrawFrame(this SpriteBatch batch, KSprite sprite, Rectangle targetRect, Color color)
    {
        var r = sprite.Rectangle;
        batch.Draw(sprite.Texture, targetRect, r, color);
    }

    public static void DrawFrame(this SpriteBatch batch, Mir2AtlasSheet sheet, string frameName, int x, int y, Color color)
    {
        if (sheet.Frames.TryGetValue(frameName, out var s))
        {
            Rectangle targetRect  = new Rectangle(x, y, (int)s.Size.X, (int)s.Size.Y);
            DrawFrame(batch, s, targetRect, color);
        }
    }

    private static string SheetPngKey(string sheetKey, string pngFile)
    {
        var idx = sheetKey.LastIndexOf('/');
        var dir = idx >= 0 ? sheetKey.Substring(0, idx) : "";
        return dir.Length == 0 ? pngFile : dir + "/" + pngFile;
    }
}
