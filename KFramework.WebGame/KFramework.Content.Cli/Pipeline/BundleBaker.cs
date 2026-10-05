using KFramework.MonoGame;
using KTexturePacker.Core;
using SkiaSharp;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KFramework.Content.Cli
{

    public static class BundleBaker
    {
        public static AssetBundleBuild BuildBundle(
            string bundleName,
            string fullDir,
            string[] files,
            string assetBaseDir,
            bool autoAtlas,
            List<string> warnings,
            ref long rawBytes,
            ref int textureCount,
            ref int dataCount,
            ref int atlasPageCount)
        {
            //剩余可用的文件资源列表（BuildAtlas 会从中抽出纹理并移除，避免主流程重复导入）
            List<string> remainAssetPathList = new List<string>(files);

            AssetBundleBuild bundle = new AssetBundleBuild { AssetBundleName = bundleName, FullDir = fullDir };
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> atlasPageRelatives = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string atlasFile in files)
            {
                string atlasRel = Path.GetRelativePath(assetBaseDir, atlasFile).Replace('\\', '/');
                if (!AtlasFile.IsAtlas(atlasRel)) continue;
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllBytes(atlasFile));
                    if (doc.RootElement.TryGetProperty("pages", out JsonElement pagesEl))
                    {
                        foreach (JsonElement page in pagesEl.EnumerateArray())
                        {
                            string image = page.GetProperty("image").GetString()!;
                            string pagePath = Path.Combine(Path.GetDirectoryName(atlasFile)!, image);
                            atlasPageRelatives.Add(Path.GetRelativePath(assetBaseDir, pagePath).Replace('\\', '/'));
                        }
                    }
                }
                catch (Exception ex)
                {
                    warnings.Add($"解析图集失败 {atlasRel}：{ex.Message}");
                }
            }

            foreach (string file in files)
            {
                string relative = Path.GetRelativePath(assetBaseDir, file).Replace('\\', '/');
                if (IsIgnored(relative)) continue;

                string name = AssetNameOf(relative);
                if (!names.Add(name))
                {
                    warnings.Add($"资源名重复，已跳过：{relative}");
                    continue;
                }

                byte[] bytes = File.ReadAllBytes(file);
                rawBytes += bytes.Length;

                try
                {
                    if (AtlasFile.IsAtlas(relative))
                    {
                        // 已切好的图集描述文件：原样入库，运行端按 .atlas 直接加载，不再自动打包
                        bundle.Assets.Add(new AssetBundleAsset
                        {
                            Path = name,
                            Type = ContentAssetType.Text,
                            Bytes = bytes,
                        });
                        dataCount++;
                        continue;
                    }

                    if (atlasPageRelatives.Contains(relative))
                    {
                        (int w, int h) = GetImageDimensions(bytes);
                        AssetBundleAsset asset = EncodeRgbaToTarget(w, h, bytes, ContentTextureDataFormatHelper.FromExtension(relative));
                        asset.Path = name;
                        bundle.Assets.Add(asset);
                        textureCount++;
                        continue;
                    }

                    if (Global.supportTextureFileType.Contains(Path.GetExtension(relative)))
                    {
                        if (autoAtlas)
                            continue;

                        (int w, int h) = GetImageDimensions(bytes);
                        AssetBundleAsset asset = EncodeRgbaToTarget(w, h, bytes, ContentTextureDataFormatHelper.FromExtension(relative));
                        asset.Path = name;
                        bundle.Assets.Add(asset);
                        textureCount++;
                    }
                    else
                    {
                        // 文本 / JSON / 音效 / 视频 / 任意字节 —— 按扩展名归类为 Audio / Video / Text，原样作为 Bundle 内资源
                        bundle.Assets.Add(new AssetBundleAsset
                        {
                            Path = name,
                            Type = AssetTypeOf(relative),
                            Bytes = bytes,
                        });
                        dataCount++;
                    }
                }
                catch (Exception ex)
                {
                    warnings.Add($"导入失败 {relative}：{ex.Message}");
                }
            }

            // 自动装箱模式：把 remainAssetPathList 中剩余的散图交给 AtlasBuilder 抽取并装箱；
            // 已切好的 .atlas 图集不参与此处打包（前述已在主流程原样入库）。
            if (autoAtlas)
            {
                AtlasBuilder.BuildAtlas(bundle, remainAssetPathList);
            }

            return bundle;
        }

        private static SKBitmap DecodeToRgba(byte[] bytes)
        {
            SKBitmap src = SKBitmap.Decode(bytes);
            if (src is null) return null;
            // 不透明/Unknown 源强制为 Opaque；真正带透明通道（Premul/Straight）的图则保留透明度
            bool forceOpaque = (src.AlphaType == SKAlphaType.Opaque || src.AlphaType == SKAlphaType.Unknown);
            SKBitmap dst = src.Copy(SKColorType.Rgba8888);
            src.Dispose();
            if (dst is null)
                throw new InvalidOperationException("纹理解码/格式转换失败：无法复制到 Rgba8888");
            if (forceOpaque)
            {
                using var pix = dst.PeekPixels();
                var span = pix.GetPixelSpan();
                for (int i = 3; i < span.Length; i += 4) span[i] = 255;
            }
            return dst;
        }

        private static byte[] GetPixels(SKBitmap bmp)
        {
            if (bmp.ColorType != SKColorType.Rgba8888)
            {
                using SKBitmap converted = bmp.Copy(SKColorType.Rgba8888)
                    ?? throw new InvalidOperationException($"纹理色彩类型转换失败：{bmp.ColorType} → Rgba8888");
                using var convertedPixmap = converted.PeekPixels();
                return convertedPixmap.GetPixelSpan().ToArray();
            }

            using var pixmap = bmp.PeekPixels();
            return pixmap.GetPixelSpan().ToArray();
        }

        internal static bool IsSuitableForKtx2(int width, int height)
        {
            return (width & 3) == 0 && (height & 3) == 0;
        }

        private static (int Width, int Height) GetImageDimensions(byte[] bytes)
        {
            using var codec = SKCodec.Create(new MemoryStream(bytes));
            if (codec is null) return (0, 0);
            return (codec.Info.Width, codec.Info.Height);
        }

        public static AssetBundleAsset EncodeRgbaToTarget(
            int width,
            int height,
            byte[] originalPixels,
            ContentTextureDataFormat oriFormat)
        {
            ContentTextureDataFormat target = oriFormat;
            if (BuildConfigResult.TextureSwitchTarget == ContentTextureSwitchTarget.Webp) { target = ContentTextureDataFormat.Webp; }
            else if (BuildConfigResult.TextureSwitchTarget == ContentTextureSwitchTarget.Rgba) { target = ContentTextureDataFormat.Rgba; }
            else if (BuildConfigResult.TextureSwitchTarget == ContentTextureSwitchTarget.Ktx2) { target = ContentTextureDataFormat.Ktx2; }

            if (target == oriFormat)
            {
                return new AssetBundleAsset { Width = width, Height = height, Bytes = originalPixels, Format = oriFormat, Type = ContentAssetType.Texture };
            }

            byte[] rgba;
            if (oriFormat == ContentTextureDataFormat.Rgba) { rgba = originalPixels; }
            else
            {
                SKBitmap bmp = DecodeToRgba(originalPixels);
                if (bmp is null)
                {
                    return new AssetBundleAsset { Width = width, Height = height, Bytes = originalPixels, Format = oriFormat, Type = ContentAssetType.Texture };
                }
                rgba = GetPixels(bmp);
                bmp.Dispose();
            }

            if (target == ContentTextureDataFormat.Webp)
            {
                return new AssetBundleAsset { Width = width, Height = height, Bytes = EncodeWebpFromRgba(rgba, width, height), Format = target, Type = ContentAssetType.Texture };
            }
            if (target == ContentTextureDataFormat.Png)
            {
                return new AssetBundleAsset { Width = width, Height = height, Bytes = EncodePngFromRgba(rgba, width, height), Format = target, Type = ContentAssetType.Texture };
            }
            if (target == ContentTextureDataFormat.Ktx2)
            {
                if (!IsSuitableForKtx2(width, height))
                {
                    return new AssetBundleAsset { Width = width, Height = height, Bytes = originalPixels, Format = oriFormat, Type = ContentAssetType.Texture };
                }
                return new AssetBundleAsset { Width = width, Height = height, Bytes = EncodeKtx2FromRgba(rgba, width, height, BuildConfigResult.BasisuPathFull, BuildOptions.Ktx2Quality), Format = target, Type = ContentAssetType.Texture };
            }

            return new AssetBundleAsset { Width = width, Height = height, Bytes = rgba, Format = target, Type = ContentAssetType.Texture };
        }

        private static byte[] EncodePng(SKBitmap bmp)
        {
            using var img = SKImage.FromBitmap(bmp);
            using var data = img.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }

        private static byte[] EncodeWebp(SKBitmap bmp)
        {
            using var img = SKImage.FromBitmap(bmp);
            using var data = img.Encode(SKEncodedImageFormat.Webp, 90);
            return data.ToArray();
        }

        internal static byte[] EncodeWebpFromRgba(byte[] rgba, int width, int height)
        {
            var bmp = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            try
            {
                using var pixmap = bmp.PeekPixels();
                Marshal.Copy(rgba, 0, pixmap.GetPixels(), rgba.Length);
                return EncodeWebp(bmp);
            }
            finally
            {
                bmp.Dispose();
            }
        }

        internal static byte[] EncodePngFromRgba(byte[] rgba, int width, int height)
        {
            var bmp = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            try
            {
                using var pixmap = bmp.PeekPixels();
                Marshal.Copy(rgba, 0, pixmap.GetPixels(), rgba.Length);
                return EncodePng(bmp);
            }
            finally
            {
                bmp.Dispose();
            }
        }



        internal static byte[] EncodeKtx2FromRgba(byte[] rgba, int width, int height, string? basisuPath, int quality)
        {
            var bmp = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            try
            {
                using var pixmap = bmp.PeekPixels();
                Marshal.Copy(rgba, 0, pixmap.GetPixels(), rgba.Length);
                return EncodeKtx2(bmp, basisuPath, quality);
            }
            finally
            {
                bmp.Dispose();
            }
        }

        internal static byte[] EncodeKtx2(SKBitmap bmp, string? basisuPath, int quality)
        {
            string tmpPng = Path.Combine(Path.GetTempPath(), $"kf_{Guid.NewGuid():N}.png");
            string outKtx = Path.Combine(Path.GetTempPath(), $"kf_{Guid.NewGuid():N}.ktx2");
            try
            {
                using (var img = SKImage.FromBitmap(bmp))
                using (var data = img.Encode(SKEncodedImageFormat.Png, 100))
                    File.WriteAllBytes(tmpPng, data.ToArray());

                string exe = string.IsNullOrWhiteSpace(basisuPath) ? "basisu" : basisuPath;
                var psi = new ProcessStartInfo(exe,
                    $"-file \"{tmpPng}\" -ktx2 -uastc -uastc_level {quality} -mipmap -output_file \"{outKtx}\"")
                {
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                Process proc;
                try
                {
                    proc = Process.Start(psi)
                           ?? throw new InvalidOperationException($"无法启动 basisu（{exe}）。");
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"无法启动 basisu（{exe}）。请安装 Basis Universal 命令行工具并配置 BuildConfigResult.BasisuPathFull。原因：{ex.Message}");
                }

                string err = proc.StandardError.ReadToEnd();
                proc.WaitForExit();
                if (proc.ExitCode != 0)
                    throw new InvalidOperationException($"basisu 编码失败（退出码 {proc.ExitCode}）：{err}");
                return File.ReadAllBytes(outKtx);
            }
            finally
            {
                if (File.Exists(tmpPng)) File.Delete(tmpPng);
                if (File.Exists(outKtx)) File.Delete(outKtx);
            }
        }

        internal static bool IsIgnored(string relativePath)
        {
            if (relativePath.Length == 0) return true;

            foreach (string segment in relativePath.Split('/'))
            {
                if (segment.Length == 0) continue;
                if (segment[0] == '.') return true;
                if (string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(segment, "content", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(segment, "build.config.json", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static string AssetNameOf(string relativePath)
        {
            return AssetName.Normalize(relativePath);
        }

        private static ContentAssetType AssetTypeOf(string relative)
        {
            string ext = Path.GetExtension(relative);
            if (Global.supportAudioFileType.Contains(ext)) return ContentAssetType.Audio;
            if (Global.supportVideoFileType.Contains(ext)) return ContentAssetType.Video;
            return ContentAssetType.Text;
        }
    }

}
