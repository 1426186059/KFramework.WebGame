using System.Text;
using System.Text.Json;
using MapTool.Models;
using Mir.Map;

namespace MapTool.Services
{
    /// <summary>
    /// 复刻 Unity 工程 Assets/Editor/MapTool.cs (MapToolWindow) 的全部功能：
    /// 路径配置 / 扫描地图 / 加载 MirDB / 提取地图 / 小地图预览。
    /// 抽离为一组无状态的 HTTP 调用即可使用的服务。
    /// </summary>
    public sealed class MapToolService
    {
        private const int MaxLogLines = 2000;

        private readonly ILogger<MapToolService> _logger;
        private readonly string _configPath;
        private readonly object _work = new object();

        private MapToolConfig _config;

        // MirDB 解析缓存：MapName(string) → MapEntry
        private Dictionary<string, MirDBParser.MapEntry>? _mirDb;
        private int _mirDbRawCount;

        // 扫描缓存
        private readonly List<string> _scannedPaths = new();
        private readonly Dictionary<string, string> _mapFormats = new();
        private readonly Dictionary<string, int> _mapTypeIds = new();

        public MapToolService(ILogger<MapToolService> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            _configPath = Path.Combine(env.ContentRootPath, "maptool.config.json");
            _config = LoadConfig();

            // OnEnable 时的自动加载行为
            if (!string.IsNullOrEmpty(_config.MirDBPath) && File.Exists(_config.MirDBPath))
            {
                try { LoadMirDB(); }
                catch (Exception ex) { _logger.LogWarning(ex, "自动加载 MirDB 失败"); }
            }
        }

        public bool MirDBLoaded => _mirDb != null;
        public int MirDBCount => _mirDb?.Count ?? 0;

        // ==================== 配置 ====================

        public MapToolConfig GetConfig() => _config;

        public MapToolConfig UpdateConfig(MapToolConfig cfg)
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

        private MapToolConfig LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    var cfg = JsonSerializer.Deserialize<MapToolConfig>(json);
                    if (cfg != null) return cfg;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "读取配置失败，使用默认值");
            }
            return new MapToolConfig();
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
                TilesExists = Directory.Exists(src + "/Tiles"),
                SmTilesExists = Directory.Exists(src + "/SmTiles"),
                ObjectsExists = Directory.Exists(src + "/Objects"),
                MinimapExists = Directory.Exists(c.MinimapPath),
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
                    // FileName 可能是 "66" 或 "066" 等形式，统一用小写无扩展名匹配
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

                // 构建返回列表
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

        // ==================== 提取地图 ====================

        public ExtractResultDto Extract(List<string> mapNames)
        {
            lock (_work)
            {
                var result = new ExtractResultDto
                {
                    DestinationPath = _config.DestinationPath
                };
                var log = result.Log;

                if (mapNames == null || mapNames.Count == 0)
                {
                    result.Message = "请先勾选要提取的地图。";
                    return result;
                }

                int success = 0, fail = 0;
                var failed = new List<string>();

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
                        if (ProcessOneMap(mapFile, mapName, log))
                            success++;
                        else
                        {
                            fail++;
                            failed.Add(mapName + " (提取失败)");
                        }
                    }
                    catch (Exception ex)
                    {
                        fail++;
                        failed.Add(mapName + $" ({ex.Message})");
                        AppendLog(log, $"[ERROR] {mapName}: {ex}");
                    }
                }

                result.Ok = fail == 0;
                result.Success = success;
                result.Fail = fail;
                result.Failed = failed;
                result.Message = $"批量提取完成！\n成功: {success} 张, 失败: {fail} 张\n输出目录: {_config.DestinationPath}";
                if (failed.Count > 0)
                    result.Message += "\n\n失败列表:\n" + string.Join("\n", failed);
                return result;
            }
        }

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

        /// <summary>移植自 MapToolWindow.ProcessOneMap</summary>
        private bool ProcessOneMap(string mapFilePath, string mapName, List<string> log)
        {
            if (!File.Exists(mapFilePath))
            {
                AppendLog(log, $"[ERROR] 地图文件不存在: {mapFilePath}");
                return false;
            }

            byte[] mapBytes = File.ReadAllBytes(mapFilePath);
            if (mapBytes.Length == 0)
            {
                AppendLog(log, $"[ERROR] 地图 [{mapName}] 文件为空: {mapFilePath}");
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
                AppendLog(log, $"[ERROR] 地图 [{mapName}] 解析失败 (尺寸: {mr.Width}x{mr.Height}, Cells: {(mr.MapCells != null ? "ok" : "null")})");
                return false;
            }

            // 根据地图格式自动匹配素材库路径
            string detectedFormat = mr.MapFormatName;
            string autoSourcePath = ResolveFormatSourcePath(detectedFormat);
            if (!string.Equals(autoSourcePath, _config.SourcePath, StringComparison.OrdinalIgnoreCase))
                AppendLog(log, $"[自动匹配] 地图 [{mapName}] 格式={detectedFormat} → 素材路径: {autoSourcePath}");

            AppendLog(log, $"[{mapName}] 格式={detectedFormat}, 尺寸={mr.Width}x{mr.Height}, 素材路径={autoSourcePath}");

            string src = autoSourcePath.TrimEnd('\\', '/') + "/";
            string dst = _config.DestinationPath.TrimEnd('\\', '/') + "/";

            mr.SourcePath = src;
            mr.DestinationPath = dst;
            mr.MinimapPath = _config.MinimapPath.TrimEnd('\\', '/') + "/";

            // 从 MirDB 自动匹配 MiniMap/BigMap 索引及中文名
            string? chineseTitle = null;
            if (_mirDb != null && _mirDb.TryGetValue(mapName, out var dbEntry))
            {
                mr.MinimapIndex = dbEntry.MiniMap;
                mr.BigMapIndex = dbEntry.BigMap;
                if (dbEntry.MiniMap > 0 || dbEntry.BigMap > 0)
                    AppendLog(log, $"[{mapName}] MirDB 匹配: MiniMap={dbEntry.MiniMap}, BigMap={dbEntry.BigMap}, 标题={dbEntry.Title}");
                chineseTitle = dbEntry.Title;
            }
            else
            {
                mr.MinimapIndex = -1;
                mr.BigMapIndex = -1;
            }

            mr.LogSink = line => AppendLog(log, line);
            mr.MapName = mapName;
            mr.DrawFloor();

            // 将中文名写入地图目录下的 {中文名}.txt
            if (!string.IsNullOrEmpty(chineseTitle))
            {
                try
                {
                    string titleFilePath = dst + mapName + "/" + chineseTitle + ".txt";
                    File.WriteAllText(titleFilePath, chineseTitle, Encoding.UTF8);
                    AppendLog(log, $"[{mapName}] 中文名已保存: {titleFilePath} -> {chineseTitle}");
                }
                catch (Exception ex)
                {
                    AppendLog(log, $"[WARN] [{mapName}] 写入中文名失败: {ex.Message}");
                }
            }

            AppendLog(log, $"地图 [{mapName}] 提取完成 -> {dst}{mapName}");
            return true;
        }

        /// <summary>移植自 MapToolWindow.formatToSourcePath（改为基于配置的源路径）</summary>
        private string ResolveFormatSourcePath(string fmt)
        {
            string root = _config.SourcePath.TrimEnd('\\', '/');
            switch (fmt)
            {
                case "WemadeMir2": return root + "\\WemadeMir2";
                case "ShandaMir2": return root + "\\ShandaMir2";
                case "WemadeMir3":
                case "ShandaMir3":
                case "Heroes": return root + "\\WemadeMir3";
                case "Custom": return root + "\\WemadeMir2";
                default: return _config.SourcePath;
            }
        }

        // ==================== 预览 ====================

        /// <summary>返回预览图片字节（原版 LargeMap.png 或渲染图 LargeMap2.png）。</summary>
        public byte[]? GetPreview(string mapName, bool rendered)
        {
            if (string.IsNullOrWhiteSpace(mapName)) return null;
            string dir = _config.DestinationPath.TrimEnd('\\', '/') + "/" + mapName + "/MMap/";
            string file = dir + (rendered ? "LargeMap2.png" : "LargeMap.png");
            try
            {
                if (!File.Exists(file)) return null;
                return File.ReadAllBytes(file);
            }
            catch { return null; }
        }

        public string GetPreviewPath(string mapName, bool rendered)
        {
            string dir = _config.DestinationPath.TrimEnd('\\', '/') + "/" + mapName + "/MMap/";
            return dir + (rendered ? "LargeMap2.png" : "LargeMap.png");
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

        // ==================== 类型描述（移植自 MapToolWindow.GetTypeDescription）====================

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
