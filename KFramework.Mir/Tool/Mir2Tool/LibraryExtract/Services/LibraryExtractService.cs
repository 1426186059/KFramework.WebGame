using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Text.Json;
using LibraryExtract.Models;
using Mir.Lib;

namespace LibraryExtract.Services
{
    /// <summary>
    /// 复刻 LibraryEditor/LMain.cs (WinForms) 的转换流程：
    /// 选择 Lib 文件/文件夹 → 递归扫描 *.Lib → 并行导出 PNG + Placements + Bytes。
    /// 原 BackgroundWorker 改为后台 Task + 轮询状态；Properties.Settings 改为 JSON 配置持久化。
    /// </summary>
    public sealed class LibraryExtractService : IDisposable
    {
        private const int MaxLogLines = 2000;
        private const int MaxScanReturn = 200;

        private readonly ILogger<LibraryExtractService> _logger;
        private readonly string _configPath;
        private readonly object _logGate = new();
        private readonly object _runGate = new();

        private LibraryExtractConfig _config;
        private CancellationTokenSource? _cts;
        private Task? _task;

        // 运行状态
        private bool _running, _cancelRequested, _cancelled;
        private int _percent;
        private string _message = "就绪";
        private int _totalFiles, _completedFiles, _totalImages;
        private string? _result, _error;
        private readonly List<string> _log = new();
        private bool _logTruncated;

        public LibraryExtractService(ILogger<LibraryExtractService> logger, IWebHostEnvironment env)
        {
            _logger = logger;
            _configPath = Path.Combine(env.ContentRootPath, "libraryextract.config.json");
            _config = LoadConfig();
        }

        // ==================== 配置 ====================

        public LibraryExtractConfig GetConfig() => _config;

        public LibraryExtractConfig UpdateConfig(LibraryExtractConfig cfg)
        {
            _config = cfg;
            SaveConfig();
            return _config;
        }

        private LibraryExtractConfig LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var cfg = JsonSerializer.Deserialize<LibraryExtractConfig>(File.ReadAllText(_configPath));
                    if (cfg != null) return cfg;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "读取配置失败，使用默认值");
            }
            return new LibraryExtractConfig();
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
            var err = Validate();
            if (err != null)
            {
                result.Message = err;
                return result;
            }

            string[] libFiles = GetLibFiles(_config.LibPath);
            result.Ok = true;
            result.Count = libFiles.Length;
            result.Message = libFiles.Length == 0
                ? "未找到任何 .Lib 文件。"
                : $"找到 {libFiles.Length} 个 .Lib 文件。";
            foreach (var f in libFiles.Take(MaxScanReturn)) result.Files.Add(f);
            return result;
        }

        private static string[] GetLibFiles(string libPath)
        {
            if (File.Exists(libPath)) return new[] { libPath };
            return Directory.GetFiles(libPath, "*.Lib", SearchOption.AllDirectories)
                            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                            .ToArray();
        }

        // ==================== 启动 / 取消 / 状态 ====================

        public StartResultDto Start()
        {
            lock (_runGate)
            {
                if (_running)
                    return new StartResultDto { Ok = false, Message = "转换正在进行中。" };

                var err = Validate();
                if (err != null)
                    return new StartResultDto { Ok = false, Message = err };

                ClearLog();
                _cancelled = false;
                _cancelRequested = false;
                _result = null;
                _error = null;
                _totalFiles = 0;
                _completedFiles = 0;
                _totalImages = 0;
                _percent = 0;
                _message = "正在扫描 .Lib 文件...";

                var libPath = _config.LibPath;
                var outputFolder = _config.OutputFolder;

                _cts = new CancellationTokenSource();
                var token = _cts.Token;
                _running = true;
                _task = Task.Run(() => Run(libPath, outputFolder, token), token);

                return new StartResultDto { Ok = true, Message = "已开始转换。" };
            }
        }

        public void Cancel()
        {
            _cancelRequested = true;
            try { _cts?.Cancel(); }
            catch { /* ignore */ }
        }

        public ConvertStatusDto GetStatus()
        {
            lock (_logGate)
            {
                bool running;
                lock (_runGate) running = _running;

                return new ConvertStatusDto
                {
                    Running = running,
                    CancelRequested = _cancelRequested,
                    Cancelled = _cancelled,
                    Percent = _percent,
                    Message = _message,
                    TotalFiles = _totalFiles,
                    CompletedFiles = _completedFiles,
                    TotalImages = _totalImages,
                    Result = _result,
                    Error = _error,
                    Log = new List<string>(_log),
                    LogTruncated = _logTruncated
                };
            }
        }

        private void SetProgress(int percent, string message)
        {
            _percent = Math.Clamp(percent, 0, 100);
            _message = message;
        }

        private void AppendLog(string line)
        {
            lock (_logGate)
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

        private void ClearLog()
        {
            lock (_logGate)
            {
                _log.Clear();
                _logTruncated = false;
            }
        }

        /// <summary>对应 LMain.BtnStartConvert_Click 里的校验</summary>
        private string? Validate()
        {
            string libPath = (_config.LibPath ?? "").Trim();
            string outputFolder = (_config.OutputFolder ?? "").Trim();

            if (string.IsNullOrEmpty(libPath))
                return "请先选择 Lib 文件夹或文件。";

            bool isFile = File.Exists(libPath);
            bool isDir = Directory.Exists(libPath);

            if (!isFile && !isDir)
                return "路径不存在，请检查。";

            if (isFile && !libPath.EndsWith(".Lib", StringComparison.OrdinalIgnoreCase))
                return "请选择 .Lib 文件。";

            if (string.IsNullOrEmpty(outputFolder))
                return "请先选择输出文件夹。";

            if (!Directory.Exists(outputFolder))
            {
                try { Directory.CreateDirectory(outputFolder); }
                catch (Exception ex) { return "无法创建输出文件夹: " + ex.Message; }
            }

            return null;
        }

        // ==================== 转换主流程 ====================

        /// <summary>移植自 LMain.BackgroundWorker_DoWork</summary>
        private void Run(string libPath, string outputFolder, CancellationToken token)
        {
            try
            {
                string[] libFiles = GetLibFiles(libPath);
                int totalFiles = libFiles.Length;

                _totalFiles = totalFiles;

                if (totalFiles == 0)
                {
                    _result = "未找到任何 .Lib 文件。";
                    _percent = 100;
                    _message = _result;
                    AppendLog(_result);
                    return;
                }

                AppendLog($"开始转换: 共 {totalFiles} 个 .Lib 文件");

                int completedFiles = 0;
                int totalImages = 0;
                int cpuCount = Environment.ProcessorCount;

                // 与原版一致：不使用 ParallelOptions.CancellationToken，
                // 取消靠 token.IsCancellationRequested 协作式检查（等价 worker.CancellationPending）
                var parallelOpts = new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Max(1, cpuCount)
                };

                Parallel.ForEach(libFiles, parallelOpts, (libFilePath, state) =>
                {
                    if (token.IsCancellationRequested)
                    {
                        state.Stop();
                        return;
                    }

                    string libFileName = Path.GetFileNameWithoutExtension(libFilePath);

                    // 计算输出目录：文件夹输入保持相对结构，单文件输入直接用文件名
                    string outputDir;
                    if (File.Exists(libPath))
                    {
                        outputDir = Path.Combine(outputFolder, libFileName);
                    }
                    else
                    {
                        string relativePath = libFilePath.Substring(libPath.Length).TrimStart(Path.DirectorySeparatorChar);
                        string relativeDir = Path.GetDirectoryName(relativePath) ?? "";
                        outputDir = string.IsNullOrEmpty(relativeDir)
                            ? Path.Combine(outputFolder, libFileName)
                            : Path.Combine(outputFolder, relativeDir, libFileName);
                    }

                    Directory.CreateDirectory(outputDir);
                    string placementDir = Path.Combine(outputDir, "Placements");
                    Directory.CreateDirectory(placementDir);
                    string bytesDir = Path.Combine(outputFolder, "Bytes");
                    Directory.CreateDirectory(bytesDir);

                    try
                    {
                        MLibraryV2 library = new MLibraryV2(libFilePath);
                        int imageCount = library.Count;

                        for (int j = 0; j < imageCount; j++)
                        {
                            if (token.IsCancellationRequested)
                            {
                                library.Close();
                                return; // 不调 state.Stop()，避免误杀其他正在写盘的线程
                            }

                            MLibraryV2.MImage? mImage = library.GetMImage(j);
                            if (mImage == null || mImage.Image == null)
                                continue;

                            Image image = mImage.Image;
                            string pngPath = Path.Combine(outputDir, j.ToString() + ".png");
                            image.Save(pngPath, System.Drawing.Imaging.ImageFormat.Png);
                            image.Dispose();
                            mImage.Image = null;

                            File.WriteAllLines(
                                Path.Combine(placementDir, j.ToString() + ".txt"),
                                new string[] { mImage.X.ToString(), mImage.Y.ToString() });
                        }

                        // 导出整合的 .bytes 索引文件
                        library.SaveBytesV2(Path.Combine(bytesDir, libFileName + ".bytes"));

                        library.Close();

                        int finished = Interlocked.Increment(ref completedFiles);
                        int thisImages = imageCount;
                        Interlocked.Add(ref totalImages, thisImages);
                        _completedFiles = completedFiles;
                        _totalImages = totalImages;

                        // 上限 99%，全部完成后再设 100%，保证所有文件已写盘完毕
                        int percent = Math.Min(99, (int)((float)finished / totalFiles * 100));
                        string msg = $"正在转换: {libFileName} (已完成 {finished}/{totalFiles} 文件, {thisImages} 张图片)";
                        SetProgress(percent, msg);
                        AppendLog(msg);
                    }
                    catch (Exception ex)
                    {
                        string errLine = $"[{DateTime.Now}] 文件: {libFilePath}, 错误: {ex.Message}";
                        try
                        {
                            File.AppendAllText(
                                Path.Combine(outputFolder, "ConvertError.txt"),
                                errLine + Environment.NewLine);
                        }
                        catch { /* 写不了就算了，至少记录到日志 */ }
                        AppendLog("[ERROR] " + errLine);
                    }
                });

                if (token.IsCancellationRequested)
                {
                    _cancelled = true;
                    _percent = 0;
                    _message = "转换已取消。";
                    AppendLog("转换已取消。");
                    return;
                }

                _completedFiles = completedFiles;
                _totalImages = totalImages;
                _result = $"转换完成! 共处理 {totalFiles} 个 .Lib 文件 ({totalImages} 张图片), 输出到: {outputFolder}";
                _percent = 100;
                _message = _result;
                AppendLog(_result);
            }
            catch (OperationCanceledException)
            {
                _cancelled = true;
                _percent = 0;
                _message = "转换已取消。";
                AppendLog("转换已取消。");
            }
            catch (Exception ex)
            {
                _error = ex.Message;
                _message = "转换出错: " + ex.Message;
                AppendLog("[ERROR] " + _message);
                _logger.LogError(ex, "转换过程发生错误");
            }
            finally
            {
                lock (_runGate) _running = false;
                _cancelRequested = false;
            }
        }

        // ==================== 目录浏览 ====================

        public FsListDto ListDir(string? path, bool includeFiles = false, string? fileFilter = null)
        {
            var dto = new FsListDto { Drives = GetDrives() };

            if (string.IsNullOrWhiteSpace(path))
                return dto;

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

                if (includeFiles)
                {
                    string filter = string.IsNullOrWhiteSpace(fileFilter) ? "*" : fileFilter;
                    foreach (var file in Directory.GetFiles(full, filter).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                        dto.Files.Add(new FsEntry { Name = Path.GetFileName(file), FullPath = file });
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

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}
