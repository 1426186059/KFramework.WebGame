// 【依赖 C#】由 KFramework.MonoGame.JSBind_Text 经 [JSImport(module: "text")] 调用；产物 text.js 由 SyncJsEngine 复制。
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
// 字距（Canvas2D 的 letterSpacing 是较新属性，不支持的浏览器直接忽略，退回默认字距）
function applyLetterSpacing(c, letterSpacing) {
    if (!('letterSpacing' in c))
        return;
    c.letterSpacing =
        letterSpacing !== 0 ? `${letterSpacing}px` : '0px';
}
// out: [0]=advance(宽) [1]=总高 [2]=基线以上高度(ascent)
export function measure(text, font, letterSpacing, out) {
    const c = ctx2d();
    c.font = font;
    applyLetterSpacing(c, letterSpacing);
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
export function render(text, font, letterSpacing, x, y, width, height, rgba) {
    canvas.width = width;
    canvas.height = height;
    const c = ctx2d();
    c.clearRect(0, 0, width, height);
    c.font = font;
    applyLetterSpacing(c, letterSpacing);
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
// 自定义字体：把 ttf/otf/woff 注册进 document.fonts，之后即可像系统字体那样用 family 名光栅化。
// 注册失败（URL 不可达 / 字节不是合法字体）返回 false，C# 侧据此决定是否继续建 SpriteFont。
export async function loadFontFromUrl(family, url) {
    try {
        const face = new FontFace(family, `url(${url})`);
        await face.load();
        document.fonts.add(face);
        return true;
    }
    catch (e) {
        console.warn('[text] 字体加载失败:', family, url, e);
        return false;
    }
}
export async function loadFontFromBytes(family, bytes) {
    try {
        // 复制一份字节：FontFace 内部是异步解析的，源缓冲被回收会导致解析失败
        const copy = new Uint8Array(bytes);
        const face = new FontFace(family, copy.buffer);
        await face.load();
        document.fonts.add(face);
        return true;
    }
    catch (e) {
        console.warn('[text] 字体注册失败:', family, e);
        return false;
    }
}
