using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Nodes;
using KFramework.MonoGame;
using Color = KFramework.MonoGame.Color;
using Rectangle = KFramework.MonoGame.Rectangle;

/// <summary>
/// 原始传奇图集加载 / 解析管理器：在 Mir2OriginResource（取数）之上，把「manifest 索引 + sheet 精灵表」
/// 组装成可用的图集。每张图集 = 整页 Texture2D（sheet.png）+ 若干 KSpriteInfo（每帧：整页贴图 + 源矩形 + 原始尺寸）。
///
/// 资源名解析忠实复刻原版 Egret 的 RES.ResourceConfig（见 main10.25341.1.min.js 的 parseConfig）：
///   RES.loadConfig("resource/default.res.json", "resource/") —— 资源根 resource/。
///   解析出三件套：
///     • fsData : 资源名 -> 资源项{name,url,type,subkeys,root}        （本类的 _fsData）
///     • alias  : 子资源(帧)短名 -> "sheetName#frameName"             （本类的 _alias；裸帧名与 "sheetName.frameName" 都登记）
///     • groups : 分组名 -> 成员资源名[]                              （本类的 _groups）
///   于是 RES.getRes("advance-sheet_json") 取整张表、RES.getRes("adv_fail") 直接取那一帧，都与原版一致。
///
/// 用法（入参都是原版短名，不用写长路径）：
///   var sheet = await Mir2AtlasLibrary.GetSheetAsync("advance-sheet_json", device, ct); // 整张表
///   var frame = await Mir2AtlasLibrary.GetResSpriteAsync("adv_fail", device, ct);       // 单帧（getRes 风格）
///   Mir2AtlasLibrary.DrawFrame(batch, frame, x, y, Color.White);
///
/// 与现有 .Lib 体系（MLibrary/MImage）解耦：这是网页版 Egret 图集，不是 WIL。
/// 注：原版还有一层 clientResourceVersionConfig 的 URL 重定向（仅影响部分“新版”资源），本类暂不复制该重定向。
/// </summary>
public static class Mir2AtlasLibrary
{
    // 已加载图集缓存：manifestKey -> sheet
    private static readonly Dictionary<string, Mir2AtlasSheet> _sheets =
        new Dictionary<string, Mir2AtlasSheet>(StringComparer.OrdinalIgnoreCase);

    // —— 以下三个字段忠实对应 Egret RES.ResourceConfig 的内部结构 ——
    // fsData: 资源名 -> 资源项（含 url/type/subkeys/root）
    private static Dictionary<string, Mir2ResItem> _fsData;
    // alias: 子资源(帧)短名 -> "sheetName#frameName"（裸帧名 与 "sheetName.frameName" 都登记，对应 Egret 的 d[t]/d[name+"."+t]）
    private static Dictionary<string, string> _alias;
    // groups: 分组名 -> 成员资源名列表
    private static Dictionary<string, string[]> _groups;

    // 原版 RES.loadConfig("resource/default.res.json", "resource/")：资源根
    private const string ResourceRoot = "resource/";

    public static Mir2AtlasManifest Manifest => Mir2OriginResource.Manifest;

    /// <summary>Egret 资源项（对应 default.res.json 的 resources[] 元素）。</summary>
    public sealed class Mir2ResItem
    {
        public string Name;     // "advance-sheet_json"
        public string Url;      // "assets/gameui4/window-sheet/advance-sheet.json"（相对 resource/）
        public string Type;     // "sheet" / "image" / ...
        public string[] Subkeys;// ["adv_fail","adv_per","adv_suc"]
        public string Root = ResourceRoot;
        // 真实 manifest key = root + url（与 Egret 的 root+url 拼接一致）
        public string ManifestKey => Root + Url.TrimStart('/'); // -> "resource/assets/gameui4/..."
    }

    /// <summary>确保 manifest 已加载（CDN 模式下 UI/图集资源需要它做哈希路径解析；本地镜像可跳过）。</summary>
    public static async Task EnsureManifestAsync(CancellationToken ct = default)
    {
        if (Mir2OriginResource.Manifest == null)
            await Mir2OriginResource.LoadManifestAsync(ct);
    }

    /// <summary>
    /// 加载并解析原版 Egret 资源表 resource/default.res.json，构建 fsData / alias / groups（对应 Egret parseConfig）。
    /// 原版 JS 通过 RES.getRes("advance-sheet_json") 取图集、靠的就是这张表；我们复用它，代码里也能写短名。
    /// 失败则留空，GetSheetAsync 会回退为把入参当 manifest key 原样用。
    /// </summary>
    private static async Task EnsureResConfigAsync(CancellationToken ct)
    {
        if (_fsData != null) return;
        _fsData = new Dictionary<string, Mir2ResItem>(StringComparer.OrdinalIgnoreCase);
        _alias = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _groups = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        try
        {
            // default.res.json 本身也在 manifest 里（哈希布局），按 key 解析即可拉到。
            var bytes = await Mir2OriginResource.GetBytesAsync(
                Mir2OriginResource.ResolveManifestAssetPath("resource/default.res.json"), ct: ct);
            if (bytes == null || bytes.Length == 0) return;
            if (JsonNode.Parse(Encoding.UTF8.GetString(bytes)) is JsonObject root)
            {
                if (root["resources"] is JsonArray rs)
                {
                    foreach (var item in rs)
                    {
                        var name = item["name"]?.GetValue<string>();
                        var url = item["url"]?.GetValue<string>();
                        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(url)) continue;
                        var type = item["type"]?.GetValue<string>() ?? "";
                        var subRaw = item["subkeys"]?.GetValue<string>();
                        var subs = string.IsNullOrEmpty(subRaw)
                            ? Array.Empty<string>()
                            : subRaw.Split(',');
                        var it = new Mir2ResItem { Name = name, Url = url, Type = type, Subkeys = subs };
                        _fsData[name] = it;
                        // 子资源别名：getRes("adv_fail") 与 getRes("advance-sheet_json.adv_fail") 都能定位到该帧
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
                            _groups[gname] = keys == "" ? Array.Empty<string>() : keys.Split(',');
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

    /// <summary>
    /// 解析名字 -> (manifestKey, frameName)。
    /// 规则（与 Egret 一致）：① 命中 alias（裸帧名 / "sheet.frame"）-> 返回所属 sheet 的 key + 帧名；
    /// ② 命中 fsData（资源名）-> 返回该资源 key + null；③ 否则当作 manifest key 原样返回。
    /// </summary>
    private static (string key, string frame) ResolveName(string nameOrKey)
    {
        if (_alias != null && _alias.TryGetValue(nameOrKey, out var spec))
        {
            var h = spec.IndexOf('#');
            var sheetName = spec.Substring(0, h);
            var frame = spec.Substring(h + 1);
            if (_fsData.TryGetValue(sheetName, out var it))
                return (it.ManifestKey, frame);
        }
        if (_fsData != null && _fsData.TryGetValue(nameOrKey, out var item))
            return (item.ManifestKey, null);
        return (nameOrKey, null); // 已是 manifest key 或未知，原样返回
    }

    /// <summary>
    /// 按「短名 / 帧短名 / manifest key」加载一张图集（精灵表 JSON + 整页贴图），结果按 key 缓存。
    /// 入参可以是原版 Egret 资源名（"advance-sheet_json"）、某帧短名（"adv_fail"，自动归并到所属 sheet），
    /// 或完整 manifest key（"resource/assets/gameui4/window-sheet/advance-sheet.json"）。
    /// </summary>
    public static async Task<Mir2AtlasSheet> GetSheetAsync(string nameOrKey, GraphicsDevice device, CancellationToken ct = default)
    {
        await EnsureManifestAsync(ct);
        await EnsureResConfigAsync(ct);
        // 先把可能的帧短名归并到所属 sheet 的 manifestKey（帧短名下 key 仍是所属 sheet 的 key）
        var (key, _) = ResolveName(nameOrKey);
        if (_sheets.TryGetValue(key, out var existing)) return existing;

        // ① sheet JSON（帧信息）
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

        // ② 同目录整页贴图（png）
        var pngKey = SheetPngKey(key, sheet.File);
        var pngPath = Mir2OriginResource.ResolveManifestAssetPath(pngKey);
        var texture = await Mir2OriginResource.GetTextureAsync(pngPath, device, ct: ct);

        sheet.Build(texture);
        _sheets[key] = sheet;
        PrintTool.Log($"[Mir2Atlas] 已加载图集 {key}：{sheet.Frames.Count} 帧");
        return sheet;
    }

    /// <summary>
    /// 忠实于 RES.getRes：name 可传资源名(返回整张 sheet)，也可传帧短名(返回该帧 KSpriteInfo，找不到返回 null)。
    /// 例如 GetResSpriteAsync("adv_fail") 等价于原版 RES.getRes("adv_fail")。
    /// </summary>
    public static async Task<KSpriteInfo> GetResSpriteAsync(string nameOrFrame, GraphicsDevice device, CancellationToken ct = default)
    {
        await EnsureResConfigAsync(ct);
        await EnsureManifestAsync(ct);
        var (key, frame) = ResolveName(nameOrFrame);
        var sheet = await GetSheetAsync(key, device, ct);
        if (frame != null && sheet.Frames.TryGetValue(frame, out var fi)) return fi;
        return null; // name 是资源名而非帧短名时，没有单帧可返回
    }

    /// <summary>
    /// 加载「独立图片」资源（default.res.json 中 type=image 的资源，如加载背景 bg_loading2n）：
    /// 按 Egret 资源名解析成 manifest key 后取整张纹理（PNG/JPG），走 Mir2OriginResource 的纹理缓存。
    /// 与 GetSheetAsync 不同：图片资源没有 JSON 帧表，只取整张图。
    /// </summary>
    public static async Task<Texture2D> GetImageTextureAsync(string name, GraphicsDevice device, CancellationToken ct = default)
    {
        await EnsureManifestAsync(ct);
        await EnsureResConfigAsync(ct);
        var (key, _) = ResolveName(name); // 图片资源 -> 其 manifestKey（无帧）
        return await Mir2OriginResource.GetManifestAssetTextureAsync(key, device, ct);
    }

    /// <summary>
    /// 按「直接相对路径」加载一张图集（绕过 manifest/RES 表，用于 CDN 直链资源，例如
    /// qufu_resource1/assets/sheet/preload-sheet.json + .png）。jsonRelativePath / pngRelativePath
    /// 为相对 CDN 根的路径，可带 ?qufu_version=229 缓存参数。任一拉取为空返回 null（由上层回退纯色条）。
    /// </summary>
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

    /// <summary>从已加载图集取某帧的 KSpriteInfo；sheetOrFrameName 可为资源名或帧短名（支持短名；未加载返回 false）。</summary>
    public static bool TryGetFrame(string sheetOrFrameName, string frameName, out KSpriteInfo frame)
    {
        var (key, aliasFrame) = ResolveName(sheetOrFrameName);
        frame = null;
        if (!_sheets.TryGetValue(key, out var s)) return false;
        var fn = frameName ?? aliasFrame;
        return fn != null && s.Frames.TryGetValue(fn, out frame);
    }

    /// <summary>返回某分组的成员资源名列表（对应 Egret groups；未配置返回空数组）。可据此批量预加载一组 UI。</summary>
    public static string[] GetGroupResourceNames(string groupName)
        => (_groups != null && _groups.TryGetValue(groupName, out var names)) ? names : Array.Empty<string>();

    /// <summary>按 KSprite 句柄绘制一帧（KSprite 可经隐式转换由 KSpriteInfo 得到，等价于上面的绘制）。</summary>
    public static void DrawFrame(this SpriteBatch batch, KSprite sprite, Rectangle targetRect, Color color)
    {
        var r = sprite.Rectangle;
        batch.Draw(sprite.Texture, targetRect, r, color);
    }

    /// <summary>按「已加载图集 + 帧名」绘制一帧（帧不存在则静默跳过）。</summary>
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
