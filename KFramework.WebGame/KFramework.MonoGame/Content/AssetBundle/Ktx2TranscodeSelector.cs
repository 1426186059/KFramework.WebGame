namespace KFramework.MonoGame
{
    /// <summary>
    /// 依据设备支持的 WebGL2 压缩纹理扩展，为 KTX2（Basis Universal 超压缩）挑选
    /// “目标转码格式 + 对应 GL 内部格式”。
    /// 优先级（质量 / 压缩比从高到低，对齐 three.js）：ASTC → BC7(BPTC) → ETC2/EAC → S3TC(DXT) → PVRTC → RGBA32（回退，不压缩）。
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
        public static (int basisFormat, int glFormat) Pick()
        {
            if (JSBind_GL.HasExtension("WEBGL_compressed_texture_astc"))
                return (ASTC_4x4, JSBind_GL.COMPRESSED_RGBA_ASTC_4x4_KHR);
            if (JSBind_GL.HasExtension("EXT_texture_compression_bptc"))
                return (BC7_M5, JSBind_GL.COMPRESSED_RGBA_BPTC_UNORM);
            if (JSBind_GL.HasExtension("WEBGL_compressed_texture_etc"))
                return (ETC2, JSBind_GL.COMPRESSED_RGBA8_ETC2_EAC);
            if (JSBind_GL.HasExtension("WEBGL_compressed_texture_s3tc"))
                return (BC3, JSBind_GL.COMPRESSED_RGBA_S3TC_DXT5_EXT);
            if (JSBind_GL.HasExtension("WEBGL_compressed_texture_pvrtc"))
                return (PVRTC1_4_RGBA, JSBind_GL.COMPRESSED_RGBA_PVRTC_4BPPV1_IMG);
            // 兜底：转码为裸 RGBA8，按普通纹理上传（体积/显存吃亏，但保证能显示）。
            return (RGBA32, JSBind_GL.RGBA8);
        }
    }
}
