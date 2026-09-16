// 纹理解码：借浏览器原生解码器把图像字节（PNG / WebP 等）解码为 RGBA8。
export async function decodeImageToRgba(bytes, outSize, outPixels) {
    const blob = new Blob([bytes]);
    const bitmap = await createImageBitmap(blob);
    const w = bitmap.width, h = bitmap.height;
    const cv = document.createElement('canvas');
    cv.width = w; cv.height = h;
    const c = cv.getContext('2d');
    c.drawImage(bitmap, 0, 0);
    const image = c.getImageData(0, 0, w, h).data;
    outPixels.set(image);
    outSize[0] = w; outSize[1] = h;
    if (bitmap.close) bitmap.close();
}
