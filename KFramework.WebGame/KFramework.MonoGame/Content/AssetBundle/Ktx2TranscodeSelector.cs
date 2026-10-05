namespace KFramework.MonoGame
{
    /// <summary>
    /// 依据当前渲染后端支持的压缩纹理能力，为 KTX2（Basis Universal 超压缩）挑选“目标转码格式 + 对应 GL 内部格式”。
    /// WebGPU 后端按 adapter.features（经 render_webgpu.hasFeature 暴露）在 ASTC → BC7(BPTC) → ETC2/EAC → RGBA32 间择优；
    /// WebGL2 后端则按扩展支持度在 ASTC → BC7(BPTC) → ETC2/EAC → S3TC(DXT) → PVRTC → RGBA32 间择优。
    /// 始终选带 alpha 的变体（BC3 / ETC2 EAC RGBA / PVRTC RGBA），对不透明图也安全。
    /// </summary>
    /// <remarks>
    /// Basis Universal 的源无论是 UASTC 还是 ETC1S，都能转码到上述任意目标格式，故只需按设备支持度选择。
    /// </remarks>
    internal static class Ktx2TranscodeSelector
    {
        // Basis Universal 转码目标格式枚举（对应 basis_transcoder 的 TranscoderFormat，
        // 注意：不是 Basis 源编码格式 BasisFormat）。取值取自 three.js KTX2Loader.TranscoderFormat。
        public const int ETC2 = 1;            // TranscoderFormat.ETC2          -> COMPRESSED_RGBA8_ETC2_EAC
        public const int BC3 = 3;             // TranscoderFormat.BC3           -> COMPRESSED_RGBA_S3TC_DXT5_EXT
        public const int BC7_M5 = 7;          // TranscoderFormat.BC7_M5        -> COMPRESSED_RGBA_BPTC_UNORM
        public const int PVRTC1_4_RGBA = 9;   // TranscoderFormat.PVRTC1_4_RGBA -> COMPRESSED_RGBA_PVRTC_4BPPV1_IMG
        public const int ASTC_4x4 = 10;       // TranscoderFormat.ASTC_4x4      -> COMPRESSED_RGBA_ASTC_4x4_KHR
        public const int RGBA32 = 13;         // TranscoderFormat.RGBA32        -> 裸 RGBA8（不压缩，兜底）

        // 优先级（参考 three.js：质量从高到低）ASTC → BC7(BPTC) → ETC2/EAC → S3TC(DXT) → PVRTC → RGBA32。
        // 返回的 glFormat 即 <see cref="SurfaceFormat"/>（其取值就是对应的 GL 内部格式，无需再映射）。
        public static (int basisFormat, SurfaceFormat glFormat) Pick()
        {
            // WebGPU 后端：按 adapter.features（render_webgpu.hasFeature 暴露）选压缩格式。
            // 优先级参考 three.js：ASTC → BC7(BPTC) → ETC2/EAC → RGBA32。
            // 注意 WebGPU 规范不含 PVRTC，故此处不列 PVRTC 分支；特性全不支持时退回裸 RGBA8（不压缩，但保证能显示）。
            if (Game.Current.GraphicsDevice.Backend is WebGpuBackend)
            {
                if (JSBind_WebGPU.HasFeature("texture-compression-astc"))
                    return (ASTC_4x4, SurfaceFormat.Astc4X4);
                if (JSBind_WebGPU.HasFeature("texture-compression-bc"))
                    return (BC7_M5, SurfaceFormat.Bc7);
                if (JSBind_WebGPU.HasFeature("texture-compression-etc2"))
                    return (ETC2, SurfaceFormat.Etc2Rgba8);
            }
            else
            {
                if (JSBind_WEBGL20.HasExtension("WEBGL_compressed_texture_astc"))
                    return (ASTC_4x4, SurfaceFormat.Astc4X4);
                if (JSBind_WEBGL20.HasExtension("EXT_texture_compression_bptc"))
                    return (BC7_M5, SurfaceFormat.Bc7);
                if (JSBind_WEBGL20.HasExtension("WEBGL_compressed_texture_etc"))
                    return (ETC2, SurfaceFormat.Etc2Rgba8);
                if (JSBind_WEBGL20.HasExtension("WEBGL_compressed_texture_s3tc"))
                    return (BC3, SurfaceFormat.Dxt5);
                if (JSBind_WEBGL20.HasExtension("WEBGL_compressed_texture_pvrtc"))
                    return (PVRTC1_4_RGBA, SurfaceFormat.PvrtcRgba4Bpp);
                // 兜底：转码为裸 RGBA8，按普通纹理上传（体积/显存吃亏，但保证能显示）。
            }
            return (RGBA32, SurfaceFormat.Color);
        }

        /// <summary>
        /// 计算某目标格式、给定尺寸下的转码后字节数（与 <c>SurfaceFormatGL.GetExpectedCompressedBytes</c> 一致），
        /// 用于解包阶段在 C# 侧预分配转码输出缓冲（JSImport 不直接支持返回 byte[]）。
        /// 与 <see cref="GraphicsDevice.CreateTexture"/> 上传前的校验共用同一套公式，保证分配 = 校验。
        /// </summary>
        public static int GetTranscodedSize(SurfaceFormat format, int width, int height)
        {
            if (format == SurfaceFormat.Color)
                return width * height * 4; // 裸 RGBA8 兜底
            int expected = SurfaceFormatGL.GetExpectedCompressedBytes(format, width, height);
            return expected > 0 ? expected : width * height * 4;
        }
    }
}
