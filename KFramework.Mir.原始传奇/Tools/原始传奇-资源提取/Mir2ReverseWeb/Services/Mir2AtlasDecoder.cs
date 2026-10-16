using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Mir2ReverseWeb.Models;

namespace Mir2ReverseWeb.Services;

/// <summary>
/// 图集（WIL/WIX 资源库）解析模块：纯逻辑，与 ASP/HTTP 解耦，便于维护与扩展。
/// 格式逆向自原版客户端 Libraries.Wil：
///   WIL：40 字节 ASCII 标题 + int32 版本 + int32 图像数；其后每个图像块为
///        「宽/高/OffsetX/OffsetY（各 int32，共 16 字节）+ 像素数据」。
///   WIX：int32 图像数 + N×int32 偏移；偏移指向 WIL 中对应图像块的起始位置。
/// 像素解码（调色板/RLE）为后续扩展点，本模块只解析结构（尺寸/偏移）。
/// </summary>
public static class Mir2AtlasDecoder
{
    private static readonly HashSet<string> _atlasExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wil", ".lib", ".wix", ".wzl", ".wzx"
    };

    /// <summary>扫描某目录下（含子目录）所有图集库文件（WIL/WIX 等）。</summary>
    public static List<AtlasFile> Scan(string root)
    {
        var files = new List<AtlasFile>();
        if (!Directory.Exists(root)) return files;
        foreach (var f in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(f);
            if (!_atlasExts.Contains(ext)) continue;
            var fi = new FileInfo(f);
            files.Add(new AtlasFile
            {
                Name = Path.GetFileName(f),
                Path = f,
                Kind = ext.TrimStart('.').ToLowerInvariant(),
                Size = fi.Length
            });
        }
        files.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return files;
    }

    /// <summary>解析单个 WIL 库（配合同名 WIX 索引）的结构：标题/版本/图像数/每图尺寸与偏移。</summary>
    public static AtlasInfo ReadAtlas(string wilPath)
    {
        var info = new AtlasInfo();
        try
        {
            if (!File.Exists(wilPath)) { info.Error = "文件不存在"; return info; }
            var dir = Path.GetDirectoryName(wilPath) ?? "";
            var name = Path.GetFileNameWithoutExtension(wilPath);
            var wixPath = Path.Combine(dir, name + ".wix");
            byte[] wil = File.ReadAllBytes(wilPath);

            if (wil.Length >= 44)
            {
                int tl = 0; while (tl < 40 && wil[tl] != 0) tl++;
                info.Title = tl > 0 ? Encoding.ASCII.GetString(wil, 0, tl) : "(无标题)";
                info.Version = BitConverter.ToInt32(wil, 40);
                info.ImageCount = BitConverter.ToInt32(wil, 44);
            }

            var offsets = new List<int>();
            if (File.Exists(wixPath))
            {
                byte[] wix = File.ReadAllBytes(wixPath);
                int n = BitConverter.ToInt32(wix, 0);
                int start = 4;
                if (n <= 0 || n * 4 + 4 > wix.Length) { n = wix.Length / 4; start = 0; }
                for (int i = 0; i < n; i++)
                {
                    int off = BitConverter.ToInt32(wix, start + i * 4);
                    if (off > 0 && off + 16 <= wil.Length) offsets.Add(off);
                }
            }

            foreach (var off in offsets)
            {
                int w = BitConverter.ToInt32(wil, off);
                int h = BitConverter.ToInt32(wil, off + 4);
                int ox = BitConverter.ToInt32(wil, off + 8);
                int oy = BitConverter.ToInt32(wil, off + 12);
                if (w <= 0 || w > 4096 || h <= 0 || h > 4096) continue;
                info.Images.Add(new AtlasImage
                {
                    Index = info.Images.Count,
                    Width = w,
                    Height = h,
                    OffsetX = ox,
                    OffsetY = oy,
                    HasData = true
                });
            }
            info.ParsedImages = info.Images.Count;
            info.Ok = true;
        }
        catch (Exception ex) { info.Error = ex.Message; }
        return info;
    }
}
