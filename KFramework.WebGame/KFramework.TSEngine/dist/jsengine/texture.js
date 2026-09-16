import * as gl from './gl.js';
// 纹理解码：借浏览器原生解码器把图像字节（PNG / WebP 等）解码为 RGBA8。
// 因 WASM 无托管 WebP 解码器，统一走 createImageBitmap（浏览器原生，覆盖 Png / Webp）。
export async function decodeImageToRgba(bytes, outSize, outPixels) {
    const blob = new Blob([bytes]);
    const bitmap = await createImageBitmap(blob);
    const w = bitmap.width, h = bitmap.height;
    const cv = document.createElement('canvas');
    cv.width = w;
    cv.height = h;
    const c = cv.getContext('2d');
    c.drawImage(bitmap, 0, 0);
    const image = c.getImageData(0, 0, w, h).data;
    outPixels.set(image);
    outSize[0] = w;
    outSize[1] = h;
    if (bitmap.close)
        bitmap.close();
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
        locateFile: (p) => new URL(p, import.meta.url).href,
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
export async function uploadKtx2(bytes, basisFormat, glFormat) {
    const mod = await loadBasis();
    const src = bytes instanceof Uint8Array ? bytes : new Uint8Array(bytes);
    const ktx2File = new mod.KTX2File(src);
    try {
        if (!ktx2File.isValid())
            throw new Error('[ktx2] 无效的 KTX2 文件');
        if (!ktx2File.startTranscoding())
            throw new Error('[ktx2] Basis startTranscoding 失败');
        const faceCount = ktx2File.getFaces() || 1;
        const layerCount = ktx2File.getLayers() || 1;
        const levelCount = ktx2File.getLevels();
        const tex = gl.createTexture();
        gl.bindTexture(GL.TEXTURE_2D, tex);
        const uncompressed = basisFormat === 13; // cTFRGBA32 回退：裸 RGBA8
        for (let face = 0; face < faceCount; face++) {
            for (let mip = 0; mip < levelCount; mip++) {
                for (let layer = 0; layer < layerCount; layer++) {
                    const info = ktx2File.getImageLevelInfo(mip, layer, face);
                    const w = levelCount > 1 ? info.origWidth : info.width;
                    const h = levelCount > 1 ? info.origHeight : info.height;
                    const size = ktx2File.getImageTranscodedSizeInBytes(mip, layer, 0, basisFormat);
                    const dst = new Uint8Array(size);
                    if (!ktx2File.transcodeImage(dst, mip, layer, face, basisFormat, 0, -1, -1))
                        throw new Error(`[ktx2] 转码第 ${mip} 级失败`);
                    if (uncompressed) {
                        gl.texImage2D(GL.TEXTURE_2D, mip, GL.RGBA8, w, h, 0, GL.RGBA, GL.UNSIGNED_BYTE, dst);
                    }
                    else {
                        gl.compressedTexImage2D(GL.TEXTURE_2D, mip, glFormat, w, h, 0, dst);
                    }
                }
            }
        }
        const err = gl.getError();
        if (err !== 0)
            console.error(`[ktx2] 上传后 GL 错误 0x${err.toString(16)}`);
        const minFilter = levelCount > 1 ? GL.LINEAR_MIPMAP_LINEAR : GL.LINEAR;
        gl.texParameteri(GL.TEXTURE_2D, GL.TEXTURE_MIN_FILTER, minFilter);
        gl.texParameteri(GL.TEXTURE_2D, GL.TEXTURE_MAG_FILTER, GL.LINEAR);
        gl.texParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_S, GL.CLAMP_TO_EDGE);
        gl.texParameteri(GL.TEXTURE_2D, GL.TEXTURE_WRAP_T, GL.CLAMP_TO_EDGE);
        return tex;
    }
    finally {
        ktx2File.close();
        ktx2File.delete();
    }
}
