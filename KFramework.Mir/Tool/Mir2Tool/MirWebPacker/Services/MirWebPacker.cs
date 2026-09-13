using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Mir.Lib;
using MirWebPacker.Models;
using SkiaSharp;

namespace MirWebPacker.Services;

/// <summary>
/// 把一个地图（或任意资源目录）打包成 .web.lib（实为 ZIP）。
/// 包内布局：manifest.json + data/（txt/json）+ textures/（webp）+ audio/（wav/mp3）。
/// 浏览器端先读 manifest.json，再按 entry.Path 懒加载对应资源。
/// </summary>
public sealed class MirWebPacker
{
    private readonly Action<string> _log;

    public MirWebPacker(Action<string>? log = null)
    {
        _log = log ?? Console.WriteLine;
    }

    // 扩展名 -> (分类目录, MIME)。png 会被转码为 webp。
    private static readonly Dictionary<string, (string Category, string Mime)> s_extMap = new(StringComparer.OrdinalIgnoreCase)
    {
        [".txt"]  = ("data", "text/plain"),
        [".json"] = ("data", "application/json"),
        [".webp"] = ("textures", "image/webp"),
        [".png"]  = ("textures", "image/webp"),
        [".wav"]  = ("audio", "audio/wav"),
        [".mp3"]  = ("audio", "audio/mpeg"),
    };

    // 已知分类目录：若输入文件已位于这些目录下，则不再叠加前缀，避免 audio/audio/... 这类重复。
    private static readonly HashSet<string> s_knownCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "data", "textures", "audio",
    };

    private static readonly JsonSerializerOptions s_jsonOpts = new() { WriteIndented = true };

    /// <summary>
    /// 打包 inputDir 到 outputFile（.web.lib）。返回 manifest 供调用方打印/校验。
    /// </summary>
    public WebLibManifest Pack(string inputDir, string outputFile, string name, bool lossless = true, int quality = 90)
    {
        if (!Directory.Exists(inputDir))
            throw new DirectoryNotFoundException($"输入目录不存在: {inputDir}");

        // 1) 先扫描并转码，全部放进内存（POC 规模足够小）
        //    按相对路径排序，保证相同内容产出完全一致的 zip（跨进程/跨次运行哈希稳定，利于热更）
        var planned = new List<(string EntryPath, byte[] Bytes, string Mime)>();
        foreach (var file in Directory.EnumerateFiles(inputDir, "*", SearchOption.AllDirectories)
                     .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var ext = Path.GetExtension(file);
            if (!s_extMap.TryGetValue(ext, out var info))
            {
                _log($"[跳过] 不支持的类型: {Path.GetRelativePath(inputDir, file)}");
                continue;
            }

            var rel = Path.GetRelativePath(inputDir, file).Replace('\\', '/');
            var firstSeg = rel.IndexOf('/') >= 0 ? rel[..rel.IndexOf('/')] : string.Empty;
            bool alreadyCategorized = s_knownCategories.Contains(firstSeg);

            byte[] bytes;
            string entryPath;

            if (ext.Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                // png -> 无损 webp 转码，复用 MirLib 已验证的编码路径
                using var bmp = SKBitmap.Decode(file)
                    ?? throw new InvalidDataException($"无法解码 PNG: {file}");
                bytes = SkiaBitmaps.EncodeWebPBytes(bmp, quality, lossless);
                var relWebp = Path.ChangeExtension(rel, ".webp");
                entryPath = alreadyCategorized ? relWebp : info.Category + "/" + relWebp;
            }
            else
            {
                bytes = File.ReadAllBytes(file);
                entryPath = alreadyCategorized ? rel : info.Category + "/" + rel;
            }

            planned.Add((entryPath, bytes, info.Mime));
        }

        // 2) 按规划生成 manifest（含 crc / hash）；按 EntryPath 排序保证 manifest 稳定
        planned.Sort((a, b) => string.Compare(a.EntryPath, b.EntryPath, StringComparison.OrdinalIgnoreCase));
        var entries = planned
            .Select(p => new WebLibEntry(
                p.EntryPath,
                p.Mime,
                p.Bytes.LongLength,
                Crc32.Hash(p.Bytes).ToString("x8"),
                "sha1:" + SHA1.HashData(p.Bytes).ToHex()))
            .ToList();
        var manifest = new WebLibManifest("web.lib", 1, "map", name, entries);

        // 3) 写 ZIP：manifest 作为首个 entry，便于调试时直接定位
        string? outDir = Path.GetDirectoryName(outputFile);
        if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);
        if (File.Exists(outputFile)) File.Delete(outputFile); // 允许覆盖

        using (var zip = ZipFile.Open(outputFile, ZipArchiveMode.Create))
        {
            WriteEntry(zip, "manifest.json", JsonSerializer.SerializeToUtf8Bytes(manifest, s_jsonOpts));
            foreach (var p in planned)
                WriteEntry(zip, p.EntryPath, p.Bytes);
        }

        return manifest;
    }

    private static void WriteEntry(ZipArchive zip, string path, byte[] bytes)
    {
        var entry = zip.CreateEntry(path);
        // 固定时间戳，保证相同内容产出逐字节一致的 zip（跨进程/跨次运行哈希稳定，利于热更）
        entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using var s = entry.Open();
        s.Write(bytes, 0, bytes.Length);
    }
}

file static class HexExtensions
{
    public static string ToHex(this byte[] b) => Convert.ToHexString(b).ToLowerInvariant();
}

// 标准 ZIP CRC32（多项式 0xEDB88320），避免额外依赖 System.IO.Hashing。
file static class Crc32
{
    private static readonly uint[] s_table = BuildTable();

    private static uint[] BuildTable()
    {
        var t = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            t[i] = c;
        }
        return t;
    }

    public static uint Hash(byte[] bytes)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in bytes)
            crc = s_table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFF;
    }
}
