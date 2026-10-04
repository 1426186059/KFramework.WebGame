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
        /// <remarks>
        /// <b>三个参数都走 MemoryView</b>，各有理由：
        /// <list type="bullet">
        /// <item>输出缓冲（outSize / outPixels）：<c>byte[]</c> / <c>int[]</c> 按 <c>JSType.Array</c> 是
        /// <b>复制</b>到 JS，JS 写进副本的像素 / 宽高回不到托管数组 —— 现象就是像素全 0、宽高全 0。</item>
        /// <item>输入字节（bytes）：按 <c>byte[]</c> 传会在跨界时<b>白白多拷一次整块图片</b>（几十 KB ~ 几 MB），
        /// 而 JS 侧紧接着要 <c>new Blob(...)</c>，那一步又会自己拷一份 —— 等于拷两遍。
        /// 走 MemoryView 后输入是零拷贝的，JS 直接用共享视图喂 Blob（<b>Blob 构造函数是同步读取的</b>），
        /// 于是全程只拷一遍。</item>
        /// </list>
        /// 之所以用 ArraySegment 而非 Span：本调用跨 await，Span 版视图跨 await 会失效，
        /// ArraySegment 版会 pin 托管数组、由 JS 侧 dispose 解 pin。
        /// </remarks>
        [JSImport("decodeImageToRgba", "texture")]
        public static partial Task DecodeImageToRgba(
            [JSMarshalAs<JSType.MemoryView>] ArraySegment<byte> bytes,
            [JSMarshalAs<JSType.MemoryView>] ArraySegment<int> outSize,
            [JSMarshalAs<JSType.MemoryView>] ArraySegment<byte> outPixels);

        /// <summary>
        /// 仅取图像尺寸（不解码像素），供内容包之外的松散图片预分配像素缓冲。
        /// <paramref name="bytes"/> 为图像文件字节；返回 JSObject { width, height }（C# 经 <c>GetPropertyAsInt32</c> 读取）。
        /// </summary>
        /// <remarks>
        /// 输入走 MemoryView，理由同 <see cref="DecodeImageToRgba"/> —— 省掉跨界那一次整块拷贝；
        /// 同样因为要跨 await，只能用 ArraySegment。
        /// </remarks>
        [JSImport("getImageSize", "texture")]
        public static partial Task<JSObject> GetImageSize([JSMarshalAs<JSType.MemoryView>] ArraySegment<byte> bytes);

        /// <summary>
        /// 借浏览器中的 Basis Universal 转码器（basis_transcoder.js/.wasm）把 KTX2（Basis 超压缩）纹理的【基础级别】
        /// 转码为当前设备支持的 GPU 压缩格式字节，并写入 <paramref name="outBuffer"/>（按 <see cref="Ktx2TranscodeSelector.GetTranscodedSize"/> 预分配）。
        /// CPU 侧、不上传 GPU；C# 缓存字节后待 LoadTexture 经 CreateCompressedTexture 上传。
        /// 用输出缓冲而非返回值，是因为 JSImport 不直接支持返回 byte[]。
        /// </summary>
        /// <remarks>
        /// 输出缓冲走 MemoryView（同 <see cref="DecodeImageToRgba"/>），否则转码结果写进 JS 副本、C# 拿到全 0。
        /// <para>
        /// 但输入 <c>bytes</c> 这里<b>刻意保持 byte[]，不改成 MemoryView</b> —— 与上面两个方法不同：
        /// 转码前要先 <c>await loadBasis()</c> 加载 Basis 转码器（首次是几百毫秒的网络往返），
        /// 而零拷贝视图跨 await 会因堆增长被 detach 而<b>静默失效</b>。
        /// 所以 JS 侧无论如何都得先固化一份自有字节，那么把输入改成 MemoryView
        /// 就只是把拷贝从"跨界封送"挪到了"JS 侧 slice"，<b>拷贝次数不变</b>，反倒多付一次 pin。
        /// 维持 byte[]，让跨界那次拷贝直接产出 JS 可长期持有的字节，才是这里的最优解。
        /// </para>
        /// </remarks>
        [JSImport("transcodeKtx2Into", "texture")]
        public static partial Task TranscodeKtx2Into(byte[] bytes, int basisFormat, [JSMarshalAs<JSType.MemoryView>] ArraySegment<byte> outBuffer);
    }
}
