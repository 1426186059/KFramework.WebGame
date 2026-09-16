using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{

    /// <summary>
    /// texture 模块绑定：借浏览器原生解码器把图像字节（PNG / WebP 等）解码为 RGBA8。
    /// 这里只负责跨语言调用，实际解码见 tsengine 的 texture 模块（createImageBitmap）。
    /// </summary>
    internal static partial class JSBind_Texture
    {
        /// <summary>
        /// 借浏览器原生解码器（createImageBitmap）把图像字节（PNG / WebP 等）解码为 RGBA8。
        /// <paramref name="bytes"/> 为图像文件字节；<paramref name="outSize"/> 写入 [宽, 高]（int[2]）；
        /// <paramref name="outPixels"/> 写入 RGBA8 像素（长度 = 宽*高*4）。
        /// 因 WASM 无托管 WebP 解码器，统一走浏览器原生解码（覆盖 Png / Webp）。
        /// </summary>
        [JSImport("decodeImageToRgba", "texture")]
        internal static partial Task DecodeImageToRgba(byte[] bytes, int[] outSize, byte[] outPixels);
    }
}
