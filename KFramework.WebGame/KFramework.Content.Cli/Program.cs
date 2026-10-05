using KFramework.MonoGame;
using System.Text;

namespace KFramework.Content.Cli
{
    /// <summary>
    /// kfc —— KFramework.MonoGame 内容管线命令行工具
    /// 用法：kfc [--root &lt;内容项目目录&gt;]
    /// 约定：&lt;root&gt; 下需有 build.config.json（不存在则自动生成默认配置）；
    /// 不传任何参数时，以「当前执行目录」作为 &lt;root&gt; 查找 build.config.json。
    /// </summary>
    internal static class Program
    {
        private const int ExitSuccess = 0;
        private const int ExitFailure = 1;

        private static int Main(string[] args)
        {
            // 强制控制台使用 UTF-8 输出，避免在 GBK（代码页 936）终端下中文日志乱码。
            // 在 Windows 上，设置 OutputEncoding 会同时把控制台输出代码页切到 65001。
            try
            {
                var utf8NoBom = new UTF8Encoding(false);
                Console.OutputEncoding = utf8NoBom;
                Console.InputEncoding = utf8NoBom;
            }
            catch
            {
                // 某些重定向环境不允许修改控制台编码，忽略即可。
            }

            // 仅保留 --root / -r；无参数时以当前执行目录作为内容项目根目录
            string root = Directory.GetCurrentDirectory();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--root" or "-r" when i + 1 < args.Length:
                        root = args[++i];
                        break;
                }
            }

            // 加载 build.config.json（不存在则自动生成默认配置），并解析为完整路径结果
            BuildConfig config = BuildConfig.Load(root);
            BuildConfigResult.Parse(config, root);

            string rawDir = BuildConfigResult.RawDirFull;

            if (!Directory.Exists(rawDir))
            {
                //原始资源目录不存在，那就直接成功就行了啊
                return ExitSuccess;
            }

            try
            {
                var builder = new ContentBuilder();
                BuildOptions.AtlasMaxSize = 2048;
                BuildOptions.WritePreviewPng = true;
                BuildReport report = builder.Build(rawDir);

                PrintTool.Log($"[kfc] raw     : {rawDir}");
                PrintTool.Log($"[kfc] content : {report.OutputDirectory}");
                PrintTool.Log($"[kfc] {report}");

                foreach (string warning in report.Warnings)
                    PrintTool.Log($"[kfc] 警告: {warning}");

                return ExitSuccess;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[kfc] 构建失败: {ex}");
                return ExitFailure;
            }
        }
    }
}
