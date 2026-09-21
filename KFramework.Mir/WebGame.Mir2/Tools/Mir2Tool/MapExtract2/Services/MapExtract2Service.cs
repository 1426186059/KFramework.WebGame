using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using MapExtract2.Models;
using Mir.Map;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace MapExtract2.Services
{
    /// <summary>
    /// 复刻 MapExtract 的 Web 页面契约（路径配置 / 探测 / 扫描 / 加载 MirDB / 目录浏览），
    /// 但「提取」操作改为：把每张地图用到的素材从 .Lib 蒸馏成「蒸馏版 Lib」，
    /// 再交由 kfc (KFramework.Content.Cli) 按地图打包成 AssetBundle（version.manifest 清单）。
    /// </summary>
    public sealed class MapExtract2Service
    {
        private const int MaxLogLines = 4000;
        private const string ManifestFile = "version.manifest";

        private readonly ILogger<MapExtract2Service> _logger;
        private readonly string _configPath;
        private readonly object _work = new object();

        private MapExtract2Config _config;

        // MirDB 解析缓存：MapName(string) → MapEntry
        private Dictionary<string, MirDBParser.MapEntry>? _mirDb;
        private int _mirDbRawCount;

        // 扫描缓存
        private readonly List<string> _scannedPaths = new();
        private readonly Dictionary<string, string> _mapFormats = new();
        private readonly Dictionary<string, int> _mapTypeIds = new();

        // 最近一次提取的每图产物统计（供 /api/result 展示）
        private readonly Dictionary<string, MapResultDto> _lastResults = new(StringComparer.OrdinalIgnoreCase);

        public MapExtract2Service(ILogger<MapExtract2Service> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            _configPath = Path.Combine(env.ContentRootPath, "mapextract2.config.json");
            _config = LoadConfig();

            if (!string.IsNullOrEmpty(_config.MirDBPath) && File.Exists(_config.MirDBPath))
            {
                try { LoadMirDB(); }
                catch (Exception ex) { _logger.LogWarning(ex, "自动加载 MirDB 失败"); }
            }
        }

        public bool MirDBLoaded => _mirDb != null;
        public int MirDBCount => _mirDb?.Count ?? 0;

        // ==================== 配置 ====================

        public MapExtract2Config GetConfig() => _config;

        public MapExtract2Config UpdateConfig(MapExtract2Config cfg)
        {
            lock (_work)
            {
                bool mirDbPathChanged = !string.Equals(_config.MirDBPath, cfg.MirDBPath, StringComparison.OrdinalIgnoreCase);
                _config = cfg;
                SaveConfig();

                if (mirDbPathChanged)
                {
                    _mirDb = null;
                    _mirDbRawCount = 0;
                    if (!string.IsNullOrEmpty(_config.MirDBPath) && File.Exists(_config.MirDBPath))
                    {
                        try { LoadMirDB(); }
                        catch (Exception ex) { _logger.LogWarning(ex, "更新配置后加载 MirDB 失败"); }
                    }
                }
            }
            return _config;
        }

        private MapExtract2Config LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    var cfg = JsonSerializer.Deserialize<MapExtract2Config>(json);
                    if (cfg != null) return cfg;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "读取配置失败，使用默认值");
            }
            return new MapExtract2Config();
        }

        private void SaveConfig()
        {
            try
            {
                var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "保存配置失败");
            }
        }

        // ==================== 路径自检 ====================

        public PathCheckDto CheckPaths()
        {
            var c = _config;
            string src = c.SourcePath.TrimEnd('\\', '/');
            return new PathCheckDto
            {
                MapDirExists = Directory.Exists(c.MapDir),
                SourceDirExists = Directory.Exists(c.SourcePath),
                TilesExists = File.Exists(src + "/WemadeMir2/Tiles.Lib"),
                SmTilesExists = File.Exists(src + "/WemadeMir2/Smtiles.Lib"),
                ObjectsExists = File.Exists(src + "/WemadeMir2/Objects.Lib"),
                MinimapExists = Directory.Exists(DeriveMinimapDir(c.MinimapLibPath)),
                MinimapLibExists = File.Exists(c.MinimapLibPath),
                MirDBExists = File.Exists(c.MirDBPath),
            };
        }

        // ==================== MirDB ====================

        public MirDbResultDto LoadMirDB()
        {
            lock (_work)
            {
                _mirDb = null;
                _mirDbRawCount = 0;

                string path = _config.MirDBPath;
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    var msg = $"MirDB 文件不存在: {path}";
                    _logger.LogWarning(msg);
                    return new MirDbResultDto { Ok = false, Message = msg, Count = 0 };
                }

                List<MirDBParser.MapEntry>? list;
                try { list = MirDBParser.Parse(path); }
                catch (Exception ex)
                {
                    return new MirDbResultDto { Ok = false, Message = "MirDB 解析失败: " + ex.Message, Count = 0 };
                }

                if (list == null || list.Count == 0)
                    return new MirDbResultDto { Ok = false, Message = "MirDB 解析失败或无地图数据。", Count = 0 };

                _mirDb = new Dictionary<string, MirDBParser.MapEntry>(StringComparer.OrdinalIgnoreCase);
                foreach (var e in list)
                {
                    string key = Path.GetFileNameWithoutExtension(e.FileName ?? "");
                    if (!string.IsNullOrEmpty(key) && !_mirDb.ContainsKey(key))
                        _mirDb[key] = e;
                }
                _mirDbRawCount = list.Count;

                return new MirDbResultDto
                {
                    Ok = true,
                    Message = $"MirDB 加载成功！共 {_mirDb.Count} 张地图",
                    Count = _mirDb.Count
                };
            }
        }

        // ==================== 扫描地图 ====================

        public ScanResultDto Scan()
        {
            lock (_work)
            {
                _scannedPaths.Clear();
                _mapFormats.Clear();
                _mapTypeIds.Clear();

                var result = new ScanResultDto { MirDBLoaded = MirDBLoaded, MirDBCount = MirDBCount };

                if (!Directory.Exists(_config.MapDir))
                {
                    result.Message = $"地图目录不存在: {_config.MapDir}";
                    return result;
                }

                var option = _config.RecursiveScan ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var lowerMaps = Directory.GetFiles(_config.MapDir, "*.map", option);
                var upperMaps = Directory.GetFiles(_config.MapDir, "*.MAP", option);
                var all = lowerMaps.Union(upperMaps, StringComparer.OrdinalIgnoreCase)
                                     .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                                     .ToList();

                for (int i = 0; i < all.Count; i++)
                {
                    string file = all[i];
                    string fmt = "?";
                    int typeId = -1;
                    try
                    {
                        byte[] bytes = File.ReadAllBytes(file);
                        if (bytes.Length > 20)
                        {
                            var mr = new MapReader(bytes);
                            fmt = mr.MapFormatName;
                            typeId = mr.MapTypeId;
                        }
                        else
                        {
                            fmt = "无数据";
                        }
                    }
                    catch { fmt = "读取失败"; }

                    _scannedPaths.Add(file);
                    _mapFormats[file] = fmt;
                    _mapTypeIds[file] = typeId;
                }

                for (int i = 0; i < _scannedPaths.Count; i++)
                {
                    string file = _scannedPaths[i];
                    string mapName = Path.GetFileNameWithoutExtension(file);
                    string fmt = _mapFormats[file];
                    int typeId = _mapTypeIds[file];

                    var dto = new MapItemDto
                    {
                        Index = i,
                        Name = mapName,
                        Path = file,
                        Format = fmt,
                        TypeId = typeId,
                        TypeDescription = GetTypeDescription(fmt, typeId)
                    };

                    if (_mirDb != null && _mirDb.TryGetValue(mapName, out var e))
                    {
                        dto.InMirDB = true;
                        dto.MiniMap = e.MiniMap;
                        dto.BigMap = e.BigMap;
                        dto.Title = e.Title ?? "";
                    }
                    result.Maps.Add(dto);
                }

                result.Ok = true;
                result.Total = _scannedPaths.Count;
                var summary = _mapFormats.Values
                    .GroupBy(f => f)
                    .Select(g => $"{g.Key}:{g.Count()}")
                    .ToList();
                result.FormatSummary = summary;
                result.Message = $"扫描完成: 找到 {result.Total} 个 .map 文件\n格式分布: {string.Join(", ", summary)}";
                return result;
            }
        }

        // ==================== 提取地图（蒸馏 → AssetBundle）====================

        public ExtractResultDto Extract(List<string> mapNames)
        {
            lock (_work)
            {
                var result = new ExtractResultDto
                {
                    DestinationPath = ""
                };
                var log = result.Log;

                if (mapNames == null || mapNames.Count == 0)
                {
                    result.Message = "请先勾选要提取的地图。";
                    return result;
                }

                // 调试上限
                if (_config.MaxMaps > 0 && mapNames.Count > _config.MaxMaps)
                    mapNames = mapNames.Take(_config.MaxMaps).ToList();

                int success = 0, fail = 0;
                var failed = new List<string>();
                var perMap = new Dictionary<string, (int libs, int imgs, string skipped)>(StringComparer.OrdinalIgnoreCase);

                string clientRoot = _config.ClientRootPath.TrimEnd('\\', '/');

                // 热更资源根目录：直接落到 Mir2Res/Map/ 下（Mir2Res 作为 http 根时即为 /Map/... 的 URL）。
                // 工具侧仅把“图片 Lib”蒸馏出来，不打包、不提取 .map。
                bool isTempRoot = string.IsNullOrWhiteSpace(_config.PackRootPath);
                string resRoot = isTempRoot
                    ? Path.Combine(Path.GetTempPath(), "MapExtract2_res_" + Guid.NewGuid().ToString("N"))
                    : _config.PackRootPath.TrimEnd('\\', '/');
                Directory.CreateDirectory(resRoot);
                result.DestinationPath = resRoot;

                try
                {
                    for (int i = 0; i < mapNames.Count; i++)
                    {
                        string mapName = mapNames[i];
                        string? mapFile = ResolveMapFile(mapName);
                        if (mapFile == null)
                        {
                            fail++;
                            failed.Add(mapName + " (文件不存在)");
                            continue;
                        }

                        AppendLog(log, $"[{i + 1}/{mapNames.Count}] 开始提取 {mapName}");
                        try
                        {
                            if (DistillOneMapToDisk(clientRoot, mapFile, mapName, resRoot, log, out var stat))
                            {
                                perMap[mapName] = (stat.LibCount, stat.ImageCount, stat.Skipped);
                                success++;
                            }
                            else
                            {
                                fail++;
                                failed.Add(mapName + " (蒸馏失败)");
                            }
                        }
                        catch (Exception ex)
                        {
                            fail++;
                            failed.Add(mapName + $" ({ex.Message})");
                            AppendLog(log, $"[ERROR] {mapName}: {ex}");
                        }
                    }

                    if (perMap.Count == 0)
                    {
                        result.Ok = fail == 0;
                        result.Success = success;
                        result.Fail = fail;
                        result.Failed = failed;
                        result.Message = "没有可提取的地图。";
                        return result;
                    }

                    // 报告
                    var report = new StringBuilder();
                    report.AppendLine("地图, 用到的库数, 用到的图数, 缺失/跳过的库");
                    foreach (var m in perMap)
                        report.AppendLine($"{m.Key}, {m.Value.libs}, {m.Value.imgs}, {m.Value.skipped}");
                    File.WriteAllText(Path.Combine(resRoot, "report.txt"), report.ToString());
                    AppendLog(log, $"提取完成，图片 Lib 已写入 {resRoot}");

                    foreach (var m in perMap)
                        _lastResults[m.Key] = BuildResultForMap(m.Key, m.Value.libs, m.Value.imgs, m.Value.skipped);
                }
                catch (Exception ex)
                {
                    AppendLog(log, $"[ERROR] 提取失败: {ex.Message}");
                    fail++;
                }
                finally
                {
                    // 稳定的 Mir2Res 根保留（含 Map/ 下图片 Lib，便于检查）；仅临时回退目录用后删除
                    if (isTempRoot) TryDelete(resRoot);
                }

                result.Ok = fail == 0;
                result.Success = success;
                result.Fail = fail;
                result.Failed = failed;
                result.Message = $"批量提取完成！\n成功: {success} 张, 失败: {fail} 张\n输出目录: {resRoot}";
                if (failed.Count > 0)
                    result.Message += "\n\n失败列表:\n" + string.Join("\n", failed);
                return result;
            }
        }

        private sealed class DistillStat
        {
            public int LibCount;
            public int ImageCount;
            public string Skipped = "";
        }

        /// <summary>
        /// 蒸馏单张地图用到的图片 Lib，按客户端相对路径去掉 "Data/Map/" 前缀后写到
        /// <paramref name="resRoot"/>/Map/&lt;地图名&gt;/...（即 Mir2Res/Map/0/WemadeMir2/Tiles.Lib）；不写 .map。
        /// 返回统计。
        /// </summary>
        private bool DistillOneMapToDisk(string clientRoot, string mapFilePath, string mapName, string resRoot, List<string> log, out DistillStat stat)
        {
            stat = new DistillStat();
            if (!File.Exists(mapFilePath))
            {
                AppendLog(log, $"[ERROR] 地图文件不存在: {mapFilePath}");
                return false;
            }

            byte[] mapBytes = File.ReadAllBytes(mapFilePath);
            if (mapBytes.Length == 0)
            {
                AppendLog(log, $"[ERROR] 地图 [{mapName}] 文件为空");
                return false;
            }

            MapReader mr;
            try { mr = new MapReader(mapBytes); }
            catch (Exception ex)
            {
                AppendLog(log, $"[ERROR] 地图 [{mapName}] 解析异常: {ex.Message}");
                return false;
            }

            if (mr.MapCells == null || mr.Width == 0 || mr.Height == 0)
            {
                AppendLog(log, $"[ERROR] 地图 [{mapName}] 解析失败 (尺寸: {mr.Width}x{mr.Height})");
                return false;
            }

            Dictionary<int, SortedSet<int>> used = MapUsageExtractor.Extract(mr, _config.DoorFrameSafety);

            int libCount = 0, imageCount = 0;
            var missing = new List<string>();
            const string mapPrefix = "Data/Map/";

            foreach (var kv in used)
            {
                int libIndex = kv.Key;
                string? rel = MapLibPathTable.GetRelPath(libIndex); // 如 "Data/Map/WemadeMir2/Tiles"
                if (rel == null) { missing.Add($"lib#{libIndex}(无路径)"); continue; }

                string srcLib = Path.Combine(clientRoot, rel + ".Lib");
                if (!File.Exists(srcLib)) { missing.Add(rel); continue; }

                byte[] distilled = LibDistiller.Distill(srcLib, new HashSet<int>(kv.Value));

                // 核心不变量：索引数量必须与源一致
                int outCount = BitConverter.ToInt32(distilled, 4);
                int srcCount = BitConverter.ToInt32(File.ReadAllBytes(srcLib), 4);
                if (outCount != srcCount)
                {
                    AppendLog(log, $"[ERROR] 蒸馏索引错位: {rel} 源 count={srcCount} 蒸馏 count={outCount}");
                    return false;
                }

                // 落到 Mir2Res/Map/<地图名>/ 下：resRoot 即 Mir2Res，需显式补一层 "Map"，
                // 去掉 "Data/Map/" 前缀得到 "WemadeMir2/Tiles"，
                // 即 Mir2Res/Map/0/WemadeMir2/Tiles.Lib（http 根=Mir2Res 时对应 URL /Map/0/WemadeMir2/Tiles.Lib）。
                string relNoDataMap = rel.StartsWith(mapPrefix, StringComparison.OrdinalIgnoreCase) ? rel.Substring(mapPrefix.Length) : rel;
                string outPath = Path.Combine(resRoot, "Map", mapName, relNoDataMap + ".Lib");
                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                File.WriteAllBytes(outPath, distilled);
                libCount++;
                imageCount += kv.Value.Count;
            }

            stat.LibCount = libCount;
            stat.ImageCount = imageCount;
            stat.Skipped = string.Join(";", missing);

            AppendLog(log, $"[{mapName}] 提取完成: {libCount} 库, {imageCount} 图" +
                           (missing.Count > 0 ? $"；缺失/跳过: {stat.Skipped}" : ""));
            return true;
        }

        private static void TryDelete(string dir)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
            catch { }
        }

        private MapResultDto BuildResultForMap(string mapName, int libs, int imgs, string skipped)
        {
            // 产物目录：Mir2Res/Map/（图片 Lib 直接平铺在此，按 libName 组织子目录）
            string resRoot = string.IsNullOrWhiteSpace(_config.PackRootPath)
                ? Path.Combine(Path.GetTempPath(), "MapExtract2_res")
                : _config.PackRootPath.TrimEnd('\\', '/');
            var dto = new MapResultDto
            {
                Map = mapName,
                OutputDir = resRoot,
                LibCount = libs,
                ImageCount = imgs,
                Skipped = skipped,
                Manifest = false,
            };
            string mapDir = Path.Combine(resRoot, "Map");
            if (Directory.Exists(mapDir))
            {
                foreach (var f in Directory.GetFiles(mapDir, "*.Lib", SearchOption.AllDirectories))
                    dto.Bundles.Add(new BundleInfo { Name = Path.GetRelativePath(resRoot, f), Size = new FileInfo(f).Length });
            }
            dto.Exists = dto.Bundles.Count > 0;
            return dto;
        }

        // ==================== 产物查询 ====================

        public MapResultDto GetResult(string mapName)
        {
            if (_lastResults.TryGetValue(mapName, out var cached))
                return cached;
            return BuildResultForMap(mapName, 0, 0, "");
        }

        // ==================== 日志辅助 ====================

        private void AppendLog(List<string> log, string line)
        {
            if (log.Count >= MaxLogLines)
            {
                if (log.Count == MaxLogLines) log.Add("...(日志过多已截断)");
                return;
            }
            log.Add(line);
        }

        private string? ResolveMapFile(string mapName)
        {
            foreach (var p in _scannedPaths)
            {
                if (string.Equals(Path.GetFileNameWithoutExtension(p), mapName, StringComparison.OrdinalIgnoreCase))
                    return p;
            }

            if (Directory.Exists(_config.MapDir))
            {
                foreach (var p in Directory.GetFiles(_config.MapDir, mapName + ".map", SearchOption.AllDirectories))
                    return p;
                foreach (var p in Directory.GetFiles(_config.MapDir, mapName + ".MAP", SearchOption.AllDirectories))
                    return p;
            }
            return null;
        }

        // ==================== 目录浏览 ====================

        public FsListDto ListDir(string? path, bool includeFiles = false, string? fileFilter = null)
        {
            var dto = new FsListDto();
            dto.Drives = GetDrives();

            if (string.IsNullOrWhiteSpace(path))
            {
                dto.Path = "";
                return dto;
            }

            try
            {
                var full = Path.GetFullPath(path);
                if (!Directory.Exists(full))
                {
                    dto.Path = path;
                    return dto;
                }

                dto.Path = full;
                dto.Parent = Directory.GetParent(full)?.FullName ?? "";

                foreach (var dir in Directory.GetDirectories(full).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
                {
                    dto.Directories.Add(new FsEntry
                    {
                        Name = Path.GetFileName(dir),
                        FullPath = dir
                    });
                }

                if (includeFiles)
                {
                    string filter = string.IsNullOrWhiteSpace(fileFilter) ? "*" : fileFilter;
                    foreach (var file in Directory.GetFiles(full, filter).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                    {
                        dto.Files.Add(new FsEntry
                        {
                            Name = Path.GetFileName(file),
                            FullPath = file
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "列举目录失败: {Path}", path);
                dto.Path = path ?? "";
            }

            return dto;
        }

        private static List<FsEntry> GetDrives()
        {
            var list = new List<FsEntry>();
            try
            {
                foreach (var d in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (!d.IsReady) continue;
                        list.Add(new FsEntry { Name = d.Name, FullPath = d.RootDirectory.FullName });
                    }
                    catch { }
                }
            }
            catch { }
            return list;
        }

        // ==================== 客户端资源根目录 → 相对路径自动探测 ====================

        private static readonly string[] FormatFolders = { "WemadeMir2", "ShandaMir2", "WemadeMir3" };

        public DetectResultDto Detect(string? clientRoot)
        {
            var r = new DetectResultDto();
            string root = (clientRoot ?? _config.ClientRootPath ?? "").Trim().TrimEnd('\\', '/');

            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                r.Message = "客户端资源根目录不存在: " + root;
                return r;
            }

            r.ClientRootPath = root;

            r.MapDir = Path.Combine(root, "Map");
            r.MapDirFound = Directory.Exists(r.MapDir) && (
                Directory.GetFiles(r.MapDir, "*.map", SearchOption.TopDirectoryOnly).Length > 0 ||
                Directory.GetFiles(r.MapDir, "*.MAP", SearchOption.TopDirectoryOnly).Length > 0);

            r.SourcePath = Path.Combine(root, "Data", "Map");
            r.SourcePathFound = Directory.Exists(r.SourcePath)
                                && FormatFolders.Any(f => Directory.Exists(Path.Combine(r.SourcePath, f)));

            string dataDir = Path.Combine(root, "Data");
            r.MinimapLibPath = FindMinimapLib(dataDir) ?? Path.Combine(dataDir, "mmap.Lib");
            r.MinimapLibFound = File.Exists(r.MinimapLibPath);
            r.MinimapDir = DeriveMinimapDir(r.MinimapLibPath);
            r.MinimapReady = MinimapReady(r.MinimapDir);

            r.MirDBPath = FindMirDB(root) ?? "";
            r.MirDBFound = r.MirDBPath.Length > 0;

            r.Ok = r.MapDirFound && r.SourcePathFound;
            r.Message = r.Ok
                ? "探测成功：地图目录、素材源路径已找到"
                  + (r.MinimapLibFound ? (r.MinimapReady ? "；小地图目录已有图" : "；小地图将按需从 mmap.Lib 抽取") : "（未找到 mmap.Lib）")
                  + (r.MirDBFound ? "；MirDB 已找到" : "（未找到 Server.MirDB）")
                : "探测完成，但部分路径未找到，请检查下方结果。";

            return r;
        }

        /// <summary>小地图 PNG 目录 = 与 mmap.Lib 同级、同名：Data\mmap.Lib → Data\mmap</summary>
        public static string DeriveMinimapDir(string libPath)
        {
            if (string.IsNullOrWhiteSpace(libPath)) return "";
            string? dir = Path.GetDirectoryName(libPath.TrimEnd('\\', '/'));
            if (string.IsNullOrEmpty(dir)) return "";
            return Path.Combine(dir, Path.GetFileNameWithoutExtension(libPath));
        }

        private static bool MinimapReady(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return false;
            try { return Directory.GetFiles(dir, "*.png", SearchOption.TopDirectoryOnly).Length > 0; }
            catch { return false; }
        }

        private static string? FindMinimapLib(string dataDir)
        {
            if (!Directory.Exists(dataDir)) return null;
            try
            {
                foreach (var f in Directory.GetFiles(dataDir, "*.Lib", SearchOption.TopDirectoryOnly))
                    if (string.Equals(Path.GetFileNameWithoutExtension(f), "mmap", StringComparison.OrdinalIgnoreCase))
                        return f;
            }
            catch { }
            return null;
        }

        private static string? FindMirDB(string root)
        {
            DirectoryInfo? dir = new DirectoryInfo(root);
            for (int up = 0; up <= 3 && dir != null; up++)
            {
                string[] candidates =
                {
                    Path.Combine(dir.FullName, "Server.MirDB"),
                    Path.Combine(dir.FullName, "Server", "Debug", "Server.MirDB"),
                    Path.Combine(dir.FullName, "Server", "Release", "Server.MirDB"),
                };
                foreach (var c in candidates)
                    if (File.Exists(c)) return c;
                dir = dir.Parent;
            }
            return null;
        }

        // ==================== 类型描述 ====================

        public static string GetTypeDescription(string fmt, int typeId)
        {
            switch (typeId)
            {
                case 0: return "传奇2 韩版原始格式 (OldSchool)";
                case 1: return "传奇2 韩版2010格式 (Map 2010 Ver 1.0)";
                case 2: return "传奇2 盛大旧格式 (Shanda Old)";
                case 3: return "传奇2 盛大2012格式 (Shanda 2012)";
                case 4: return "传奇2 韩版防挂格式 (AntiHack)";
                case 5: return "传奇3 韩版格式 (Mir3 Wemade)";
                case 6: return "传奇3 盛大格式 (Mir3 Shanda)";
                case 7: return "传奇3/4英雄格式 (Heroes)";
                case 100: return "C# 自定义格式 (Custom)";
                default: return fmt == "Unknown" ? "未知格式" : fmt;
            }
        }
    }
}
