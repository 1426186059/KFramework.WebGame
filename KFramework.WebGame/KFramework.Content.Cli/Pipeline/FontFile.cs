namespace KFramework.Content.Cli
{

    public static class FontFile
    {
        private const string FontPageLinePrefix = "page ";

        public static bool IsFont(string path)
        {
            return path.EndsWith(".fnt", StringComparison.OrdinalIgnoreCase);
        }

        public static List<string> PageImages(string fntPath)
        {
            var files = new List<string>();
            foreach (string rawLine in File.ReadAllLines(fntPath))
            {
                string line = rawLine.Trim();
                if (!line.StartsWith(FontPageLinePrefix, StringComparison.OrdinalIgnoreCase)) continue;

                int fileAt = line.IndexOf("file=", StringComparison.OrdinalIgnoreCase);
                if (fileAt < 0) continue;

                int valueAt = fileAt + "file=".Length;
                if (valueAt >= line.Length) continue;

                // 两种导出格式：文本是 file="xxx.png"，XML 是 file="xxx.png" />，统一取引号内内容
                int first = line.IndexOf('"', valueAt);
                if (first < 0)
                {
                    files.Add(line.Substring(valueAt).Trim().TrimEnd('/', '>'));
                    continue;
                }

                int last = line.IndexOf('"', first + 1);
                if (last > first) files.Add(line.Substring(first + 1, last - first - 1));
            }
            return files;
        }
    }

}
