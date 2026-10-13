// 【依赖 C#】由 KFramework.MonoGame.JSBind_Text 经 [JSImport(module: "text")] 调用；产物 text.js 由 SyncJsEngine 复制。
// 文字光栅化：用 Canvas2D 把字形画进字形图集，C# 侧只负责上传像素。
const canvas = document.createElement('canvas');
const context = canvas.getContext('2d', { willReadFrequently: true });
function ctx2d() {
    if (!context)
        throw new Error('[text] 无法创建 Canvas2D 上下文');
    return context;
}
// 度量结果的 short 暂存 + 它的字节视图（复用同一块，零分配）。
// 为什么绕一层 byte：MemoryView 既没有 [] 索引器、也没有单元素 set(i, v)，只能 set(TypedArray, offset)；
// 而 .NET 的 MemoryView 编组只支持 Span<byte> / Span<int>（Span<short> 过不了 source generator），
// 所以这里把 4 个 short 拼成 8 字节再整体拷过去（WASM 是小端，与 C# 侧 MemoryMarshal.Cast 一致）。
const _metrics = new Int16Array(4);
const _metricBytes = new Uint8Array(_metrics.buffer);
/** short 上限：像素量到这个量级已是病态字号，夹住即可（避免静默截断破坏字形盒子）。 */
const SHORT_MAX = 32767;
// 字距（Canvas2D 的 letterSpacing 是较新属性，不支持的浏览器直接忽略，退回默认字距）
function applyLetterSpacing(c, letterSpacing) {
    if (!('letterSpacing' in c))
        return;
    c.letterSpacing =
        letterSpacing !== 0 ? `${letterSpacing}px` : '0px';
}
// out: [0]=advance(宽) [1]=总高 [2]=基线以上高度(ascent) [3]=保留
// 四个值都是像素量（与 SpriteFont.Size 同量纲），故用 short 传递即可；写入见下方 out.set。
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
    // advance 是文字布局用的字形推进宽度，必须贴近 Canvas2D 真实 metrics.width，
    // 绝不能额外 +1——否则每个字符都多出 1px 推进（约等于全局 1px 字距），
    // 长行会显著比 GDI 宽、撑爆 UI。格子额外留白由 SpriteFont 的 Padding/cellWidth 负责，与布局 advance 解耦。
    _metrics[0] = Math.min(SHORT_MAX, Math.max(1, Math.ceil(metrics.width)));
    _metrics[1] = Math.min(SHORT_MAX, Math.max(1, Math.ceil(ascent + descent) + 4));
    _metrics[2] = Math.min(SHORT_MAX, Math.max(1, Math.ceil(ascent) + 2));
    _metrics[3] = 0;
    out.set(_metricBytes, 0);
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
    // getImageData 返回的是 Uint8ClampedArray，而 .NET 的 MemoryView_Span 只接受 Uint8Array
    const image = c.getImageData(0, 0, width, height).data;
    const bytes = new Uint8Array(image.buffer, image.byteOffset, image.byteLength);
    rgba.set(bytes);
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
