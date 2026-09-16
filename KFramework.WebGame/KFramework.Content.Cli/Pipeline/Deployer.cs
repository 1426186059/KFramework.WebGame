using System.Diagnostics;
using System.IO;
using KFramework.MonoGame;

namespace KFramework.Content.Build;

/// <summary>
/// 发布（部署）阶段：根据配置把构建产物发布出去。
/// <list type="bullet">
///   <item><c>www</c> / <c>copy</c>：把 outputDirectory 整体镜像复制到 wwwDir（相对 root）。</item>
///   <item><c>serve</c>：在 outputDirectory 上启动一个本地静态 Web 服务（阻塞，直到 Ctrl+C）。不自建服务器，复用系统已安装的 python / node。</item>
///   <item><c>none</c>（或其它值）：不发布。</item>
/// </list>
/// </summary>
public static class Deployer
{
    public static void Deploy(BuildConfig config, string root, string outputDirectory, List<string> warnings)
    {
        string mode = config.Deploy;
        if (mode is "www" or "copy")
        {
            string wwwDir = Path.Combine(root, config.WwwDir);
            CopyDirectory(outputDirectory, wwwDir);
            PrintTool.Log($"[kfc] 已发布到 {wwwDir}（deploy = {mode}）");
        }
        else if (mode == "serve")
        {
            ServerContent(outputDirectory, config.Port); // 阻塞直到 Ctrl+C
        }
        else if (mode != "none")
        {
            warnings.Add($"未知的 deploy 模式：{mode}（可选 www / serve / none）");
        }
    }

    /// <summary>把 source 目录整体镜像复制到 dest（先清空 dest 再复制，保证不含残留旧文件）。</summary>
    private static void CopyDirectory(string source, string dest)
    {
        if (Directory.Exists(dest)) Directory.Delete(dest, recursive: true);
        Directory.CreateDirectory(dest);

        foreach (string file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);
        foreach (string dir in Directory.EnumerateDirectories(source))
            CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
    }

    /// <summary>
    /// 在指定目录上启动一个本地静态 Web 服务（用于开发调试）。
    /// 不自建 HTTP 服务器：依次尝试 python / python3 / npx http-server，通过命令行拉起独立进程；Ctrl+C 退出。
    /// </summary>
    private static void ServerContent(string directory, int port)
    {
        // 复用系统已安装的 Python / Node 标准能力，而非自己实现服务器
        var candidates = new (string FileName, string Args)[]
        {
            ("python", $"-m http.server {port} --directory \"{directory}\""),
            ("python3", $"-m http.server {port} --directory \"{directory}\""),
            ("npx", $"-y http-server \"{directory}\" -p {port}"),
        };

        Process? server = null;
        foreach (var (fileName, args) in candidates)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = args,
                    UseShellExecute = true,
                    WorkingDirectory = directory,
                };
                server = Process.Start(psi);
                if (server != null)
                {
                    PrintTool.Log($"[kfc] 本地 Web 服务已启动（{fileName}）：http://localhost:{port}/ （Ctrl+C 退出）");
                    break;
                }
            }
            catch
            {
                server = null;
            }
        }

        if (server == null)
        {
            PrintTool.Log($"[kfc] 未找到可用的 Web 服务器（python / python3 / npx）。请安装其一后再用 deploy=serve。");
            return;
        }

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = false;
            try { server.Kill(); } catch { /* ignore */ }
        };
        server.WaitForExit();
    }
}
