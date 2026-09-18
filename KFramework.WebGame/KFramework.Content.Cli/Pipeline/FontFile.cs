namespace KFramework.Content.Build;

/// <summary>
/// 美术字（BMFont）描述文件：以 <c>.fnt</c> 结尾，由美术工具（BMFont / Hiero 等）导出，
/// 描述每个字形在图集页上的矩形、偏移与前进量。
///
/// 与已切好的 <c>.atlas</c> 图集同理：它引用的图集页是<b>已经排好版</b>的整页图，
/// 一旦被自动装箱重排，字形坐标就全部失效，因此打包时需要把它的 page 页识别为「已切图集页」原样入包。
/// </summary>
public static class FontFile
{
    private const string FontPageLinePrefix = "page ";

    /// <summary>判断给定路径是否为 BMFont 描述文件。</summary>
    public static bool IsFont(string path)
        => path.EndsWith(".fnt", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 取 .fnt 引用的全部图集页文件名（<c>page id=0 file="xxx.png"</c>）。
    /// 文本与 XML 两种导出行都覆盖；解析失败时返回空列表（由调用方按警告处理）。
    /// </summary>
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
