using System.IO;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 纹理的实际数据格式（写入 AssetBundle 的“真实”格式），与构建端的“统一转换目标” <see cref="ContentTextureSwitchTarget"/> 区分开：
    /// <list type="bullet">
    ///   <item><see cref="ContentTextureSwitchTarget"/> 是构建配置，决定整包图统一转成什么；其中 <c>None</c> 表示不走统一转换、保留每张图自身的 <see cref="ContentTextureDataFormat"/>。</item>
    ///   <item><see cref="ContentTextureDataFormat"/> 则是某张图最终落库的数据格式：可以是 CPU 压缩/无压缩格式（<see cref="Png"/>/<see cref="Webp"/>/<see cref="Jpg"/>/<see cref="Bmp"/>/<see cref="Gif"/>/<see cref="Tiff"/>）、
    ///        裸像素 <see cref="Rgba"/>，或 GPU 压缩 <see cref="Ktx2"/>。</item>
    /// </list>
    /// 这些格式均可由浏览器原生 <c>createImageBitmap</c> 解码（Rgba 直接上传、Ktx2 经 Basis 转码器）；构建端在 <see cref="ContentTextureSwitchTarget.None"/> 时按源图自身的 <see cref="ContentTextureDataFormat"/> 原样保留。
    /// </summary>
    public enum ContentTextureDataFormat
    {
        /// <summary>PNG（8 位、无损）。</summary>
        Png = 0,
        /// <summary>WebP（有损 / 无损）。</summary>
        Webp = 1,
        /// <summary>JPEG。</summary>
        Jpg = 2,
        /// <summary>Windows Bitmap。</summary>
        Bmp = 3,
        /// <summary>GIF。</summary>
        Gif = 4,
        /// <summary>TIFF。</summary>
        Tiff = 5,
        /// <summary>裸 RGBA8 像素（行优先，长度 = Width*Height*4）。不压缩、无图像头，运行端直接上传 GPU。</summary>
        Rgba = 6,
        /// <summary>KTX2（Basis Universal 超压缩 GPU 纹理）。</summary>
        Ktx2 = 7,
    }

    /// <summary><see cref="ContentTextureDataFormat"/> 的辅助方法：按扩展名推断源图数据格式、并给出落库实际格式与是否原样保留字节。</summary>
    public static class ContentTextureDataFormatHelper
    {
        /// <summary>按文件扩展名推断源图数据格式（未知扩展名回退 Png）。</summary>
        public static ContentTextureDataFormat FromExtension(string relative)
        {
            string ext = Path.GetExtension(relative).ToLowerInvariant();
            if (ext == ".webp") return ContentTextureDataFormat.Webp;
            if (ext == ".jpg" || ext == ".jpeg") return ContentTextureDataFormat.Jpg;
            if (ext == ".bmp") return ContentTextureDataFormat.Bmp;
            if (ext == ".gif") return ContentTextureDataFormat.Gif;
            if (ext == ".tif" || ext == ".tiff") return ContentTextureDataFormat.Tiff;
            return ContentTextureDataFormat.Png;
        }

        /// <summary>
        /// 返回该格式落库时的实际数据格式与是否原样保留字节：
        /// <list type="bullet">
        ///   <item>Png / Webp / Jpg / Bmp / Gif / Tiff 等文件格式：<c>KeepOriginalBytes=true</c>，直接存源字节。</item>
        ///   <item>Rgba / Ktx2：本身即目标数据格式，<c>KeepOriginalBytes=false</c>。</item>
        /// </list>
        /// 返回值类型为 <see cref="ContentTextureDataFormat"/>——这正是写入 manifest 的 <c>Format</c> 字段类型。
        /// </summary>
        public static (ContentTextureDataFormat Stored, bool KeepOriginalBytes) ToStored(this ContentTextureDataFormat cpu)
            => cpu switch
            {
                ContentTextureDataFormat.Rgba => (ContentTextureDataFormat.Rgba, false),
                ContentTextureDataFormat.Ktx2 => (ContentTextureDataFormat.Ktx2, false),
                _ => (cpu, true),
            };
    }
}
