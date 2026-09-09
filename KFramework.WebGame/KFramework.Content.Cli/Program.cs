using KFramework.Content.Pipeline;

// kfc —— KFramework 内容管线命令行工具
//   kfc --root <内容项目目录> [--out <发布目录>] [--atlas-size 2048] [--no-preview]
// 约定：<root>/raw 是开发者维护的原始资源，<root>/content 是工具生成的发布资源。

const int ExitSuccess = 0;
const int ExitFailure = 1;

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
            Console.WriteLine("用法: kfc --root <内容项目目录> [--out <输出目录>] [--atlas-size 2048] [--no-preview]");
            return ExitSuccess;
    }
}

string projectDirectory = Path.GetFullPath(root);
string rawDirectory = Path.Combine(projectDirectory, "raw");
string outputDirectory = Path.GetFullPath(output ?? Path.Combine(projectDirectory, "content"));

if (!Directory.Exists(rawDirectory))
{
    Console.Error.WriteLine($"错误：原始资源目录不存在 -> {rawDirectory}");
    return ExitFailure;
}

try
{
    var builder = new ContentBuilder();
    BuildReport report = builder.Build(rawDirectory, outputDirectory, new ContentBuilder.BuildOptions
    {
        AtlasMaxSize = atlasSize,
        WritePreviewPng = preview,
    });

    Console.WriteLine($"[kfc] raw     : {rawDirectory}");
    Console.WriteLine($"[kfc] content : {outputDirectory}");
    Console.WriteLine($"[kfc] {report}");

    foreach (string warning in report.Warnings)
        Console.WriteLine($"[kfc] 警告: {warning}");

    return ExitSuccess;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[kfc] 构建失败: {ex.Message}");
    return ExitFailure;
}
