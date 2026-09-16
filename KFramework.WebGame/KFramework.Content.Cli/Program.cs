using KFramework.MonoGame;

namespace KFramework.Content.Cli
{
    /// <summary>
    /// kfc —— KFramework.MonoGame 内容管线命令行工具
    /// 用法：kfc --root &lt;内容项目目录&gt; [--out &lt;发布目录&gt;] [--atlas-size 2048] [--no-preview]
    /// 约定：&lt;root&gt;/raw 是开发者维护的原始资源，&lt;root&gt;/content 是工具生成的发布资源。
    /// </summary>
    internal static class Program
    {
        private const int ExitSuccess = 0;
        private const int ExitFailure = 1;

        private static int Main(string[] args)
        {
            string root = Directory.GetCurrentDirectory();
            string? output = null;
            int atlasSize = 2048;
            bool preview = true;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--root" or "-r" when i + 1 < args.Length:
                        root = args[++i];
                        break;
                    case "--out" or "-o" when i + 1 < args.Length:
                        output = args[++i];
                        break;
                    case "--atlas-size" when i + 1 < args.Length:
                        atlasSize = int.Parse(args[++i]);
                        break;
                    case "--no-preview":
                        preview = false;
                        break;
                    case "--help" or "-h":
                        PrintTool.Log("用法: kfc --root <内容项目目录> [--out <输出目录>] [--atlas-size 2048] [--no-preview]");
                        return ExitSuccess;
                }
            }

            Global.mBuildConfig = BuildConfig.Load(root);
            string ContentDir = Path.GetFullPath(root);
            string rawDir = Path.Combine(ContentDir, Global.mBuildConfig.RawDir);

            if (!Directory.Exists(rawDir))
            {
                //原始资源目录不存在，那就直接成功就行了啊
                return ExitSuccess;
            }

            try
            {
                var builder = new ContentBuilder();
                BuildReport report = builder.Build(rawDir, new ContentBuilder.BuildOptions
                {
                    AtlasMaxSize = atlasSize,
                    WritePreviewPng = preview,
                });

                PrintTool.Log($"[kfc] raw     : {rawDir}");
                PrintTool.Log($"[kfc] content : {report.OutputDirectory}");
                PrintTool.Log($"[kfc] {report}");

                foreach (string warning in report.Warnings)
                    PrintTool.Log($"[kfc] 警告: {warning}");

                return ExitSuccess;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[kfc] 构建失败: {ex.Message}");
                return ExitFailure;
            }
        }
    }
}
