// 文字光栅化：用 Canvas2D 把字形画进字形图集，C# 侧只负责上传像素。

const canvas = document.createElement('canvas');
const context = canvas.getContext('2d', { willReadFrequently: true });

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
    for (let i = 0; i < values.length; i++) view[i] = values[i];
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
    for (let i = 0; i < data.length; i++) view[i] = data[i];
}

// out: [0]=advance(宽) [1]=总高 [2]=基线以上高度(ascent)
export function measure(text, font, out) {
    context.font = font;

    const metrics = context.measureText(text);
    const ascent = metrics.actualBoundingBoxAscent || metrics.fontBoundingBoxAscent || 0;
    const descent = metrics.actualBoundingBoxDescent || metrics.fontBoundingBoxDescent || 0;

    writeInts(out, [
        Math.max(1, Math.ceil(metrics.width)),
        Math.max(1, Math.ceil(ascent + descent) + 2),
        Math.max(1, Math.ceil(ascent) + 1),
        0,
    ]);
}

// 在 (x, y) 处（y 为基线）绘制白色文字，结果写入 rgba
export function render(text, font, x, y, width, height, rgba) {
    canvas.width = width;
    canvas.height = height;

    context.clearRect(0, 0, width, height);
    context.font = font;
    context.textAlign = 'left';
    context.textBaseline = 'alphabetic';
    context.fillStyle = '#ffffff';
    context.fillText(text, x, y);

    const data = context.getImageData(0, 0, width, height).data;
    writeBytes(rgba, data);
}
