// 文字光栅化：用 Canvas2D 把字形画进字形图集，C# 侧只负责上传像素。
const canvas = document.createElement('canvas');
const context = canvas.getContext('2d', { willReadFrequently: true });
function ctx2d() {
    if (!context)
        throw new Error('[text] 无法创建 Canvas2D 上下文');
    return context;
}
function writeInts(view, values) {
    const array = new Int32Array(values);
    if (view instanceof Int32Array) {
        view.set(array);
        return;
    }
    if (typeof view.set === 'function') {
        view.set(array, 0);
        return;
    }
    const fallback = view;
    for (let i = 0; i < values.length; i++)
        fallback[i] = values[i];
}
function writeBytes(view, data) {
    if (view instanceof Uint8Array) {
        view.set(data);
        return;
    }
    if (typeof view.set === 'function') {
        view.set(data, 0);
        return;
    }
    const fallback = view;
    for (let i = 0; i < data.length; i++)
        fallback[i] = data[i];
}
// out: [0]=advance(宽) [1]=总高 [2]=基线以上高度(ascent)
export function measure(text, font, out) {
    const c = ctx2d();
    c.font = font;
    const metrics = c.measureText(text);
    // actualBoundingBox* 在部分 WebKit 版本上会返回 0，字形会被裁成一条缝。
    // 这里用字号推算一个下限兜底，保证任何浏览器上字形盒子都装得下文字。
    const size = parseFloat((font.match(/(\d+(?:\.\d+)?)px/i) || [])[1] || '16');
    const ascent = Math.max(metrics.actualBoundingBoxAscent || 0, size * 0.80);
    const descent = Math.max(metrics.actualBoundingBoxDescent || 0, size * 0.25);
    writeInts(out, [
        Math.max(1, Math.ceil(metrics.width) + 1),
        Math.max(1, Math.ceil(ascent + descent) + 4),
        Math.max(1, Math.ceil(ascent) + 2),
        0,
    ]);
}
// 在 (x, y) 处（y 为基线）绘制白色文字，结果写入 rgba
export function render(text, font, x, y, width, height, rgba) {
    canvas.width = width;
    canvas.height = height;
    const c = ctx2d();
    c.clearRect(0, 0, width, height);
    c.font = font;
    c.textAlign = 'left';
    c.textBaseline = 'alphabetic';
    c.fillStyle = '#ffffff';
    c.fillText(text, x, y);
    // getImageData 返回的是 Uint8ClampedArray，而 .NET 的 MemoryView 只接受 Uint8Array
    const image = c.getImageData(0, 0, width, height).data;
    const bytes = new Uint8Array(image.length);
    bytes.set(image);
    writeBytes(rgba, bytes);
}
