// TexturePacker JSON(hash) 图集导入器：把 *.atlas.json + 图集页位图拆成单张散图，
// 输出到内容管线的 raw/ 目录，由 ContentBuilder 重新装箱。
//
//   用法：AtlasImporter <图集目录> <输出目录>
//
// 同时导出 animations 段为 <图集名>.anim.json，供运行时做逐帧动画。
using System.Text;
using System.Text.Json;
using KFramework.MonoGame;

namespace KFramework.Example2.Tools;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("用法：AtlasImporter <图集目录> <输出目录>");
            return 1;
        }

        string atlasDirectory = Path.GetFullPath(args[0]);
        string outputDirectory = Path.GetFullPath(args[1]);

        if (!Directory.Exists(atlasDirectory))
        {
            Console.Error.WriteLine($"图集目录不存在：{atlasDirectory}");
            return 1;
        }

        Directory.CreateDirectory(outputDirectory);

        int totalFrames = 0;
        foreach (string jsonPath in Directory.EnumerateFiles(atlasDirectory, "*.atlas.json").OrderBy(static p => p, StringComparer.Ordinal))
        {
            totalFrames += ImportAtlas(jsonPath, outputDirectory);
        }

        PrintTool.Log($"完成：共导出 {totalFrames} 张精灵到 {outputDirectory}");
        return 0;
    }

    private static int ImportAtlas(string jsonPath, string outputDirectory)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(jsonPath));

        if (!document.RootElement.TryGetProperty("frames", out JsonElement frames))
        {
            Console.Error.WriteLine($"[跳过] 没有 frames 段：{jsonPath}");
            return 0;
        }

        string imageName = document.RootElement.GetProperty("meta").GetProperty("image").GetString()!;
        string pagePath = Path.Combine(Path.GetDirectoryName(jsonPath)!, imageName);
        if (!File.Exists(pagePath))
        {
            Console.Error.WriteLine($"[跳过] 找不到图集页位图：{pagePath}");
            return 0;
        }

        Bitmap page = PngDecoder.Decode(File.ReadAllBytes(pagePath));
        string atlasName = Path.GetFileName(jsonPath).Replace(".atlas.json", string.Empty, StringComparison.OrdinalIgnoreCase);

        int count = 0;
        foreach (JsonProperty frameProperty in frames.EnumerateObject())
        {
            Bitmap sprite = ExtractFrame(frameProperty.Value, page);
            File.WriteAllBytes(Path.Combine(outputDirectory, $"{frameProperty.Name}.png"), PngEncoder.Encode(sprite));
            count++;
        }

        ExportAnimations(document.RootElement, atlasName, outputDirectory);
        PrintTool.Log($"{atlasName}: {count} 帧（页 {page.Width}x{page.Height}）");
        return count;
    }

    /// <summary>按 frame 矩形从图集页里切一块出来；rotated 的帧按逆时针旋转 90° 还原。</summary>
    private static Bitmap ExtractFrame(JsonElement frameEntry, Bitmap page)
    {
        JsonElement rect = frameEntry.GetProperty("frame");
        int x = rect.GetProperty("x").GetInt32();
        int y = rect.GetProperty("y").GetInt32();
        int w = rect.GetProperty("w").GetInt32();
        int h = rect.GetProperty("h").GetInt32();

        bool rotated = frameEntry.TryGetProperty("rotated", out JsonElement rotatedElement) && rotatedElement.GetBoolean();
        if (!rotated) return CopyRegion(page, x, y, w, h);

        // 旋转过的帧在图上占位是 h 宽 w 高，取出来后要逆时针转回来。
        Bitmap rotatedRegion = CopyRegion(page, x, y, h, w);
        Bitmap result = new(w, h);
        for (int sy = 0; sy < w; sy++)
        {
            for (int sx = 0; sx < h; sx++)
            {
                // 逆时针 90°：(sx, sy) -> (sy, h - 1 - sx)
                result.SetPixel(sy, h - 1 - sx, rotatedRegion.GetPixel(sx, sy));
            }
        }
        return result;
    }

    private static Bitmap CopyRegion(Bitmap source, int x, int y, int width, int height)
    {
        Bitmap region = new(width, height);
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                int sx = x + column;
                int sy = y + row;
                if (sx < 0 || sy < 0 || sx >= source.Width || sy >= source.Height) continue;
                region.SetPixel(column, row, source.GetPixel(sx, sy));
            }
        }
        return region;
    }

    /// <summary>导出 animations 段。原始顺序写回，运行时按名字取帧序列。</summary>
    private static void ExportAnimations(JsonElement root, string atlasName, string outputDirectory)
    {
        if (!root.TryGetProperty("animations", out JsonElement animations) || animations.ValueKind != JsonValueKind.Object)
            return;

        var builder = new StringBuilder();
        builder.Append('{');
        bool first = true;
        foreach (JsonProperty animation in animations.EnumerateObject())
        {
            if (!first) builder.Append(',');
            first = false;
            builder.Append(JsonSerializer.Serialize(animation.Name)).Append(':').Append('[');
            bool firstFrame = true;
            foreach (JsonElement frame in animation.Value.EnumerateArray())
            {
                if (!firstFrame) builder.Append(',');
                firstFrame = false;
                builder.Append(JsonSerializer.Serialize(frame.GetString()));
            }
            builder.Append(']');
        }
        builder.Append('}');

        File.WriteAllText(Path.Combine(outputDirectory, $"{atlasName}.anim.json"), builder.ToString());
    }
}
