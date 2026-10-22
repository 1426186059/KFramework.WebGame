using KFramework.MonoGame;
using KFramework.MonoGameExtend;
using System.Text;
using System.Text.Json.Nodes;
using Color = KFramework.MonoGame.Color;
using Rectangle = KFramework.MonoGame.Rectangle;

public static class Mir2AtlasLibrary
{
    private static readonly Dictionary<string, Mir2AtlasSheet> _sheets =
        new Dictionary<string, Mir2AtlasSheet>(StringComparer.OrdinalIgnoreCase);

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
            PrintTool.Log($"[Mir2Atlas] RES 配置已加载：资源 {_fsData.Count}，分组 {_groups.Count}，子资源别名 {_alias.Count}");
        }
        catch (Exception ex)
        {
            PrintTool.LogError("[Mir2Atlas] RES 配置(default.res.json)加载失败，回退为直接用原始 key：" + ex);
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
                PrintTool.Log("[Mir2Atlas] 初始化完成（manifest + RES 配置）。");
            }
            catch (Exception ex)
            {
                PrintTool.LogError("[Mir2Atlas] 初始化失败（manifest/RES 配置）：" + ex);
                _initTask = null;
            }
        }, ct);
        return _initTask;
    }
    
    private static (string key, string frame) ResolveName(string nameOrKey)
    {
        if (_alias != null && _alias.TryGetValue(nameOrKey, out var spec))
        {
            var h = spec.IndexOf('#');
            var sheetName = spec.Substring(0, h);
            var frame = spec.Substring(h + 1);
            if (_fsData.TryGetValue(sheetName, out var it))
            {
                return (it.ManifestKey, frame);
            }
        }

        if (_fsData != null && _fsData.TryGetValue(nameOrKey, out var item))
        {
            return (item.ManifestKey, null);
        }

        return (nameOrKey, null);
    }
    
    public static async Task<Mir2AtlasSheet> GetSheetAsync(string nameOrKey, GraphicsDevice device, CancellationToken ct = default)
    {
        await EnsureManifestAsync(ct);
        await EnsureResConfigAsync(ct);
        var (key, _) = ResolveName(nameOrKey);
        if (_sheets.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var jsonPath = Mir2OriginResource.ResolveManifestAssetPath(key);
        var jsonBytes = await Mir2OriginResource.GetBytesAsync(jsonPath, ct: ct);
        if (jsonBytes == null || jsonBytes.Length == 0)
        {
            throw new InvalidOperationException(
                $"[Mir2Atlas] 图集 JSON 下载为空：nameOrKey={nameOrKey}，resolvedKey={key}，resolvedPath={jsonPath}。" +
                $"若 resolvedPath 仍是逻辑路径(含 resource/assets/...)且 404，说明 LocalMirrorUsesKeyPath 与 base 不匹配；" +
                $"若 resolvedPath 已是哈希路径(resource/xx/...)仍空，说明该 key 不在 manifest 或资源缺失。");
        }

        var sheet = Mir2AtlasSheet.Parse(Encoding.UTF8.GetString(jsonBytes));
        var pngKey = SheetPngKey(key, sheet.File);
        var pngPath = Mir2OriginResource.ResolveManifestAssetPath(pngKey);
        var texture = await Mir2OriginResource.GetTextureAsync(pngPath, device, ct: ct);

        sheet.Build(texture);
        _sheets[key] = sheet;
        PrintTool.Log($"[Mir2Atlas] 已加载图集 {key}：{sheet.Frames.Count} 帧");
        return sheet;
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
        PrintTool.Log($"[Mir2Atlas] 已加载直链图集 {jsonRelativePath}：{sheet.Frames.Count} 帧");
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
