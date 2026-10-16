using System.Text;
using System.Text.Json;
using Mir.Lib;
using SkiaSharp;
using TextureToWebP.Models;

namespace TextureToWebP.Services
{
    /// <summary>
    /// 把选中文件夹（含所有子目录）里的图片批量转成 WebP。
    /// 网页游戏用途：WebP 相比 PNG 体积通常能小 60%~80%，且浏览器原生支持。
    /// </summary>
    public sealed class TextureService
    {
        private const int MaxLogLines = 2000;
        private const int MaxScanReturn = 300;

        private readonly ILogger<TextureService> _logger;
        private readonly string _configPath;
        private readonly object _gate = new();

        private TextureConfig _config;

        private CancellationTokenSource? _cts;
        private Task? _task;
        private bool _running, _cancelled;
        private int _percent;
        private string _message = "就绪";
        private string _current = "";
        private int _done, _total;
        private long _sourceBytes, _webpBytes;
        private string? _result, _error;
        private readonly List<string> _log = new();
        private bool _logTruncated;

        public TextureService(ILogger<TextureService> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            _configPath = Path.Combine(env.ContentRootPath, "texturetowebp.config.json");
            _config = LoadConfig();
        }

        // ==================== 配置 ====================

        public TextureConfig GetConfig() => _config;

        public TextureConfig UpdateConfig(TextureConfig cfg)
        {
            _config = cfg;
            SaveConfig();
            return _config;
        }

        private TextureConfig LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var cfg = JsonSerializer.Deserialize<TextureConfig>(File.ReadAllText(_configPath));
                    if (cfg != null) return cfg;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "读取配置失败，使用默认值");
            }
            return new TextureConfig();
        }

        private void SaveConfig()
        {
            try
            {
                File.WriteAllText(_configPath,
                    JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true }),
                    Encoding.UTF8);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "保存配置失败");
            }
        }

        // ==================== 扫描 ====================

        public ScanResultDto Scan()
        {
            var result = new ScanResultDto();

            string root = (_config.SourceFolder ?? "").Trim().TrimEnd('\\', '/');
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                result.Message = "源文件夹不存在: " + root;
                return result;
            }

            var files = CollectFiles(root);
            result.Ok = true;
            result.Count = files.Count;
            result.Message = files.Count == 0
                ? "未找到匹配的图片。"
                : $"找到 {files.Count} 张待转换图片。";

            long total = 0;
            foreach (var f in files)
            {
                try { total += new FileInfo(f).Length; } catch { }
                if (result.Files.Count < MaxScanReturn) result.Files.Add(f);
            }
            result.TotalBytes = total;
            return result;
        }

        private List<string> CollectFiles(string root)
        {
            var option = _config.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var exts = ParseExtensions(_config.Extensions);

            var files = new List<string>();

            // 先按扩展名枚举（快），再统一去重、排序
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var ext in exts)
            {
                try
                {
                    foreach (var f in Directory.GetFiles(root, "*" + ext, option))
                        if (seen.Add(f)) files.Add(f);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "枚举失败: {Root} {Ext}", root, ext);
                }
            }

            files.Sort(StringComparer.OrdinalIgnoreCase);
            return files;
        }

        private static List<string> ParseExtensions(string? raw)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(raw)) return new List<string> { ".png" };

            foreach (var part in raw.Split(',', ';', ' '))
            {
                string e = part.Trim();
                if (string.IsNullOrEmpty(e)) continue;
                if (!e.StartsWith(".")) e = "." + e;
                list.Add(e.ToLowerInvariant());
            }
            return list.Count > 0 ? list : new List<string> { ".png" };
        }

        // ==================== 转换 ====================

        public OpResultDto Start()
        {
            lock (_gate)
            {
                if (_running)
                    return new OpResultDto { Ok = false, Message = "转换正在进行中。" };

                string root = (_config.SourceFolder ?? "").Trim().TrimEnd('\\', '/');
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                    return new OpResultDto { Ok = false, Message = "源文件夹不存在。" };

                var files = CollectFiles(root);
                if (files.Count == 0)
                    return new OpResultDto { Ok = false, Message = "未找到匹配的图片。" };

                _log.Clear();
                _logTruncated = false;
                _cancelled = false;
                _result = null;
                _error = null;
                _percent = 0;
                _message = "准备转换...";
                _current = "";
                _done = 0;
                _total = files.Count;
                _sourceBytes = 0;
                _webpBytes = 0;

                var cfg = _config;
                _cts = new CancellationTokenSource();
                var token = _cts.Token;
                _running = true;
                _task = Task.Run(() => Run(files, root, cfg, token), token);

                return new OpResultDto { Ok = true, Message = $"开始转换 {files.Count} 张图片。" };
            }
        }

        public void Cancel()
        {
            try { _cts?.Cancel(); } catch { }
        }

        public ConvertStatusDto GetStatus()
        {
            lock (_gate)
            {
                return new ConvertStatusDto
                {
                    Running = _running,
                    Cancelled = _cancelled,
                    Percent = _percent,
                    Message = _message,
                    Current = _current,
                    Done = _done,
                    Total = _total,
                    SourceBytes = _sourceBytes,
                    WebPBytes = _webpBytes,
                    Result = _result,
                    Error = _error,
                    Log = new List<string>(_log),
                    LogTruncated = _logTruncated
                };
            }
        }

        private void AppendLog(string line)
        {
            lock (_gate)
            {
                if (_log.Count >= MaxLogLines)
                {
                    if (!_logTruncated)
                    {
                        _log.Add("...(日志过多已截断)");
                        _logTruncated = true;
                    }
                    return;
                }
                _log.Add(line);
            }
        }

        /// <summary>输出路径：留空输出目录 = 源文件同目录；否则保持相对子目录结构</summary>
        internal static string ResolveOutput(string file, string sourceFolder, string? outputFolder)
        {
            string rel = file;
            if (!string.IsNullOrEmpty(sourceFolder) && file.StartsWith(sourceFolder, StringComparison.OrdinalIgnoreCase))
                rel = file.Substring(sourceFolder.Length).TrimStart('\\', '/');

            string dir;
            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                dir = Path.GetDirectoryName(file) ?? "";
            }
            else
            {
                dir = Path.Combine(outputFolder, Path.GetDirectoryName(rel) ?? "");
            }

            return Path.Combine(dir, Path.GetFileNameWithoutExtension(file) + ".webp");
        }

        private void Run(List<string> files, string root, TextureConfig cfg, CancellationToken token)
        {
            try
            {
                int done = 0, failed = 0, skipped = 0;
                long srcBytes = 0, webpBytes = 0;
                string? outputFolder = string.IsNullOrWhiteSpace(cfg.OutputFolder) ? null : cfg.OutputFolder.Trim();

                foreach (var file in files)
                {
                    if (token.IsCancellationRequested) break;

                    _current = file;
                    if (done % 50 == 0)
                    {
                        _message = $"正在转换 ({done}/{files.Count}) {Path.GetFileName(file)}";
                        _percent = Math.Min(99, (int)((float)done / files.Count * 100));
                    }

                    try
                    {
                        string dest = ResolveOutput(file, root, outputFolder);

                        if (!cfg.Overwrite && File.Exists(dest))
                        {
                            skipped++;
                            done++;
                            continue;
                        }

                        using var bmp = SkiaBitmaps.Decode(file);
                        if (bmp == null)
                        {
                            failed++;
                            AppendLog("[WARN] 无法解码: " + file);
                            done++;
                            continue;
                        }

                        try { srcBytes += new FileInfo(file).Length; } catch { }

                        SkiaBitmaps.SaveWebP(bmp, dest, ClampQuality(cfg.Quality), cfg.Lossless);

                        try { webpBytes += new FileInfo(dest).Length; } catch { }

                        if (cfg.DeleteSource)
                        {
                            bmp.Dispose();
                            File.Delete(file);
                        }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        AppendLog("[ERROR] " + file + " -> " + ex.Message);
                    }

                    done++;
                    _done = done;
                    _sourceBytes = srcBytes;
                    _webpBytes = webpBytes;
                    _percent = Math.Min(99, (int)((float)done / files.Count * 100));
                }

                if (token.IsCancellationRequested)
                {
                    _cancelled = true;
                    _percent = 0;
                    _message = "转换已取消。";
                    AppendLog("转换已取消。");
                    return;
                }

                double ratio = srcBytes > 0 ? (1.0 - (double)webpBytes / srcBytes) * 100 : 0;
                _result = $"转换完成：成功 {done - failed - skipped} 张，跳过 {skipped} 张，失败 {failed} 张；" +
                          $"体积 {Fmt(srcBytes)} → {Fmt(webpBytes)}（节省 {ratio:F1}%）";
                _percent = 100;
                _message = _result;
                AppendLog(_result);
            }
            catch (Exception ex)
            {
                _error = ex.Message;
                _message = "转换出错: " + ex.Message;
                AppendLog("[ERROR] " + _message);
                _logger.LogError(ex, "WebP 转换过程发生错误");
            }
            finally
            {
                _running = false;
                _current = "";
            }
        }

        private static int ClampQuality(int q) => q < 1 ? 1 : (q > 100 ? 100 : q);

        internal static string Fmt(long bytes)
        {
            if (bytes >= 1024L * 1024 * 1024) return (bytes / 1073741824.0).ToString("F2") + " GB";
            if (bytes >= 1024L * 1024) return (bytes / 1048576.0).ToString("F1") + " MB";
            if (bytes >= 1024) return (bytes / 1024.0).ToString("F0") + " KB";
            return bytes + " B";
        }

        // ==================== 目录浏览 ====================

        public FsListDto ListDir(string? path)
        {
            var dto = new FsListDto { Drives = GetDrives() };

            if (string.IsNullOrWhiteSpace(path)) return dto;

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
                    dto.Directories.Add(new FsEntry { Name = Path.GetFileName(dir), FullPath = dir });
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
    }
}
