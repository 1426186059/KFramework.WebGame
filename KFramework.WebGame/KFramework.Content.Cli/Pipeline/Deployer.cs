namespace KFramework.Content.Cli
{
    public static class Deployer
    {
        public static void Deploy(bool copyToWwwroot, string root, string outputDirectory, List<string> warnings)
        {
            if (copyToWwwroot)
            {
                string targetDirName = outputDirectory.Substring(outputDirectory.LastIndexOf(Path.DirectorySeparatorChar) + 1);
                string wwwDir = Path.Combine(Path.GetDirectoryName(root) ?? root, "wwwroot");
                string targetDir = Path.Combine(wwwDir, targetDirName);
                CopyDirectory(outputDirectory, targetDir);
            }
        }

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
