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
        /// 分两个独立函数（见 <see cref="DecodeImageToRgbaAsync1"/> / <see cref="DecodeImageToRgbaAsync2"/>）：
        /// <list type="bullet">
        /// <item><description>未知尺寸（松散图片）：<see cref="DecodeImageToRgbaAsync1"/> —— 解码成功后把 RGBA8 像素<b>暂存进 JS 模块缓存</b>，并返回打包宽高的 int（失败 -1）；调用方解出宽高后，经 <see cref="GetImageData"/> 取回像素字节。</description></item>
        /// <item><description>已知尺寸（资源包已带宽高）：<see cref="DecodeImageToRgbaAsync2"/> —— 解码后直接<b>零拷贝写入</b> outSize + outPixels 缓冲并返回 <c>true</c>。</description></item>
        /// </list>
        /// 任一步失败（解码失败 / 缓冲不足）返回 <c>false</c>，不抛。
        /// 因 WASM 无托管 WebP 解码器，统一走浏览器原生解码（覆盖 Png / Webp）。
        /// </summary>
        /// <remarks>
        /// <b>输入 <paramref name="bytes"/> 走 MemoryView</b>：按 <c>byte[]</c> 传会在跨界时白多拷一次整块图片，而 JS 侧紧接着 <c>new Blob(...)</c> 又会自拷一份；
        /// 走 MemoryView 后输入零拷贝，全程只拷一遍。
        /// 之所以用 ArraySegment 而非 Span：本调用跨 await，Span 版视图跨 await 会失效，
        /// ArraySegment 版会 pin 托管数组、由 JS 侧 dispose 解 pin。
        /// </remarks>

        /// <summary>
        /// 未知尺寸的纹理解码：只传 <paramref name="bytes"/>，解码成功后把 RGBA8 像素暂存进 JS 模块缓存，
        /// 返回一个把宽高按位打包成的 int：<c>(width << 16) | height</c>（高 16 位宽、低 16 位高；两维均 ≤ 65535，且必然 ≥ 0）；
        /// 调用方解出宽高后，再经 <see cref="GetImageData"/> 取回像素字节。解码失败返回 <c>-1</c>。
        /// </summary>
        [JSImport("decodeImageToRgbaAsync1", "texture")]
        public static partial Task<int> DecodeImageToRgbaAsync1(
            [JSMarshalAs<JSType.MemoryView>] ArraySegment<byte> bytes);

        /// <summary>
        /// 已知尺寸的纹理解码（原 DecodeImageToRgba，仅改名 + 返回 bool）：解码后零拷贝写入
        /// <paramref name="outSize"/>(int[2]，[宽,高]) 与 <paramref name="outPixels"/>(长度 = 宽*高*4)，成功返回 <c>true</c>，缓冲不足返回 <c>false</c>。
        /// </summary>
        [JSImport("decodeImageToRgbaAsync2", "texture")]
        public static partial Task<bool> DecodeImageToRgbaAsync2(
            [JSMarshalAs<JSType.MemoryView>] ArraySegment<byte> bytes,
            [JSMarshalAs<JSType.MemoryView>] ArraySegment<int> outSize,
            [JSMarshalAs<JSType.MemoryView>] ArraySegment<byte> outPixels);

        /// <summary>
        /// 取回「未知尺寸解码」缓存的 RGBA8 像素字节（RGBA8，长度 = 宽*高*4）。
        /// 必须在 <see cref="DecodeImageToRgbaAsync1"/> 返回 ≥ 0 后调用，否则抛错。
        /// 同步返回 <c>byte[]</c>：运行时把缓存的 Uint8Array 复制成一份托管数组（可长期持有），取走即清空 JS 缓存。
        /// 之所以能同步返回 byte[]（而非必须异步）：byte[] 的同步封送是“复制成新数组”，不像 JSObject 那样需要异步句柄。
        /// </summary>
        [JSImport("getImageData", "texture")]
        public static partial byte[] GetImageData();

        /// <summary>
        /// 借浏览器中的 Basis Universal 转码器（basis_transcoder.js/.wasm）把 KTX2（Basis 超压缩）纹理的【基础级别】
        /// 转码为当前设备支持的 GPU 压缩格式字节，并写入 <paramref name="outBuffer"/>（按 <see cref="Ktx2TranscodeSelector.GetTranscodedSize"/> 预分配）。
        /// CPU 侧、不上传 GPU；C# 缓存字节后待 LoadTexture 经 CreateCompressedTexture 上传。
        /// 用输出缓冲而非返回值，是因为 JSImport 不直接支持返回 byte[]。
        /// </summary>
        /// <remarks>
        /// 输出缓冲走 MemoryView（同 <see cref="DecodeImageToRgbaAsync"/>），否则转码结果写进 JS 副本、C# 拿到全 0。
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
