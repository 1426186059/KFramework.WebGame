using System;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 纹理表面格式（对齐 MonoGame 的 <c>SurfaceFormat</c>，取 WebGL2 用得到的子集）。
    /// 用枚举而非裸 GL 常量表达格式：既能类型安全，又能在 <see cref="SurfaceFormatGL"/> 一处统一映射到 GL 内部格式 / 压缩块大小。
    /// </summary>
    public enum SurfaceFormat
    {
        /// <summary>无压缩 RGBA8（每像素 4 字节）。</summary>
        Color,
        /// <summary>DXT1 / BC1（4x4 块，每块 8 字节）。</summary>
        Dxt1,
        /// <summary>DXT3 / BC2（4x4 块，每块 16 字节）。</summary>
        Dxt3,
        /// <summary>DXT5 / BC3（4x4 块，每块 16 字节）。</summary>
        Dxt5,
        /// <summary>BC7（4x4 块，每块 16 字节，需 EXT_texture_compression_bptc）。</summary>
        Bc7,
        /// <summary>ASTC 4x4（每块 16 字节）。</summary>
        Astc4X4,
        /// <summary>ASTC 5x5（每块 16 字节）。</summary>
        Astc5X5,
        /// <summary>ASTC 6x6（每块 16 字节）。</summary>
        Astc6X6,
        /// <summary>ASTC 8x8（每块 16 字节）。</summary>
        Astc8X8,
        /// <summary>ASTC 10x10（每块 16 字节）。</summary>
        Astc10X10,
        /// <summary>ASTC 12x12（每块 16 字节）。</summary>
        Astc12X12,
        /// <summary>ETC2 RGB8（4x4 块，每块 8 字节）。</summary>
        Etc2Rgb8,
        /// <summary>ETC2 RGBA8 (EAC)（4x4 块，每块 16 字节）。</summary>
        Etc2Rgba8,
        /// <summary>PVRTC RGBA 2bpp（4x4 块，每块 8 字节，需 IMG_texture_compression_pvrtc）。</summary>
        PvrtcRgba2Bpp,
        /// <summary>PVRTC RGBA 4bpp（4x4 块，每块 16 字节，需 IMG_texture_compression_pvrtc）。</summary>
        PvrtcRgba4Bpp,
    }

    /// <summary>
    /// <see cref="SurfaceFormat"/> 到 WebGL2 GL 内部格式常量 / 压缩块大小的映射（对应 MonoGame 各平台后端里的格式转换表）。
    /// </summary>
    internal static class SurfaceFormatGL
    {
        public const int RGBA8 = 0x8058;
        public const int COMPRESSED_RGBA_S3TC_DXT1_EXT = 0x83F0;
        public const int COMPRESSED_RGBA_S3TC_DXT3_EXT = 0x83F2;
        public const int COMPRESSED_RGBA_S3TC_DXT5_EXT = 0x83F3;
        public const int COMPRESSED_RGBA_BC7_EXT = 0x9093;
        public const int COMPRESSED_RGBA_ASTC_4X4_KHR = 0x93B0;
        public const int COMPRESSED_RGBA_ASTC_5X5_KHR = 0x93B1;
        public const int COMPRESSED_RGBA_ASTC_6X6_KHR = 0x93B2;
        public const int COMPRESSED_RGBA_ASTC_8X8_KHR = 0x93B3;
        public const int COMPRESSED_RGBA_ASTC_10X10_KHR = 0x93B4;
        public const int COMPRESSED_RGBA_ASTC_12X12_KHR = 0x93B5;
        public const int COMPRESSED_RGB8_ETC2 = 0x9274;
        public const int COMPRESSED_RGBA8_ETC2_EAC = 0x9278;
        public const int COMPRESSED_RGBA_PVRTC_2BPPV1_IMG = 0x8C03;
        public const int COMPRESSED_RGBA_PVRTC_4BPPV1_IMG = 0x8C02;

        /// <summary>返回该格式对应的 GL 内部格式常量（即 compressedTexImage2D 的 internalFormat）。</summary>
        public static int ToInternalFormat(SurfaceFormat format) => format switch
        {
            SurfaceFormat.Color => RGBA8,
            SurfaceFormat.Dxt1 => COMPRESSED_RGBA_S3TC_DXT1_EXT,
            SurfaceFormat.Dxt3 => COMPRESSED_RGBA_S3TC_DXT3_EXT,
            SurfaceFormat.Dxt5 => COMPRESSED_RGBA_S3TC_DXT5_EXT,
            SurfaceFormat.Bc7 => COMPRESSED_RGBA_BC7_EXT,
            SurfaceFormat.Astc4X4 => COMPRESSED_RGBA_ASTC_4X4_KHR,
            SurfaceFormat.Astc5X5 => COMPRESSED_RGBA_ASTC_5X5_KHR,
            SurfaceFormat.Astc6X6 => COMPRESSED_RGBA_ASTC_6X6_KHR,
            SurfaceFormat.Astc8X8 => COMPRESSED_RGBA_ASTC_8X8_KHR,
            SurfaceFormat.Astc10X10 => COMPRESSED_RGBA_ASTC_10X10_KHR,
            SurfaceFormat.Astc12X12 => COMPRESSED_RGBA_ASTC_12X12_KHR,
            SurfaceFormat.Etc2Rgb8 => COMPRESSED_RGB8_ETC2,
            SurfaceFormat.Etc2Rgba8 => COMPRESSED_RGBA8_ETC2_EAC,
            SurfaceFormat.PvrtcRgba2Bpp => COMPRESSED_RGBA_PVRTC_2BPPV1_IMG,
            SurfaceFormat.PvrtcRgba4Bpp => COMPRESSED_RGBA_PVRTC_4BPPV1_IMG,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "未支持的 SurfaceFormat"),
        };

        /// <summary>
        /// 返回该压缩格式下给定宽高预期的字节数（用于在上传前校验 data 长度，对应 MonoGame 的块大小校验）。
        /// 非压缩格式（<see cref="SurfaceFormat.Color"/>）返回 -1（不校验）。
        /// </summary>
        public static int GetExpectedCompressedBytes(SurfaceFormat format, int width, int height)
        {
            (int blockW, int blockH, int bytesPerBlock) = BlockInfo(format);
            if (bytesPerBlock <= 0) return -1;
            int blocksX = (width + blockW - 1) / blockW;
            int blocksY = (height + blockH - 1) / blockH;
            return blocksX * blocksY * bytesPerBlock;
        }

        private static (int, int, int) BlockInfo(SurfaceFormat format) => format switch
        {
            SurfaceFormat.Dxt1 or SurfaceFormat.Etc2Rgb8 or SurfaceFormat.PvrtcRgba2Bpp => (4, 4, 8),
            SurfaceFormat.Dxt3 or SurfaceFormat.Dxt5 or SurfaceFormat.Bc7
                or SurfaceFormat.Etc2Rgba8 or SurfaceFormat.PvrtcRgba4Bpp => (4, 4, 16),
            SurfaceFormat.Astc4X4 => (4, 4, 16),
            SurfaceFormat.Astc5X5 => (5, 5, 16),
            SurfaceFormat.Astc6X6 => (6, 6, 16),
            SurfaceFormat.Astc8X8 => (8, 8, 16),
            SurfaceFormat.Astc10X10 => (10, 10, 16),
            SurfaceFormat.Astc12X12 => (12, 12, 16),
            _ => (0, 0, 0), // 非压缩
        };
    }
}
