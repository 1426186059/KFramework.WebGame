// 【依赖 C#】由 KFramework.MonoGame.JSBind_Texture 经 [JSImport(module: "texture")] 调用（含 KTX2/Basis 转码）；产物 texture.js 由 SyncJsEngine 复制。
import { sharedBytesOf } from './custom_data_byte_cache.js';
import { assert } from './cusotm_func.js';
// 取图像源字节：优先【零拷贝】拿共享托管内存的视图；拿不到才退回 slice() 拷一份。
//
// 为什么这里敢用共享视图：new Blob(...) 是【同步读取】的 —— 数据在 Blob 构造函数返回前
// 就已固化成 Blob 自己的字节，之后 await createImageBitmap(blob) 用的是那份，
// 与共享视图此后是否失效无关。于是全程只拷一遍（Blob 那次）；
// 若退回 slice()，就变成"先把视图拷成副本 + Blob 再拷一遍"，白多一次几 MB 的 memcpy。
function sourceBytes(view) {
    const shared = sharedBytesOf(view);
    if (shared)
        return shared;
    const sliced = view.slice();
    return new Uint8Array(sliced.buffer, sliced.byteOffset, sliced.byteLength);
}
// 未知尺寸解码的像素缓存：decodeImageToRgbaAsync1 入缓存，getImageData 取回后清空。
let _lastDecoded = null;
// 纹理解码：借浏览器原生解码器把图像字节（PNG / WebP 等）解码为 RGBA8。
// 因 WASM 无托管 WebP 解码器，统一走 createImageBitmap（浏览器原生，覆盖 Png / Webp）。
// bytes 走 ArraySegment + MemoryView：C# 侧跨界（await createImageBitmap）时不再整块拷贝（理由见 JSBind_Texture 的注释）。
// 未知尺寸（松散图片）：解码成功后把 RGBA8 像素暂存进 _lastDecoded 缓存，返回打包宽高的 int（(w << 16) | h：高 16 位宽、低 16 位高）；
// 调用方解出宽高后，用 getImageData 取回像素。失败（解码失败）返回 -1，不抛。
export async function decodeImageToRgbaAsync1(bytes) {
    try {
        const blob = new Blob([sourceBytes(bytes)]);
        const bitmap = await createImageBitmap(blob);
        const w = bitmap.width, h = bitmap.height;
        // 断言：宽高均不得大于 short 的最大值（32767），即 ≤ 32767；否则 (w<<16)|h 打包会失真/越界。
        // 用不依赖 console 的硬断言（throw），以免 Release 剥离 console 时把断言一并删掉。
        assert(w <= 32767 && h <= 32767, `decodeImageToRgbaAsync1 尺寸越界：w=${w} h=${h}（须 ≤ 32767 / short.MaxValue）`);
        const cv = document.createElement('canvas');
        cv.width = w;
        cv.height = h;
        const c = cv.getContext('2d');
        c.drawImage(bitmap, 0, 0);
        const image = c.getImageData(0, 0, w, h).data;
        if (bitmap.close)
            bitmap.close();
        // 未知尺寸：缓存像素，供 getImageData 取回
        _lastDecoded = { width: w, height: h, data: new Uint8Array(image) };
        // 打包宽高：高 16 位存 w、低 16 位存 h，即 (w << 16) | h（每维 < 65536 时位不重叠；本函数断言已限制更严 < 32767）；解码失败返回 -1。
        return (w << 16) | h;
    }
    catch {
        return -1;
    }
    finally {
        // 视图是 ArraySegment 版，pin 了托管数组，用完必须解 pin
        bytes.dispose?.();
    }
}
// 已知尺寸（资源包已带宽高）：解码后直接零拷贝写入 outSize(int[2]) 与 outPixels(长度 = 宽*高*4)，返回 true；
// 缓冲不足返回 false。失败（解码失败）返回 false，不抛。
export async function decodeImageToRgbaAsync2(bytes, outSize, outPixels) {
    try {
        const blob = new Blob([sourceBytes(bytes)]);
        const bitmap = await createImageBitmap(blob);
        const w = bitmap.width, h = bitmap.height;
        const cv = document.createElement('canvas');
        cv.width = w;
        cv.height = h;
        const c = cv.getContext('2d');
        c.drawImage(bitmap, 0, 0);
        const image = c.getImageData(0, 0, w, h).data;
        if (bitmap.close)
            bitmap.close();
        // 已知尺寸：直接写入预分配缓冲
        if (image.byteLength > outPixels.byteLength)
            return false;
        outPixels.set(image, 0);
        outSize.set(new Int32Array([w, h]), 0);
        return true;
    }
    catch {
        return false;
    }
    finally {
        // 视图是 ArraySegment 版，pin 了托管数组，用完必须解 pin
        bytes.dispose?.();
        outSize?.dispose?.();
        outPixels?.dispose?.();
    }
}
// 取回「未知尺寸解码」缓存的 RGBA8 像素字节（C# 侧以同步 byte[] 导入）。
// 必须在 decodeImageToRgbaAsync1(bytes) 返回 ≥ 0 之后调用；否则抛错（上一步解码失败或未调用）。
// 直接返回缓存的 Uint8Array：C# 封送复制成新 byte[]，取走即清空缓存，避免多次加载堆积。
// 不再 async / 不再包 JSObject —— 同步返回 byte[] 在本互操作下可行（byte[] 同步封送 = 复制成新数组）。
export function getImageData() {
    if (!_lastDecoded)
        throw new Error('[texture] getImageData 前必须先成功调用 decodeImageToRgbaAsync1（未知尺寸）');
    const cached = _lastDecoded;
    _lastDecoded = null;
    return cached.data;
}
// ---------- KTX2（Basis Universal 超压缩）纹理上传 ----------
// 懒加载官方 Basis Universal 转码器（basis_transcoder.js + .wasm）。
// 文件随 tsengine 一起复制到 jsengine/deps/ktx2/，路径相对于本模块（deps/ktx2/）。
let _basis = null;
async function loadBasis() {
    if (_basis)
        return _basis;
    const jsUrl = new URL('./deps/ktx2/basis_transcoder.js', import.meta.url).href;
    await new Promise((resolve, reject) => {
        const s = document.createElement('script');
        s.src = jsUrl;
        s.onload = () => resolve();
        s.onerror = () => reject(new Error('[ktx2] 加载 basis_transcoder.js 失败：请确认其位于 jsengine/deps/ktx2/'));
        document.head.appendChild(s);
    });
    const factory = window.BASIS || globalThis.BASIS;
    if (!factory)
        throw new Error('[ktx2] basis_transcoder.js 未暴露 BASIS 全局');
    const mod = await factory({
        // wasm 与 basis_transcoder.js 同目录（deps/ktx2/），必须相对 jsUrl 解析；
        // 若相对 import.meta.url（本模块 texture.js 在 /jsengine/）会导致 wasm 404。
        locateFile: (p) => new URL(p, jsUrl).href,
    });
    mod.initializeBasis();
    _basis = mod;
    return mod;
}
/**
 * 借浏览器中的 Basis 转码器把 KTX2（Basis 超压缩）纹理转码为当前设备支持的 GPU 压缩格式，
 * 并直接上传到一张新建的 WebGL2 纹理。
 * @param bytes KTX2 文件字节
 * @param basisFormat 目标 Basis 转码格式枚举（cTFASTC_4x4=10 / cTFBC7_M5=7 / cTFBC3=3 / cTFETC2=1 / cTFPVRTC1_4_RGBA=9 / cTFRGBA32=13）
 * @param glFormat 对应的 WebGL 内部格式枚举（cTFRGBA32 回退时为 RGBA8）
 * @returns 新建的 WebGLTexture
 */
// outBuffer 是 C# 的 ArraySegment<byte> → MemoryView_ArraySegment：转码器要 Uint8Array，先转码到临时缓冲再拷回视图。
//
// 【bytes 这里刻意仍是 byte[]，不像上面两个方法那样走 MemoryView】
// 转码前要先 await loadBasis() 加载 Basis 转码器（首次是几百毫秒的网络往返），
// 而零拷贝视图跨 await 会因堆增长被 detach 而【静默失效】。
// 所以 JS 侧无论如何都得先有一份自有字节；改成 MemoryView 只是把拷贝从"跨界封送"
// 挪到"JS 侧 slice"，拷贝次数不变，反倒多付一次 pin。故维持 byte[]。
export async function transcodeKtx2Into(bytes, basisFormat, outBuffer) {
    const mod = await loadBasis();
    const src = bytes instanceof Uint8Array ? bytes : new Uint8Array(bytes);
    const ktx2File = new mod.KTX2File(src);
    try {
        if (!ktx2File.isValid())
            throw new Error('[ktx2] 无效的 KTX2 文件');
        if (!ktx2File.startTranscoding())
            throw new Error('[ktx2] Basis startTranscoding 失败');
        // 只转码基础级别（mip 0 / layer 0 / face 0）：GPU 上传延后到 C# 的 CreateCompressedTexture。
        const size = ktx2File.getImageTranscodedSizeInBytes(0, 0, 0, basisFormat);
        if (outBuffer.byteLength < size)
            throw new Error(`[ktx2] 输出缓冲 ${outBuffer.byteLength} 小于所需 ${size}（请检查 GetTranscodedSize）`);
        const dst = new Uint8Array(size);
        if (!ktx2File.transcodeImage(dst, 0, 0, 0, basisFormat, 0, -1, -1))
            throw new Error('[ktx2] 转码基础级别失败');
        outBuffer.set(dst, 0);
    }
    finally {
        ktx2File.close();
        ktx2File.delete();
        outBuffer.dispose?.();
    }
}
