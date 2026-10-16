using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;
using System.Text.Json.Serialization;
using Mir2ReverseWeb.Models;
using SkiaSharp;

namespace Mir2ReverseWeb.Services;

/// <summary>
/// 原始传奇资源逆向服务。
/// 地图(.dat) 解析采用我们已逆向的格式：
///   大端 [count:u16][ [id:i32][len:i32] … ][ 数据… ]
///   每个地图数据块: 前 4 字节丢弃，其后为 4-bit/格 的单元格（两格压一字节，高 nibble 在前）
///   单元格 bit3=可走 / bit2=遮挡 / bit1=安全区
///   真实网格列数 = ceil(像素宽 / 48)，行数 = ceil(像素高 / 32)（来自 GameDefine.MAP_GRID_*）
/// </summary>
public class GameConfigService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(180) };
    static GameConfigService()
    {
        // CDN 会按 User-Agent 返回不同编码的配置：现代 UA 拿到的是「GBK 误当 UTF-8」的乱码
        // （如 "商店" 变成 "鍟嗗簵"），旧式 UA 才返回正确的 UTF-8。这里伪装成旧客户端以拿到干净中文。
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1; SV1; .NET CLR 2.0.50727)");
    }
    private readonly Dictionary<int, MapMeta> _mapConfig = new();
    private readonly Dictionary<int, string> _idToFile = new();
    private string? _loadedFile;
    private byte[]? _mapDatBytes;   // 从 URL 下载并解压后的完整 .dat（仅存内存，不落盘）
    private List<ManifestEntry>? _manifestEntries;   // 加载后的 manifest 条目（内存）
    private static readonly object _lock = new();

    // ---------------- 配置表（0config*.json 顶层每个键 = 一张配置表） ----------------
    private readonly Dictionary<string, JsonNode> _configTables = new();
    private bool _configLoaded;

    private static readonly JsonSerializerOptions _jsonIndented = new()
    {
        WriteIndented = true,
        // 保留中文等非 ASCII 字符为可读原文（而非 \uXXXX 转义）
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        // byte[] 默认会被序列化为 Base64 字符串；这里改成「数字数组」以贴合 byte[] 语义
        Converters = { new ByteArrayToArrayConverter() }
    };

    // ---------------- 透明解压 + 缓存 ----------------
    private readonly Dictionary<string, (long len, long tm, byte[] data)> _cache = new();

    private byte[] GetData(string path)
    {
        var fi = new FileInfo(path);
        var key = path;
        if (_cache.TryGetValue(key, out var c) && c.len == fi.Length && c.tm == fi.LastWriteTimeUtc.Ticks)
            return c.data;

        var raw = File.ReadAllBytes(path);
        var data = DecompressIfNeeded(raw);

        _cache[key] = (fi.Length, fi.LastWriteTimeUtc.Ticks, data);
        return data;
    }

    /// <summary>按需解压：gzip / zlib 魔数则解压，否则原样返回（纯内存，不落盘）</summary>
    private static byte[] DecompressIfNeeded(byte[] raw)
    {
        if (raw.Length > 2 && raw[0] == 0x1f && raw[1] == 0x8b) // gzip
        {
            using var ms = new MemoryStream(raw);
            using var gz = new GZipStream(ms, CompressionMode.Decompress);
            using var o = new MemoryStream(); gz.CopyTo(o); return o.ToArray();
        }
        if (raw.Length > 2 && raw[0] == 0x78 && (raw[1] == 0x01 || raw[1] == 0x9c || raw[1] == 0xda)) // zlib
        {
            using var ms = new MemoryStream(raw);
            using var zl = new ZLibStream(ms, CompressionMode.Decompress);
            using var o = new MemoryStream(); zl.CopyTo(o); return o.ToArray();
        }
        return raw;
    }

    // ---------------- 索引解析（委托给 MapDecoder） ----------------
    public MapIndexResult ParseMapDat(string path)
    {
        var res = new MapIndexResult();
        try
        {
            var data = GetData(path);
            _mapDatBytes = data;
            _loadedFile = path;
            res.SourceFile = path;
            res.Entries = MapDecoder.ParseIndex(data);
            res.Count = res.Entries.Count;
            res.TotalBytes = res.Entries.Sum(e => (long)e.Len);
            res.Ok = true;
        }
        catch (Exception ex) { res.Error = ex.Message; }
        return res;
    }

    // ---------------- 自动下载（仅内存，不落盘） + 配置合并 ----------------
    private async Task<byte[]?> DownloadBytesAsync(string url, List<string> msgs)
    {
        try
        {
            msgs.Add($"[下载] {url}");
            var raw = await _http.GetByteArrayAsync(url);
            var data = DecompressIfNeeded(raw);
            msgs.Add($"[已载] {url}（{(data.Length / 1024.0).ToString("F1")} KB，仅内存）");
            return data;
        }
        catch (Exception ex) { msgs.Add($"[下载失败] {url}：{ex.Message}"); return null; }
    }

    /// <summary>解析配置 JSON（中文地图名，可能以 UTF-8 或 GBK 编码，依 CDN 返回的编码而定），合并进内存字典</summary>
    private void MergeConfig(byte[] bytes, string label, List<string> msgs)
    {
        try
        {
            // CDN 会按 User-Agent 返回不同编码（本服务的 HttpClient 拿到的是 GBK，旧 UA 是 UTF-8）。
            // 先按严格 UTF-8 解析；若字节非合法 UTF-8 则回退 GBK，避免中文名变成乱码。
            string text;
            try { text = new UTF8Encoding(false, true).GetString(bytes); }
            catch (DecoderFallbackException) { text = Encoding.GetEncoding("gbk").GetString(bytes); }
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            // 结构：{ "map": { "154": { id, name, width, height, ... }, ... } }
            if (root.TryGetProperty("map", out var mapEl) && mapEl.ValueKind == JsonValueKind.Object)
                root = mapEl;
            int n = 0;
            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in root.EnumerateObject())
                {
                    if (prop.Value.ValueKind != JsonValueKind.Object) continue;
                    var v = prop.Value;
                    int id = (v.TryGetProperty("id", out var idp) && idp.TryGetInt32(out var idi))
                        ? idi
                        : (int.TryParse(prop.Name, out var pid) ? pid : 0);
                    if (id == 0) continue;
                    string name = FixChinese(StrOrNum(v, "name"));
                    string img = StrOrNum(v, "img");
                    if (string.IsNullOrEmpty(img) && v.TryGetProperty("id", out var idp2)) img = idp2.GetInt32().ToString();
                    int w = v.TryGetProperty("width", out var wp) ? wp.GetInt32() : 0;
                    int h = v.TryGetProperty("height", out var hp) ? hp.GetInt32() : 0;
                    _mapConfig[id] = new MapMeta { Name = name, Width = w, Height = h, Img = img };
                    n++;
                }
            }
            msgs.Add($"[配置] {label}：{n} 条地图定义");
        }
        catch (Exception ex) { msgs.Add($"[配置失败] {label}：{ex.Message}"); }
    }

    /// <summary>读取配置里「可能是字符串也可能是数字」的字段，统一返回字符串。
    /// 线上配置里 "img":103、"id":103 都是数字，直接 GetString() 会抛异常导致整份配置解析失败。</summary>
    private static string StrOrNum(JsonElement v, string prop)
    {
        if (!v.TryGetProperty(prop, out var e)) return "";
        if (e.ValueKind == JsonValueKind.String) return e.GetString() ?? "";
        if (e.ValueKind == JsonValueKind.Number) return e.GetRawText();
        return "";
    }

    // ---------------- 配置表：加载 / 枚举 / 提取（C# 类 + JSON 数据） ----------------

    /// <summary>
    /// 下载并合并配置分片（游戏实际加载 6 片：0config~5config，见 main10.25341.1.min.js 的
    /// onLoadCfgComplete2 循环）。每片「顶层每个键」视为一张配置表，全部合并进内存
    /// （靠后的分片同名键会覆盖靠前的，与游戏行为一致），供枚举与导出复用。
    /// baseUrl / version 可空，使用 Config.cs 默认值。某个分片下载失败会被跳过，不阻断其余分片。
    /// </summary>
    public async Task<ConfigTableListResult> LoadConfigTablesAsync(string? baseUrl = null, string? version = null)
    {
        var res = new ConfigTableListResult { Messages = new() };
        try
        {
            var urls = Mir2Config.ConfigShardUrls(baseUrl, version).ToList();
            _configTables.Clear();
            _configLoaded = false;
            res.Urls = urls;

            int loaded = 0;
            foreach (var url in urls)
            {
                var bytes = await DownloadBytesAsync(url, res.Messages);
                if (bytes == null) continue;
                // 与 MergeConfig 相同的编码兜底：先严格 UTF-8，失败时回退 GBK
                string text;
                try { text = new UTF8Encoding(false, true).GetString(bytes); }
                catch (DecoderFallbackException) { text = Encoding.GetEncoding("gbk").GetString(bytes); }
                var node = JsonNode.Parse(text);
                if (node is not JsonObject obj) { res.Messages.Add($"[跳过] {url}：根不是 JSON 对象"); continue; }
                foreach (var kv in obj)
                    if (kv.Value != null) _configTables[kv.Key] = kv.Value!;
                res.Messages.Add($"[配置] {url}：载入 {obj.Count} 个顶层键（累计 {_configTables.Count}）");
                loaded++;
            }
            if (loaded == 0) { res.Error = "所有配置分片下载失败，请检查「数据服务器地址 / 版本号」"; return res; }
            if (_configTables.Count == 0) { res.Error = "未从配置分片解析到任何配置表"; return res; }
            _configLoaded = true;
            res.Tables = ListTableInfos();
            res.Count = res.Tables.Count;
            res.Ok = true;
        }
        catch (Exception ex) { res.Error = ex.Message; }
        return res;
    }

    /// <summary>从已加载的内存配置枚举所有表及其字段（不重新下载）。</summary>
    public ConfigTableListResult ListConfigTables()
    {
        var res = new ConfigTableListResult();
        if (!_configLoaded) { res.Error = "请先加载配置表（点“加载配置表”按钮）"; return res; }
        res.Tables = ListTableInfos();
        res.Count = res.Tables.Count;
        res.Ok = true;
        return res;
    }

    private List<ConfigTableInfo> ListTableInfos()
    {
        var list = new List<ConfigTableInfo>();
        foreach (var kv in _configTables)
            list.Add(BuildTableInfo(kv.Key, kv.Value));
        list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    private ConfigTableInfo BuildTableInfo(string name, JsonNode node)
    {
        var info = new ConfigTableInfo { Name = name };
        var entries = NormalizeEntries(node);
        info.Count = entries.Count;
        info.Kind = node is JsonObject ? "object" : node is JsonArray ? "array" : node.GetValueKind().ToString();
        info.Fields = InferFields(entries);
        return info;
    }

    /// <summary>
    /// 把一张表规整成「条目集合」：
    /// object 表 → 其值的集合；array 表 → 数组元素。
    /// 若所有条目本身是数组（如某表 = { "1000": [ {…}, {…} ] }），则展平为内部对象，便于字段推断。
    /// </summary>
    private static List<JsonNode> NormalizeEntries(JsonNode node)
    {
        List<JsonNode> entries;
        if (node is JsonObject o) entries = o.Select(p => p.Value!).ToList();
        else if (node is JsonArray a) entries = a.Select(x => x!).ToList();
        else return new List<JsonNode>();

        if (entries.Count > 0 && entries.All(e => e is JsonArray))
        {
            var inner = entries.SelectMany(e => ((JsonArray)e).Select(x => x!))
                .Where(x => x is JsonObject).ToList();
            if (inner.Count > 0) entries = inner;
        }
        return entries;
    }

    /// <summary>从一组条目推断所有字段名与 C# 类型（最佳努力：标量尽量用强类型，对象/数组/混合一律 JsonElement 兜底）。</summary>
    private static List<ConfigTableField> InferFields(List<JsonNode> entries)
    {
        var stat = new Dictionary<string, FieldStat>();
        var optional = new HashSet<string>();
        foreach (var e in entries)
        {
            if (e is not JsonObject o) continue;
            var present = new HashSet<string>();
            foreach (var p in o)
            {
                present.Add(p.Key);
                if (!stat.TryGetValue(p.Key, out var s)) stat[p.Key] = s = new FieldStat();
                var vk = p.Value?.GetValueKind() ?? JsonValueKind.Null;
                s.Kinds.Add(vk);
                if (vk == JsonValueKind.Number && p.Value is JsonValue jv && jv.TryGetValue<double>(out var d))
                {
                    if (Math.Abs(d - Math.Floor(d)) > 1e-9 || double.IsInfinity(d) || double.IsNaN(d))
                        s.NumIntegral = false;
                    if (d < int.MinValue || d > int.MaxValue) s.NumFitsInt32 = false;
                }
            }
            foreach (var k in stat.Keys)
                if (!present.Contains(k)) optional.Add(k);
        }
        var fields = new List<ConfigTableField>();
        foreach (var kv in stat)
            fields.Add(new ConfigTableField
            {
                Name = kv.Key,
                CsType = InferCsType(kv.Value),
                Optional = optional.Contains(kv.Key)
            });
        fields.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return fields;
    }

    private static string InferCsType(FieldStat s)
    {
        var k = s.Kinds;
        if (k.Count == 1)
        {
            var only = k.First();
            if (only == JsonValueKind.String) return "string";
            if (only == JsonValueKind.True || only == JsonValueKind.False) return "bool";
            if (only == JsonValueKind.Number) return s.NumIntegral ? (s.NumFitsInt32 ? "int" : "long") : "double";
            return "JsonElement";
        }
        if (k.All(x => x == JsonValueKind.True || x == JsonValueKind.False)) return "bool";
        if (k.All(x => x == JsonValueKind.Number))
            return s.NumIntegral ? (s.NumFitsInt32 ? "int" : "long") : "double";
        return "JsonElement";
    }

    /// <summary>
    /// 提取配置表：为每张表写出「C# 类（描述字段 schema）」与「JSON 数据（保持原始 object/array 形态）」。
    /// 输出目录默认系统下载目录下的 Mir2Config 子目录。
    /// </summary>
    public ConfigExtractResult ExtractConfigTables(ConfigExtractRequest req)
    {
        var res = new ConfigExtractResult { Messages = new() };
        try
        {
            if (!_configLoaded) { res.Error = "请先加载配置表（点“加载配置表”按钮）"; return res; }
            var baseDir = string.IsNullOrWhiteSpace(req.OutDir)
                ? Path.Combine(OsDownloadDir(), "Mir2Config")
                : req.OutDir!;
            Directory.CreateDirectory(baseDir);

            var allNames = _configTables.Keys.ToList();
            List<string> names;
            if (req.Tables != null && req.Tables.Count > 0 && !(req.Tables.Count == 1 && req.Tables[0] == "*"))
                names = req.Tables.Where(n => _configTables.ContainsKey(n)).ToList();
            else
                names = allNames;

            int n = 0;
            foreach (var name in names)
            {
                var node = _configTables[name];
                var info = BuildTableInfo(name, node);
                int entryCount = info.Count;

                string? classFile = null;
                if (req.WriteClass)
                {
                    var cs = GenerateConfigClass(name, info);
                    if (cs != null)
                    {
                        classFile = Path.Combine(baseDir, Sanitize(name) + "Config.cs");
                        File.WriteAllText(classFile, cs);
                    }
                }
                string? dataFile = null;
                if (req.WriteJson)
                {
                    dataFile = Path.Combine(baseDir, Sanitize(name) + ".json");
                    File.WriteAllText(dataFile, JsonSerializer.Serialize(node, _jsonIndented));
                }
                res.Files.Add(new ConfigTableFile { Table = name, ClassFile = classFile, DataFile = dataFile, Entries = entryCount });
                if (++n % 25 == 0) res.Messages.Add($"[进度] {n}/{names.Count}");
            }
            res.TableCount = n;
            res.Messages.Add($"[完成] 共 {n} 张配置表，输出目录：{baseDir}");
            res.Ok = true;
        }
        catch (Exception ex) { res.Error = ex.Message; }
        return res;
    }

    /// <summary>把表名转 PascalCase 作为 C# 类名（数字/非法字符处理为合法标识符）。</summary>
    private static string PascalName(string key)
    {
        var sb = new System.Text.StringBuilder();
        bool up = true;
        foreach (var c in key)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                sb.Append(up ? char.ToUpperInvariant(c) : c);
                up = false;
            }
            else up = true; // 分隔符后下一个字母大写
        }
        var s = sb.ToString();
        if (s.Length == 0) s = "Table";
        if (!char.IsLetter(s[0]) && s[0] != '_') s = "_" + s;
        return s;
    }

    /// <summary>把字段名规整为合法 C# 标识符（保留原始键名通过 [JsonPropertyName] 映射）。</summary>
    private static string CsFieldName(string key)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in key)
            sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        var s = sb.ToString();
        if (s.Length == 0) s = "Field";
        if (char.IsDigit(s[0])) s = "_" + s;
        // 规避 C# 关键字
        return s == "object" || s == "string" || s == "event" || s == "base" || s == "class" || s == "namespace"
            ? "@" + s : s;
    }

    /// <summary>由表元信息生成一张配置表的 C# 类源文件。</summary>
    private static string? GenerateConfigClass(string name, ConfigTableInfo info)
    {
        if (info.Fields.Count == 0) return null;
        var cls = PascalName(name) + "Config";
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("// <auto-generated> 原始传奇 配置表 \"" + name + "\" —— 由 Mir2ReverseWeb 提取");
        sb.AppendLine("// 形态: " + info.Kind + "，条目数: " + info.Count + "，字段数: " + info.Fields.Count);
        sb.AppendLine("// 标量字段做了最佳努力类型推断；对象/数组/混合字段用 JsonElement 兜底，反序列化时无需额外配置。");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine();
        sb.AppendLine("namespace Mir2Config;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>配置表 \"" + name + "\" 的单条记录</summary>");
        sb.AppendLine("public class " + cls);
        sb.AppendLine("{");
        foreach (var f in info.Fields)
        {
            var csType = f.CsType + (f.Optional && f.CsType != "string" && f.CsType != "JsonElement" ? "?" : "");
            var def = f.CsType == "string" ? " = \"\"" : f.Optional ? "" : (f.CsType == "JsonElement" ? "" : " = default");
            sb.AppendLine("    [JsonPropertyName(\"" + f.Name + "\")]");
            sb.AppendLine("    public " + csType + " " + CsFieldName(f.Name) + " { get; set; }" + def + "");
            sb.AppendLine();
        }
        sb.AppendLine("}");
        return sb.ToString();
    }

    private sealed class FieldStat
    {
        public HashSet<JsonValueKind> Kinds { get; } = new();
        public bool NumIntegral { get; set; } = true;
        public bool NumFitsInt32 { get; set; } = true;
    }

    /// <summary>
    /// 还原被“双重编码”成乱码的中文（如 "商店" 在 CDN 上偶发会变成 "鍟嗗簵"：
    /// 即原始 GBK 字节被误当 UTF-8 再编码）。恢复方式：把当前字符串按 UTF-8 取回字节，
    /// 再用 GBK 解码。该操作对已经是正确中文的字符串是幂等的（"商店"→"商店"），可放心套用。
    /// </summary>
    private static string FixChinese(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        try { return Encoding.GetEncoding("gbk").GetString(Encoding.UTF8.GetBytes(s)); }
        catch { return s; }
    }

    /// <summary>
    /// 自动下载地图包 + 配置（仅放入内存，不写本地磁盘），解析索引并给每张地图补上名字与真实像素宽高。
    /// 地图包 ~1.2MB + 4 个配置分片，下载后直接解压进内存缓存，供预览/导出复用。
    /// </summary>
    public async Task<MapIndexResult> LoadMapDataAsync(string? datUrl = null)
    {
        var res = new MapIndexResult { Messages = new() };
        try
        {
            var datUrls = string.IsNullOrWhiteSpace(datUrl) ? Mir2Config.MapDataUrls : new[] { datUrl };
            foreach (var url in datUrls)
            {
                var bytes = await DownloadBytesAsync(url, res.Messages);
                if (bytes == null) continue;
                var entries = MapDecoder.ParseIndex(bytes);
                res.Entries.AddRange(entries);
                int n = entries.Count;
                res.Messages.Add($"[地图包] {url}：{n} 张");
                _mapDatBytes = bytes;
                _loadedFile = url;
            }
            foreach (var url in Mir2Config.MapConfigUrls)
            {
                var bytes = await DownloadBytesAsync(url, res.Messages);
                if (bytes == null) continue;
                MergeConfig(bytes, url, res.Messages);
            }
            lock (_lock)
            {
                foreach (var e in res.Entries)
                {
                    if (_mapConfig.TryGetValue(e.Id, out var m)) { e.Name = m.Name; e.Width = m.Width; e.Height = m.Height; e.Img = m.Img; }
                }
            }
            res.ConfigCount = _mapConfig.Count;
            res.SourceFile = _loadedFile;
            res.Count = res.Entries.Count;
            res.TotalBytes = res.Entries.Sum(e => (long)e.Len);
            res.MapConfigUrls = Mir2Config.MapConfigUrls.ToList();
            res.MapJsUrls = Mir2Config.MapJsUrls.ToList();
            res.Ok = true;
        }
        catch (Exception ex) { res.Error = ex.Message; }
        return res;
    }

    /// <summary>操作系统默认下载目录（UserProfile/Downloads）</summary>
    internal static string OsDownloadDir()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    // ---------------- 资源 manifest 加载 + 下载（仅内存解析，按需落盘） ----------------
    /// <summary>由 manifest 条目推导实际 CDN 文件 URL：{CdnBase}/resource/{v前2位}/{v}_{s}.{ext}</summary>
    private static string ManifestToUrl(string key, string hash, long size)
    {
        var v2 = hash.Length >= 2 ? hash.Substring(0, 2) : hash;
        var cleanExt = Path.GetExtension(key).TrimStart('.');
        return $"{Mir2Config.CdnBase}/resource/{v2}/{hash}_{size}.{cleanExt}";
    }

    public async Task<ManifestResult> LoadManifestAsync()
    {
        var res = new ManifestResult { Messages = new() };
        try
        {
            res.ManifestUrl = Mir2Config.ManifestUrl;
            res.Messages.Add($"[下载] {Mir2Config.ManifestUrl}");
            var raw = await _http.GetByteArrayAsync(Mir2Config.ManifestUrl);
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            var list = new List<ManifestEntry>();
            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in root.EnumerateObject())
                {
                    if (prop.Value.ValueKind != JsonValueKind.Object) continue;
                    var v = prop.Value;
                    var hash = v.TryGetProperty("v", out var vp) ? (vp.GetString() ?? "") : "";
                    var size = v.TryGetProperty("s", out var sp) ? sp.GetInt64() : 0;
                    var url = ManifestToUrl(prop.Name, hash, size);
                    list.Add(new ManifestEntry { Key = prop.Name, Hash = hash, Size = size, Ext = Path.GetExtension(prop.Name), Url = url });
                }
            }
            _manifestEntries = list;
            res.Entries = list;
            res.Count = list.Count;
            res.Messages.Add($"[清单] 共 {list.Count} 条资源");
            res.Ok = true;
        }
        catch (Exception ex) { res.Error = ex.Message; }
        return res;
    }

    public async Task<AtlasDownloadResult> DownloadManifestAsset(string key, string? outDir = null)
    {
        var res = new AtlasDownloadResult { Key = key };
        try
        {
            var entry = _manifestEntries?.FirstOrDefault(e => e.Key == key);
            if (entry == null) { res.Error = "清单未加载或无此条目"; return res; }
            res.Url = entry.Url;
            var sub = entry.Hash.Length >= 2 ? entry.Hash.Substring(0, 2) : entry.Hash;
            var baseDir = string.IsNullOrWhiteSpace(outDir) ? OsDownloadDir() : outDir!;
            var dir = Path.Combine(baseDir, sub);
            Directory.CreateDirectory(dir);
            var fileName = $"{entry.Hash}_{entry.Size}.{entry.Ext.TrimStart('.')}";
            var file = Path.Combine(dir, fileName);
            if (File.Exists(file) && new FileInfo(file).Length == entry.Size)
            {
                res.Skipped = true; res.LocalPath = file; res.Bytes = entry.Size; res.Ok = true; return res;
            }
            var bytes = await _http.GetByteArrayAsync(entry.Url);
            await File.WriteAllBytesAsync(file, bytes);
            res.LocalPath = file; res.Bytes = bytes.Length; res.Ok = true;
        }
        catch (Exception ex) { res.Error = ex.Message; }
        return res;
    }

    public async Task<AtlasDownloadAllResult> DownloadManifestAll(string? outDir = null)
    {
        var res = new AtlasDownloadAllResult { Messages = new() };
        try
        {
            var list = _manifestEntries;
            if (list == null || list.Count == 0) { res.Error = "请先加载清单"; return res; }
            res.Total = list.Count;
            int n = 0;
            foreach (var e in list)
            {
                n++;
                var r = await DownloadManifestAsset(e.Key, outDir);
                if (r.Skipped) res.Skipped++;
                else if (r.Ok) { res.Downloaded++; res.TotalBytes += r.Bytes; }
                else res.Failed++;
                if (n % 50 == 0) res.Messages.Add($"[进度] {n}/{list.Count}（下载 {res.Downloaded} 跳过 {res.Skipped} 失败 {res.Failed}）");
            }
            res.Messages.Add($"[完成] 总计 {res.Total}：下载 {res.Downloaded}，跳过 {res.Skipped}，失败 {res.Failed}，共 {(res.TotalBytes / 1048576.0).ToString("F1")} MB");
            res.Ok = true;
        }
        catch (Exception ex) { res.Error = ex.Message; }
        return res;
    }

    private string? ResolveFile(int id)
    {
        if (_idToFile.TryGetValue(id, out var f)) return f;
        return _loadedFile;
    }

    public MapDecodeResult DecodeMap(MapDecodeRequest req)
    {
        var res = new MapDecodeResult { Id = req.Id };
        try
        {
            // URL 加载的数据已解压进内存（_mapDatBytes）；仅当内存无数据时才回退到本地文件
            byte[]? data = _mapDatBytes;
            if (data == null)
            {
                string? file = (!string.IsNullOrWhiteSpace(req.Path) && File.Exists(req.Path)) ? req.Path : ResolveFile(req.Id);
                if (file == null || !File.Exists(file)) { res.Error = "未加载地图数据，请先在「提取地图资源」页加载"; return res; }
                data = GetData(file);
            }
            var block = MapDecoder.ExtractMapBlock(data, req.Id, out int rawLen);
            if (block == null) { res.Error = $"未找到地图 {req.Id}（块长 {rawLen}）"; return res; }

            int pw = req.Width, ph = req.Height;
            string name = "";
            if (_mapConfig.TryGetValue(req.Id, out var m)) { name = m.Name; if (pw <= 0) pw = m.Width; if (ph <= 0) ph = m.Height; }

            // 委托给 MapDecoder 解析网格与逐格标志（纯逻辑，可合并到 MonoGame.Client）
            var map = MapDecoder.Decode(req.Id, block, pw, ph, name, Mir2Config.GridWidth, Mir2Config.GridHeight);

            // 导出模式：渲染真实网格比例的完整地图图片（最长边 ≤ 4096）；否则渲染内联预览（≤ 560）
            bool export = !string.IsNullOrWhiteSpace(req.Destination) && Directory.Exists(req.Destination);
            int side = export ? 4096 : 560;
            using var bmp = MapRenderer.RenderMask(map, side);

            if (export)
            {
                if (req.WritePng)
                {
                    var pngBytes = MapRenderer.EncodePng(bmp);
                    var pngFile = Path.Combine(req.Destination!, $"map_{req.Id}.png");
                    File.WriteAllBytes(pngFile, pngBytes);
                    res.OutFile = pngFile;

                    // 内联预览使用缩小图，避免超大 base64
                    using var prev = MapRenderer.ScaleDown(bmp, 560);
                    res.PreviewPngBase64 = "data:image/png;base64," + Convert.ToBase64String(MapRenderer.EncodePng(prev));
                }

                // 同时导出结构化地图数据（JSON，含每格可走/遮挡/安全区标志）
                var jsonFile = Path.Combine(req.Destination!, $"map_{req.Id}.json");
                File.WriteAllText(jsonFile, JsonSerializer.Serialize(map, _jsonIndented));
                res.OutDataFile = jsonFile;
            }
            else
            {
                res.PreviewPngBase64 = "data:image/png;base64," + Convert.ToBase64String(MapRenderer.EncodePng(bmp));
            }

            res.Cells = map.Cols * map.Rows;
            res.RawCells = map.RawCells;
            res.Cols = map.Cols;
            res.Rows = map.Rows;
            res.ConfigCells = (pw > 0 && ph > 0)
                ? (int)Math.Ceiling(pw / (double)Mir2Config.GridWidth) * (int)Math.Ceiling(ph / (double)Mir2Config.GridHeight)
                : 0;
            res.Walkable = map.Walkable;
            res.Cover = map.Cover;
            res.Safe = map.Safe;
            res.Warn = map.Warn;
            res.Ok = true;
        }
        catch (Exception ex) { res.Error = ex.Message; }
        return res;
    }

    public MapExportResult ExportMaps(MapExportRequest req)
    {
        var res = new MapExportResult();
        try
        {
            if (req.Ids.Count == 0) { res.Error = "未选择地图"; return res; }
            Directory.CreateDirectory(req.Destination);
            foreach (var id in req.Ids)
            {
                // 每张地图一个以「地图名_id」命名的文件夹
                string name = _mapConfig.TryGetValue(id, out var mc) ? mc.Name : "";
                string dirName = string.IsNullOrWhiteSpace(name) ? $"map_{id}" : $"{Sanitize(name)}_{id}";
                string dir = Path.Combine(req.Destination, dirName);
                Directory.CreateDirectory(dir);

                var dm = new MapDecodeRequest
                {
                    Path = req.Path,
                    Id = id,
                    Destination = dir,
                    WritePng = req.WritePng
                };
                if (_mapConfig.TryGetValue(id, out var m)) { dm.Width = m.Width; dm.Height = m.Height; }
                var r = DecodeMap(dm);
                if (r.Ok && r.OutFile != null) res.Files.Add(r.OutFile);
                if (r.Ok && r.OutDataFile != null) res.DataFiles.Add(r.OutDataFile);
            }
            res.Exported = res.DataFiles.Count;
            res.Ok = true;
        }
        catch (Exception ex) { res.Error = ex.Message; }
        return res;
    }

    /// <summary>清理文件名中的非法字符，避免建文件夹失败</summary>
    private static string Sanitize(string s)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var c in s)
            sb.Append(invalid.Contains(c) ? '_' : c);
        var outStr = sb.ToString().Trim().TrimEnd('.', ' ');
        return string.IsNullOrWhiteSpace(outStr) ? "map" : outStr;
    }

    // ---------------- 图集 WIL/WIX ----------------
    public ScanResult Scan(string root, List<string> exts)
    {
        var res = new ScanResult();
        try
        {
            if (!Directory.Exists(root)) { res.Error = "目录不存在"; return res; }
            var lower = exts.Select(e => e.Trim().ToLowerInvariant()).ToList();
            var files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(f =>
                {
                    if (lower.Count == 0) return true;
                    var e = Path.GetExtension(f).ToLowerInvariant();
                    return lower.Contains(e) || lower.Contains(e.TrimStart('.'));
                })
                .Select(f => new FsEntry
                {
                    Name = Path.GetFileName(f),
                    Path = f,
                    IsDir = false,
                    Size = new FileInfo(f).Length
                })
                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            res.Files = files;
            res.Ok = true;
        }
        catch (Exception ex) { res.Error = ex.Message; }
        return res;
    }

    /// <summary>扫描图集库文件（委托 Mir2AtlasDecoder 模块）。</summary>
    public AtlasScanResult ScanAtlases(string root)
    {
        var res = new AtlasScanResult();
        try
        {
            res.Files = Mir2AtlasDecoder.Scan(root);
            res.Ok = true;
        }
        catch (Exception ex) { res.Error = ex.Message; }
        return res;
    }

    /// <summary>解析 WIL/WIX 图集结构（委托 Mir2AtlasDecoder 模块）。</summary>
    public AtlasInfo ReadAtlas(string wilPath)
        => Mir2AtlasDecoder.ReadAtlas(wilPath);

    // ---------------- 文件信息 ----------------
    public FileInfoResult GetFileInfo(string path)
    {
        var res = new FileInfoResult { Path = path };
        try
        {
            if (!File.Exists(path)) { res.Exists = false; res.Note = "文件不存在"; return res; }
            var fi = new FileInfo(path);
            res.Exists = true;
            res.Size = fi.Length;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            res.Kind = ext.TrimStart('.');
            if (ext is ".txt" or ".json" or ".csv" or ".ini" or ".xml" or ".html" or ".js" or ".ts")
            {
                var text = File.ReadAllText(path);
                res.Lines = text.Split('\n').Length;
                res.Note = "文本文件，已统计行数";
            }
            else if (ext is ".mirdb")
            {
                res.Note = "MirDB 二进制数据库（怪物/物品/技能/NPC 等定义），像素/结构解析待接入";
            }
            else
            {
                res.Note = "二进制资源，结构解析待接入";
            }
        }
        catch (Exception ex) { res.Note = "读取失败: " + ex.Message; }
        return res;
    }

    // ---------------- 文件系统 ----------------
    public List<FsEntry> ListDir(string? path, bool files, string? filter)
    {
        var root = string.IsNullOrWhiteSpace(path)
            ? (OperatingSystem.IsWindows() ? "C:\\" : "/")
            : path!;
        if (!Directory.Exists(root))
            return new List<FsEntry>();

        var exts = (filter ?? "")
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim().ToLowerInvariant())
            .Where(x => x.Length > 0)
            .ToList();

        var entries = new List<FsEntry>();
        try { foreach (var d in Directory.EnumerateDirectories(root)) entries.Add(new FsEntry { Name = Path.GetFileName(d), Path = d, IsDir = true }); }
        catch { }
        if (files)
        {
            try
            {
                foreach (var f in Directory.EnumerateFiles(root))
                {
                    var ext = Path.GetExtension(f).ToLowerInvariant();
                    if (exts.Count != 0 && !exts.Contains(ext)) continue;
                    var fi = new FileInfo(f);
                    entries.Add(new FsEntry { Name = Path.GetFileName(f), Path = f, IsDir = false, Size = fi.Length });
                }
            }
            catch { }
        }
        return entries.OrderBy(e => !e.IsDir).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }
}

/// <summary>内存中的地图元数据（来自 config 合并）</summary>
internal class MapMeta
{
    public string Name { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public string Img { get; set; } = "";
}

/// <summary>
/// 把 byte[] 序列化为 JSON 数字数组（[0,1,3,…]），而非默认的 Base64 字符串。
/// 反序列化时也支持从数字数组还原回 byte[]，方便人工校对与合并进客户端。
/// </summary>
public class ByteArrayToArrayConverter : JsonConverter<byte[]>
{
    public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var b in value) writer.WriteNumberValue(b);
        writer.WriteEndArray();
    }

    public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("byte[] 期望以 JSON 数组开头");
        var list = new List<byte>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray) return list.ToArray();
            if (reader.TokenType == JsonTokenType.Number) list.Add((byte)reader.GetByte());
        }
        throw new JsonException("byte[] 数组未正常结束");
    }
}
