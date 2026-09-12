using System.Runtime.InteropServices;
using SkiaSharp;

namespace Mir.Lib
{
    /// <summary>
    /// 基于 SkiaSharp 的位图工具（替代 System.Drawing，跨平台且原生支持 WebP）。
    /// 注意：Mir 的 .Lib 像素是 BGRA 直排（与 GDI+ 32bppArgb 内存布局一致），
    /// 因此统一用 SKColorType.Bgra8888，读写都不需要额外转换。
    /// </summary>
    public static class SkiaBitmaps
    {
        /// <summary>画布类位图（用于绘制/混合）：预乘 Alpha</summary>
        public static SKBitmap Create(int width, int height)
            => new SKBitmap(new SKImageInfo(Math.Max(1, width), Math.Max(1, height),
                                            SKColorType.Bgra8888, SKAlphaType.Premul));

        public static SKBitmap CreateEmpty()
            => Create(1, 1);

        /// <summary>把 BGRA 直排字节直接铺成位图（不预乘，保持原始像素）</summary>
        public static SKBitmap FromBgra(byte[] bgra, int width, int height)
        {
            var bmp = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul));
            int need = width * height * 4;
            Marshal.Copy(bgra, 0, bmp.GetPixels(), Math.Min(bgra.Length, need));
            return bmp;
        }

        /// <summary>取 BGRA 直排像素（会先归一化到 Unpremul 的 BGRA8888）</summary>
        public static byte[] ToBgra(SKBitmap input)
        {
            var info = new SKImageInfo(input.Width, input.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            using var bmp = new SKBitmap(info);
            using (var canvas = new SKCanvas(bmp))
            {
                canvas.Clear(SKColors.Transparent);
                // 1:1 拷贝，用 Nearest 避免任何重采样
                canvas.DrawBitmap(input, SKPoint.Empty, new SKSamplingOptions(SKFilterMode.Nearest));
            }

            byte[] pixels = new byte[info.Width * info.Height * 4];
            Marshal.Copy(bmp.GetPixels(), pixels, 0, pixels.Length);
            return pixels;
        }

        /// <summary>按扩展名推断编码格式</summary>
        public static SKEncodedImageFormat FormatFromExtension(string path)
        {
            string ext = Path.GetExtension(path);
            if (string.IsNullOrEmpty(ext)) return SKEncodedImageFormat.Png;

            return ext.ToLowerInvariant() switch
            {
                ".webp" => SKEncodedImageFormat.Webp,
                ".jpg" or ".jpeg" => SKEncodedImageFormat.Jpeg,
                ".gif" => SKEncodedImageFormat.Gif,
                ".bmp" => SKEncodedImageFormat.Bmp,
                ".ico" => SKEncodedImageFormat.Ico,
                _ => SKEncodedImageFormat.Png,
            };
        }

        /// <summary>编码为指定格式的字节</summary>
        public static byte[] Encode(SKBitmap bmp, SKEncodedImageFormat format, int quality)
        {
            using var data = bmp.Encode(format, quality);
            if (data == null) throw new IOException("图片编码失败: " + format);
            return data.ToArray();
        }

        /// <summary>按文件扩展名自动选择编码格式保存</summary>
        public static void Save(SKBitmap bmp, string path, SKEncodedImageFormat? format = null, int quality = 90)
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var fmt = format ?? FormatFromExtension(path);
            using var data = bmp.Encode(fmt, quality);
            if (data == null) throw new IOException("图片编码失败: " + path);

            using var fs = File.Create(path);
            data.SaveTo(fs);
        }

        public static void SavePng(SKBitmap bmp, string path) => Save(bmp, path, SKEncodedImageFormat.Png, 100);

        /// <summary>
        /// 保存为 WebP。
        /// 游戏贴图（像素风、带透明通道）默认无损（lossless）：像素 100% 还原，避免有损 WebP
        /// 在硬边缘产生的振铃/光晕失真，以及半透明边被压缩坏的问题。
        /// 无损模式下 quality 作为压缩力度（1~100，越大越慢、体积略小），不影响画质。
        /// 若需更高压缩比且可接受轻微失真，设 lossless=false，此时 quality 为有损质量(1~100)。
        /// </summary>
        public static void SaveWebP(SKBitmap bmp, string path, int quality = 90, bool lossless = true)
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var options = new SKWebpEncoderOptions
            {
                Lossless = lossless,
                Quality = (byte)ClampQuality(quality),
            };
            using var img = SKImage.FromBitmap(bmp);
            using var data = img.Encode(options);
            if (data == null) throw new IOException("WebP 编码失败: " + path);

            using var fs = File.Create(path);
            data.SaveTo(fs);
        }

        private static int ClampQuality(int q) => q < 0 ? 0 : (q > 100 ? 100 : q);

        /// <summary>解码图片文件；失败返回 null</summary>
        public static SKBitmap? Decode(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                using var stream = File.OpenRead(path);
                return SKBitmap.Decode(stream);
            }
            catch
            {
                return null;
            }
        }
    }
}
