using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

namespace KFramework.MonoGame
{

    /// <summary>
    /// texture 模块绑定：借浏览器原生解码器把图像字节（PNG / WebP 等）解码为 RGBA8；
    /// 或借 Basis Universal 转码器把 KTX2（GPU 压缩纹理）转码为设备原生压缩格式并上传 GPU。
    /// 这里只负责跨语言调用，实际逻辑见 KFramework.TSEngine/src/texture.ts（"texture" 模块）；编译产物 texture.js 由 SyncJsEngine 复制到 wwwroot/jsengine。
    /// </summary>
    public static partial class JSBind_Texture
    {
        /// <summary>
        /// 借浏览器原生解码器（createImageBitmap）把图像字节（PNG / WebP 等）解码为 RGBA8。
        /// <paramref name="bytes"/> 为图像文件字节；<paramref name="outSize"/> 写入 [宽, 高]（int[2]）；
        /// <paramref name="outPixels"/> 写入 RGBA8 像素（长度 = 宽*高*4）。
        /// 因 WASM 无托管 WebP 解码器，统一走浏览器原生解码（覆盖 Png / Webp）。
        /// </summary>
        [JSImport("decodeImageToRgba", "texture")]
        public static partial Task DecodeImageToRgba(byte[] bytes, int[] outSize, byte[] outPixels);

        /// <summary>
        /// 仅取图像尺寸（不解码像素），供内容包之外的松散图片预分配像素缓冲。
        /// <paramref name="bytes"/> 为图像文件字节；返回 JSObject { width, height }（C# 经 <c>GetPropertyAsInt32</c> 读取）。
        /// </summary>
        [JSImport("getImageSize", "texture")]
        public static partial Task<JSObject> GetImageSize(byte[] bytes);

        /// <summary>
        /// 借浏览器中的 Basis Universal 转码器（basis_transcoder.js/.wasm）把 KTX2（Basis 超压缩）纹理的【基础级别】
        /// 转码为当前设备支持的 GPU 压缩格式字节，并写入 <paramref name="outBuffer"/>（按 <see cref="Ktx2TranscodeSelector.GetTranscodedSize"/> 预分配）。
        /// CPU 侧、不上传 GPU；C# 缓存字节后待 LoadTexture 经 CreateCompressedTexture 上传。
        /// 用输出缓冲而非返回值，是因为 JSImport 不直接支持返回 byte[]。
        /// </summary>
        [JSImport("transcodeKtx2Into", "texture")]
        public static partial Task TranscodeKtx2Into(byte[] bytes, int basisFormat, byte[] outBuffer);
    }
}
