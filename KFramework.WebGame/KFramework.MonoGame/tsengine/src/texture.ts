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
