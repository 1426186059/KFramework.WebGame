using KFramework.MonoGame;
using KTexturePacker.Core;
using SkiaSharp;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KFramework.Content.Cli
{

    /// <summary>
    /// 构建单个 AssetBundle：把给定文件夹的直接资源文件打包——纹理进图集，其余原样入包。
    /// 图集页与子图索引都写入同一个包，因此每个包自带其纹理（运行端按 Page 切片）。
    /// 复用外部 KTexturePacker 工具核心（MaxRects 摆放 + 整页合成 + AtlasData 导出）。
    /// </summary>
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
                        // 已切图集的整页图：原样入库为整图纹理，跳过自动装箱（不再重排/重切）
                        SKBitmap skImage = DecodeToRgba(bytes);
                        if (skImage is null)
                        {
                            warnings.Add($"解码纹理失败：{relative}");
                            continue;
                        }
                        (byte[] encoded, ContentTextureDataFormat fmt) = EncodeTexture(skImage, bytes, relative);
                        bundle.Assets.Add(new AssetBundleAsset
                        {
                            Path = name,
                            Type = ContentAssetType.Texture,
                            Bytes = encoded,
                            Width = skImage.Width,
                            Height = skImage.Height,
                            Format = fmt,
                        });
                        skImage.Dispose();
                        textureCount++;
                        continue;
                    }

                    if (Global.supportTextureFileType.Contains(Path.GetExtension(relative)))
                    {
                        // 自动装箱模式：散图交给 AtlasBuilder 从 remainAssetPathList 抽取并装箱，主循环跳过
                        if (autoAtlas)
                            continue;

                        SKBitmap skImage = DecodeToRgba(bytes);
                        if (skImage is null)
                        {
                            warnings.Add($"解码纹理失败：{relative}");
                            continue;
                        }
                        // 不装箱：整图原样入包，按 BuildOptions.TextureFormat 编码（与已切图集整页图同一编码路径）。
                        (byte[] encoded, ContentTextureDataFormat fmt) = EncodeTexture(skImage, bytes, relative);
                        bundle.Assets.Add(new AssetBundleAsset
                        {
                            Path = name,
                            Type = ContentAssetType.Texture,
                            Bytes = encoded,
                            Width = skImage.Width,
                            Height = skImage.Height,
                            Format = fmt,
                        });
                        skImage.Dispose();
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

        /// <summary>把任意图片字节解码并规范化为「Rgba8888」位图。
        /// 关键点：
        /// 1) jpg/bmp 经 <see cref="SKBitmap.Decode"/> 后 AlphaType 常为 Unknown，下游 KTX2 编码会把它当全透明→黑图，这里强制为 Opaque（像素 alpha 写 255）；
        /// 2) 尺寸不做强制 pad：是否适合块压缩（KTX2/S3TC 等，要求宽高 4 的倍数）由 <see cref="IsSuitableForKtx2"/> 在编码阶段判断，
        ///    不适合的纹理会在 <see cref="EncodeTexture"/> 中回退为 Png 等非压缩格式，而不是悄悄把尺寸改大（篡改纹理坐标）。</summary>
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

        /// <summary>取出 Rgba8888 直 alpha 像素字节（整图纹理上传与装箱产物均依赖此格式）。</summary>
        /// <remarks>
        /// <see cref="SKBitmap.Decode"/> 在 Windows 上默认解成 Bgra8888（平台色彩类型），
        /// 直接取字节会把 BGRA 当 RGBA 入库，导致运行端 R/B 通道互换（红砖变蓝）。
        /// 这里统一转换成 Rgba8888 再取字节。
        /// </remarks>
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

        /// <summary>全部块压缩格式（S3TC/DXT、ASTC、BC7、ETC2）均为 4×4 块，要求宽高均为 4 的倍数；
        /// 否则运行端无论转码成哪种压缩格式，都会因「基级尺寸非 4 倍数」而 glCompressedTexImage2D 失败（黑图）。
        /// 因此 <see cref="ContentTextureSwitchTarget.Ktx2"/> 仅对 <paramref name="width"/>/<paramref name="height"/> 都是 4 倍数的纹理适用。</summary>
        internal static bool IsSuitableForKtx2(int width, int height)
            => (width & 3) == 0 && (height & 3) == 0;

        /// <summary>按 BuildOptions.TextureFormat 把整图纹理（来自磁盘原图）编码为目标格式字节 + 实际数据格式标记（整图纹理与已切图集整页图共用）。
        /// <list type="bullet">
        ///   <item><see cref="ContentTextureSwitchTarget.None"/>：不转码，按源图自身的 <see cref="ContentTextureDataFormat"/> 原样保留（png/webp/jpg… 直接存源文件字节）。</item>
        ///   <item><see cref="ContentTextureSwitchTarget.Ktx2"/>：宽高皆 4 倍数 → 编码为 KTX2；否则回退为 Webp。</item>
        ///   <item><see cref="ContentTextureSwitchTarget.Rgba"/> / <see cref="ContentTextureSwitchTarget.Webp"/>：直接编码为对应 <see cref="ContentTextureDataFormat"/>，无尺寸限制。</item>
        /// </list></summary>
        private static (byte[] Bytes, ContentTextureDataFormat Format) EncodeTexture(
            SKBitmap skImage, byte[] originalBytes, string relative)
        {
            if (BuildConfigResult.TextureSwitchTarget == ContentTextureSwitchTarget.None)
            {
                ContentTextureDataFormat cpu = ContentTextureDataFormatHelper.FromExtension(relative);
                (ContentTextureDataFormat stored, bool keepBytes) = cpu.ToStored();
                PrintTool.Log($"[kfc] 纹理格式=None（原图处理） {relative} {skImage.Width}x{skImage.Height}：源图格式 {cpu} → 入库 {stored}");
                byte[] outBytes = keepBytes ? originalBytes : GetPixels(skImage);
                return (outBytes, stored);
            }

            if (BuildConfigResult.TextureSwitchTarget != ContentTextureSwitchTarget.Ktx2)
            {
                ContentTextureDataFormat fmt = BuildConfigResult.TextureSwitchTarget switch
                {
                    ContentTextureSwitchTarget.Rgba => ContentTextureDataFormat.Rgba,
                    ContentTextureSwitchTarget.Webp => ContentTextureDataFormat.Webp,
                    _ => ContentTextureDataFormat.Rgba,
                };
                byte[] bytes = fmt switch
                {
                    ContentTextureDataFormat.Rgba => GetPixels(skImage),
                    ContentTextureDataFormat.Webp => EncodeWebp(skImage),
                    ContentTextureDataFormat.Png  => EncodePng(skImage),
                    _ => GetPixels(skImage),
                };
                return (bytes, fmt);
            }

            if (IsSuitableForKtx2(skImage.Width, skImage.Height))
            {
                PrintTool.Log($"[kfc] KTX2 适配检查 {relative} {skImage.Width}x{skImage.Height}：适合（宽高均为 4 倍数），编码为 KTX2");
                return (EncodeKtx2(skImage, BuildConfigResult.BasisuPathFull, BuildOptions.Ktx2Quality), ContentTextureDataFormat.Ktx2);
            }

            PrintTool.Log($"[kfc] KTX2 适配检查 {relative} {skImage.Width}x{skImage.Height}：不适合（宽高非 4 倍数），回退为 Webp");
            return (EncodeWebp(skImage), ContentTextureDataFormat.Webp);
        }

        /// <summary>把 SKBitmap 编码为 PNG 字节（整图非装箱模式下 TextureFormat=Png 时使用）。</summary>
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

        /// <summary>把 RGBA8 字节（行优先 W*H*4）编码为 PNG（供图集页回退复用，因上游 KTexturePacker 不提供 ToPng）。</summary>
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



        /// <summary>把 RGBA8 字节（行优先 W*H*4）编码为 KTX2（Basis Universal 超压缩）。</summary>
        /// <remarks>
        /// 经临时 PNG 调用 <c>basisu</c> 命令行编码为 KTX2（UASTC）：<c>basisu -file x.png -ktx2 -uastc -uastc_level &lt;q&gt; -mipmap -output_file y.ktx2</c>。需预先安装 Basis Universal 工具
        /// （https://github.com/BinomialLLC/basis_universal），并配置 <paramref name="basisuPath"/>（为空则用 PATH 中的 basisu）。
        /// 编码失败时抛出明确异常，提示安装/配置 basisu。
        /// </remarks>
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

        /// <summary>把 SKBitmap 编码为 KTX2（Basis Universal 超压缩），借外部 <c>basisu</c> 命令行完成。</summary>
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

        /// <summary>文件路径 → 资源名：保留原始扩展名（如 .png/.json/.txt），仅小写化、统一用 / 分隔。
        /// 保留扩展名可让资源名携带更多类型信息，配合模糊/精确查找更易区分同名不同型的资源。</summary>
        private static string AssetNameOf(string relativePath)
            => AssetName.Normalize(relativePath);

        /// <summary>按扩展名把资源归类为 Audio / Video / Text 之一（仅控制包内 Type 元数据，不影响实际字节）。</summary>
        private static ContentAssetType AssetTypeOf(string relative)
        {
            string ext = Path.GetExtension(relative);
            if (Global.supportAudioFileType.Contains(ext)) return ContentAssetType.Audio;
            if (Global.supportVideoFileType.Contains(ext)) return ContentAssetType.Video;
            return ContentAssetType.Text;
        }
    }

}
