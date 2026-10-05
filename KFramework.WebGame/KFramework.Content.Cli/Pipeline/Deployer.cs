using KFramework.MonoGame;

namespace KFramework.Content.Cli
{
    public static class Deployer
    {
        public static void Deploy(BuildConfig config, string root, string outputDirectory, List<string> warnings)
        {
            if (config.copy_to_wwwroot)
            {
                string targetDirName = outputDirectory.Substring(outputDirectory.LastIndexOf(Path.DirectorySeparatorChar) + 1);
                string wwwDir = Path.Combine(Path.GetDirectoryName(root) ?? root, "wwwroot");
                string targetDir = Path.Combine(wwwDir, targetDirName);
                CopyDirectory(outputDirectory, targetDir);
                PrintTool.Log($"Deployer CopyTo: {targetDir}");
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
    }

}
