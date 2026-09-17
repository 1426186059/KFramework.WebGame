// 【依赖 C#】由 KFramework.MonoGame.JSBind_Texture 经 [JSImport(module: "texture")] 调用（含 KTX2/Basis 转码）；产物 texture.js 由 SyncJsEngine 复制。
import * as gl from './gl.js';

// 纹理解码：借浏览器原生解码器把图像字节（PNG / WebP 等）解码为 RGBA8。
// 因 WASM 无托管 WebP 解码器，统一走 createImageBitmap（浏览器原生，覆盖 Png / Webp）。
export async function decodeImageToRgba(
    bytes: Uint8Array, outSize: Int32Array, outPixels: Uint8Array,
): Promise<void> {
    const blob = new Blob([bytes as BlobPart]);
    const bitmap = await createImageBitmap(blob);
    const w = bitmap.width, h = bitmap.height;
    const cv = document.createElement('canvas');
    cv.width = w;
    cv.height = h;
    const c = cv.getContext('2d')!;
    c.drawImage(bitmap, 0, 0);
    const image = c.getImageData(0, 0, w, h).data;
    outPixels.set(image);
    outSize[0] = w;
    outSize[1] = h;
    if (bitmap.close) bitmap.close();
}

// 仅取图像尺寸（不解码像素），用于「直接复制、未打包」的松散图片——
// 调用方据此预分配像素缓冲后，再调 decodeImageToRgba 一次性解码上传。
// 返回 { width, height }（JSObject，C# 经 GetPropertyAsInt32 读取）。
export async function getImageSize(
    bytes: Uint8Array,
): Promise<{ width: number; height: number }> {
    const blob = new Blob([bytes as BlobPart]);
    const bitmap = await createImageBitmap(blob);
    const w = bitmap.width, h = bitmap.height;
    if (bitmap.close) bitmap.close();
    return { width: w, height: h };
}

// ---------- KTX2（Basis Universal 超压缩）纹理上传 ----------

// 懒加载官方 Basis Universal 转码器（basis_transcoder.js + .wasm）。
// 文件随 tsengine 一起复制到 jsengine/deps/ktx2/，路径相对于本模块（deps/ktx2/）。
let _basis: any = null;

async function loadBasis(): Promise<any> {
    if (_basis) return _basis;
    const jsUrl = new URL('./deps/ktx2/basis_transcoder.js', import.meta.url).href;
    await new Promise<void>((resolve, reject) => {
        const s = document.createElement('script');
        s.src = jsUrl;
        s.onload = () => resolve();
        s.onerror = () =>
            reject(new Error('[ktx2] 加载 basis_transcoder.js 失败：请确认其位于 jsengine/deps/ktx2/'));
        document.head.appendChild(s);
    });
    const factory: any = (window as any).BASIS || (globalThis as any).BASIS;
    if (!factory) throw new Error('[ktx2] basis_transcoder.js 未暴露 BASIS 全局');
    const mod: any = await factory({
        locateFile: (p: string) => new URL(p, import.meta.url).href,
    });
    mod.initializeBasis();
    _basis = mod;
    return mod;
}

// 各 Basis 转码格式对应的 GL 内部格式（与 C# Ktx2TranscodeSelector 保持一致）。
const GL = {
    TEXTURE_2D: 0x0de1,
    RGBA8: 0x8058,
    RGBA: 0x1908,
    UNSIGNED_BYTE: 0x1401,
    TEXTURE_MIN_FILTER: 0x2801,
    TEXTURE_MAG_FILTER: 0x2800,
    TEXTURE_WRAP_S: 0x2802,
    TEXTURE_WRAP_T: 0x2803,
    LINEAR: 0x2601,
    LINEAR_MIPMAP_LINEAR: 0x2703,
    CLAMP_TO_EDGE: 0x812f,
};

/**
 * 借浏览器中的 Basis 转码器把 KTX2（Basis 超压缩）纹理转码为当前设备支持的 GPU 压缩格式，
 * 并直接上传到一张新建的 WebGL2 纹理。
 * @param bytes KTX2 文件字节
 * @param basisFormat 目标 Basis 转码格式枚举（cTFASTC_4x4=10 / cTFBC7_M5=7 / cTFBC3=3 / cTFETC2=1 / cTFPVRTC1_4_RGBA=9 / cTFRGBA32=13）
 * @param glFormat 对应的 WebGL 内部格式枚举（cTFRGBA32 回退时为 RGBA8）
 * @returns 新建的 WebGLTexture
 */
export async function transcodeKtx2Into(
    bytes: Uint8Array,
    basisFormat: number,
    outBuffer: Uint8Array,
): Promise<void> {
    const mod = await loadBasis();
    const src = bytes instanceof Uint8Array ? bytes : new Uint8Array(bytes);
    const ktx2File = new mod.KTX2File(src);
    try {
        if (!ktx2File.isValid())
            throw new Error('[ktx2] 无效的 KTX2 文件');
        if (!ktx2File.startTranscoding())
            throw new Error('[ktx2] Basis startTranscoding 失败');

        // 只转码基础级别（mip 0 / layer 0 / face 0）：GPU 上传延后到 C# 的 CreateCompressedTexture。
        const info = ktx2File.getImageLevelInfo(0, 0, 0);
        const size = ktx2File.getImageTranscodedSizeInBytes(0, 0, 0, basisFormat);
        if (outBuffer.length < size)
            throw new Error(`[ktx2] 输出缓冲 ${outBuffer.length} 小于所需 ${size}（请检查 GetTranscodedSize）`);
        const dst = outBuffer instanceof Uint8Array ? outBuffer : new Uint8Array(outBuffer);
        if (!ktx2File.transcodeImage(dst, 0, 0, 0, basisFormat, 0, -1, -1))
            throw new Error('[ktx2] 转码基础级别失败');
    } finally {
        ktx2File.close();
        ktx2File.delete();
    }
}
