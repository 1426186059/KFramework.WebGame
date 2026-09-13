// mirengine/render/webgl/webgl2d.ts
// WebGL 绘制后端（mir.createImage / disposeImage / drawImage / drawBatch / fillRect / drawText /
// measureText / clear / setStatus / drawLabel / drawTextBox）。对应 C# 端 MirClient/MirCanvas.cs。
// 精灵上传即预乘 alpha；文本先在隐藏 2D 画布栅格化再上传为纹理。
import { dom, gfx, toCss } from '../../shared.js';
import { ensureGL, resolveSource, drawTexQuad, drawSolidQuad } from './glcore.js';

/** 一片 RGBA 像素（C# byte[] 跨边界后是 Uint8Array；Canvas 侧产出 Uint8ClampedArray）。 */
type RgbaPixels = Uint8Array | Uint8ClampedArray;

/**
 * 上传 RGBA 像素为一个纹理句柄（同名 key 会先释放旧纹理，避免 GPU 纹理泄漏）。
 * @param key 纹理句柄 id
 * @param rgba 像素数据，长度需 = w * h * 4
 * @param w 宽
 * @param h 高
 */
export const createImage = (key: number, rgba: RgbaPixels, w: number, h: number): void => {
    try {
        const gl = dom.gl;
        if (!gl) return;
        ensureGL();
        // 同一 key 重传时，先释放旧 GL 纹理，避免每帧 drawTextBox/drawLabel 造成的 GPU 纹理泄漏（真实 GPU 约 15s OOM 卡崩）。
        const exist = gfx.textures.get(key);
        if (exist && exist.tex) { try { gl.deleteTexture(exist.tex); } catch (e) { } }
        const t = gl.createTexture();
        gl.bindTexture(gl.TEXTURE_2D, t);
        gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL, true);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA8, w, h, 0, gl.RGBA, gl.UNSIGNED_BYTE, rgba);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
        gfx.textures.set(key, { tex: t, w, h });
    } catch (e) {
        console.error('[gl] createImage failed', e);
    }
};

/**
 * 释放纹理句柄对应的 GL 纹理。
 * @param key 纹理句柄 id
 */
export const disposeImage = (key: number): void => {
    try {
        const t = gfx.textures.get(key);
        if (t && dom.gl) dom.gl.deleteTexture(t.tex);
        gfx.textures.delete(key);
    } catch (e) { }
};

/**
 * 把源矩形绘制到目标矩形。
 * @param key 纹理 / 离屏句柄
 * @param sx 源矩形 x
 * @param sy 源矩形 y
 * @param sw 源矩形宽（<=0 时跳过）
 * @param sh 源矩形高（<=0 时跳过）
 * @param dx 目标 x
 * @param dy 目标 y
 * @param dw 目标宽
 * @param dh 目标高
 */
export const drawImage = (key: number, sx: number, sy: number, sw: number, sh: number, dx: number, dy: number, dw: number, dh: number): void => {
    const src = resolveSource(key);
    if (src === undefined || src === null || sw <= 0 || sh <= 0) return;
    drawTexQuad(src, sx, sy, sw, sh, dx, dy, dw, dh, 1, true);
};

// 批命令模式：cmd 为 Int32Array，每条 9 个 int
/**
 * 批量绘制：避免逐条跨界调用。每条命令 = 9 个 int。
 * @param cmd 命令缓冲：[key, sx, sy, sw, sh, dx, dy, dw, dh] * count
 * @param count 命令条数
 */
export const drawBatch = (cmd: Int32Array, count: number): void => {
    for (let i = 0; i < count; i++) {
        const o = i * 9;
        const src = resolveSource(cmd[o]);
        if (src === undefined || src === null) continue;
        drawTexQuad(src, cmd[o + 1], cmd[o + 2], cmd[o + 3], cmd[o + 4],
            cmd[o + 5], cmd[o + 6], cmd[o + 7], cmd[o + 8], 1, true);
    }
};

/**
 * 纯色填充矩形。
 * @param x 左上角 x
 * @param y 左上角 y
 * @param w 宽
 * @param h 高
 * @param argb 颜色，32 位 ARGB
 */
export const fillRect = (x: number, y: number, w: number, h: number, argb: number): void => {
    drawSolidQuad(x, y, w, h, argb);
};

// 文本栅格化到隐藏 2D 画布，再作为纹理绘制。语义对齐 Canvas2D 的 fillText（基线对齐字基线）。
/**
 * 绘制单行文本（基线落在 (x, y)）。
 * @param text 文本内容（空则不绘制）
 * @param x 文字基线 x
 * @param y 文字基线 y
 * @param argb 颜色，32 位 ARGB
 * @param font CSS 字体串
 */
export const drawText = (text: string, x: number, y: number, argb: number, font: string): void => {
    try {
        if (!text) return;
        const gl = dom.gl;
        if (!gl) return;
        ensureGL();
        const c = dom.scratchCtx;
        c.font = font;
        c.textBaseline = 'alphabetic';
        const m = c.measureText(text);
        const fs = parseInt(String((font.match(/(\d+)(px|pt)/) || [])[1] || 12));
        const w = Math.max(1, Math.ceil(m.width));
        const h = Math.max(1, Math.ceil(fs * 1.35));
        const pad = 2;
        const tw = w + pad * 2, th = h + pad * 2;
        dom.scratch.width = tw; dom.scratch.height = th;
        const c2 = dom.scratchCtx;
        c2.clearRect(0, 0, tw, th);
        c2.font = font;
        c2.textBaseline = 'alphabetic';
        c2.fillStyle = toCss(argb);
        const ascent = (m.actualBoundingBoxAscent) || Math.round(fs * 0.8);
        c2.fillText(text, pad, pad + ascent);
        const img = c2.getImageData(0, 0, tw, th);
        const t = gl.createTexture();
        gl.bindTexture(gl.TEXTURE_2D, t);
        gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL, true);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA8, tw, th, 0, gl.RGBA, gl.UNSIGNED_BYTE, img.data);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
        // 纹理顶部（基线上方 pad+ascent 处）对齐到画布 y，使基线落在 (x, y)。
        drawTexQuad({ tex: t, w: tw, h: th }, 0, 0, tw, th, x - pad, y - pad - ascent, tw, th, 1, true);
        gl.deleteTexture(t);
    } catch (e) {
        console.error('[gl] drawText failed', e);
    }
};

/**
 * 量算文本宽度。
 * @param text 文本内容
 * @param font CSS 字体串
 * @returns 宽度（向上取整）；失败返回 0
 */
export const measureText = (text: string, font: string): number => {
    try {
        if (!dom.measureCtx) return 0;
        dom.measureCtx.font = font;
        return Math.ceil(dom.measureCtx.measureText(text).width);
    } catch (e) { return 0; }
};

/**
 * 用纯色清空主画布。
 * @param argb 颜色，32 位 ARGB
 */
export const clear = (argb: number): void => {
    try {
        const gl = dom.gl;
        if (!gl) return;
        gl.bindFramebuffer(gl.FRAMEBUFFER, null);
        gl.viewport(0, 0, dom.canvas.width, dom.canvas.height);
        gl.disable(gl.BLEND);
        gl.clearColor(((argb >> 16) & 255) / 255, ((argb >> 8) & 255) / 255, (argb & 255) / 255, ((argb >>> 24) & 255) / 255);
        gl.clear(gl.COLOR_BUFFER_BIT);
        gl.enable(gl.BLEND);
    } catch (e) {
        console.error('[gl] clear failed', e);
    }
};

/**
 * 更新右侧状态面板的 HTML。
 * @param html HTML 片段
 */
export const setStatus = (html: string): void => { dom.panel.innerHTML = html; };

// —— 以下 drawLabel / drawTextBox 复用 Canvas2D 文本栅格化逻辑，仅 createImage 改为 GL 上传 ——

// 按宽度折行：英文按空格分词，超长单词/中文（无空格）按字符断行；保留 \n 硬换行。
/**
 * 把一个段落按给定宽度折成多行。
 * @param ctx 2D 上下文（用于 measureText）
 * @param text 原文（支持 \n 硬换行）
 * @param maxWidth 折行宽度
 * @returns 折行后的行数组
 */
function wrapLines(ctx: CanvasRenderingContext2D, text: string, maxWidth: number): string[] {
    const out: string[] = [];
    const paras = (text || '').split('\n');
    for (const para of paras) {
        if (para.length === 0) { out.push(''); continue; }
        let line = '';
        const tokens = para.split(' ');
        for (let i = 0; i < tokens.length; i++) {
            const word = tokens[i];
            const candidate = line === '' ? word : line + ' ' + word;
            if (line === '' || ctx.measureText(candidate).width <= maxWidth) {
                line = candidate;
            } else {
                out.push(line);
                if (ctx.measureText(word).width > maxWidth) {
                    let piece = '';
                    for (const ch of word) {
                        if (piece !== '' && ctx.measureText(piece + ch).width > maxWidth) { out.push(piece); piece = ch; }
                        else piece += ch;
                    }
                    line = piece;
                } else {
                    line = word;
                }
            }
        }
        out.push(line);
    }
    return out;
}

// 在离屏画布上渲染文字（多行折行/描边/渐变/对齐），取 RGBA 后注册为纹理句柄。
// 对应 C# 端 MirEngine.BrowserCanvas.DrawLabel。
// format 位：HorizontalCenter=1, Right=2, VerticalCenter=4, Bottom=8, WordBreak=16。
/**
 * 把一块标签文字渲染成纹理句柄。
 * @param handle 目标纹理句柄 id
 * @param w 标签宽
 * @param h 标签高
 * @param text 文本内容
 * @param fontCss CSS 字体串
 * @param foreArgb 前景色，32 位 ARGB
 * @param outlineArgb 描边色；<0 表示不描边
 * @param format 对齐位标志：水平居中=1 / 右对齐=2 / 垂直居中=4 / 底部=8 / 自动折行=16
 * @param backArgb 背景色；<0 表示不填充背景
 * @param gradTopArgb 渐变起始色（gradient 为真时生效）
 * @param gradBottomArgb 渐变结束色
 * @param gradient 是否用竖向渐变填充文字
 */
export const drawLabel = (handle: number, w: number, h: number, text: string, fontCss: string, foreArgb: number, outlineArgb: number, format: number, backArgb: number, gradTopArgb: number, gradBottomArgb: number, gradient: boolean): void => {
    try {
        w = Math.max(1, w | 0); h = Math.max(1, h | 0);
        const cv = document.createElement('canvas');
        cv.width = w; cv.height = h;
        const c = cv.getContext('2d');
        if (!c) return; // 拿不到 2D 上下文（理论上不会发生）时放弃本次绘制
        c.clearRect(0, 0, w, h);
        if (backArgb >= 0) { c.fillStyle = toCss(backArgb); c.fillRect(0, 0, w, h); }
        if (!text) { createImage(handle, new Uint8ClampedArray(c.getImageData(0, 0, w, h).data), w, h); return; }

        c.font = fontCss;
        c.textBaseline = 'top';
        const fs = parseInt(String((fontCss.match(/(\d+)(px|pt)/) || [])[1] || 12));
        const _fm = c.measureText('Mg');
        const lineHeight = Math.max(1, Math.round((_fm.fontBoundingBoxAscent || fs) + (_fm.fontBoundingBoxDescent || fs * 0.3)));
        const horizontalCenter = (format & 1) !== 0;
        const right = (format & 2) !== 0;
        const verticalCenter = (format & 4) !== 0;
        const bottom = (format & 8) !== 0;
        const wordBreak = (format & 16) !== 0;
        const pad = 1;

        const lines = wordBreak ? wrapLines(c, text, Math.max(1, w - pad * 2)) : (text || '').split('\n');
        const totalHeight = lines.length * lineHeight;

        let startY: number;
        if (verticalCenter) startY = Math.max(0, (h - totalHeight) / 2);
        else if (bottom) startY = Math.max(0, h - totalHeight);
        else startY = 0;
        if (!bottom) startY += Math.max(1, Math.round(fs * 0.1));

        const ox = outlineArgb >= 0 ? 1 : 0;
        const oy = outlineArgb >= 0 ? 1 : 0;
        const drawText = (line: string, lx: number, ly: number, argb: number): void => { c.fillStyle = toCss(argb); c.fillText(line, lx, ly); };

        for (let i = 0; i < lines.length; i++) {
            const line = lines[i];
            const lw = c.measureText(line).width;
            let lx = pad;
            if (horizontalCenter) lx = Math.max(pad, (w - lw) / 2);
            else if (right) lx = Math.max(pad, w - lw - pad);

            const lyBase = startY + i * lineHeight;
            if (outlineArgb >= 0) {
                drawText(line, lx + 1, lyBase, outlineArgb);
                drawText(line, lx, lyBase + 1, outlineArgb);
                drawText(line, lx + 2, lyBase + 1, outlineArgb);
                drawText(line, lx + 1, lyBase + 2, outlineArgb);
            }
            const ly = lyBase + oy;
            if (gradient) {
                const g = c.createLinearGradient(0, 0, 0, h);
                g.addColorStop(0, toCss(gradTopArgb));
                g.addColorStop(1, toCss(gradBottomArgb));
                c.fillStyle = g;
                c.fillText(line, lx + ox, ly);
            } else {
                drawText(line, lx + ox, ly, foreArgb);
            }
        }

        createImage(handle, new Uint8ClampedArray(c.getImageData(0, 0, w, h).data), w, h);
    } catch (e) { console.error('[gl] drawLabel failed', e); }
};

// 文本框文字渲染：背景 + 选择高亮矩形 + 文本 + 光标竖条。对应 C# 端 MirEngine.BrowserCanvas.DrawTextBox。
/**
 * 把一个单行文本框渲染成纹理句柄。
 * @param handle 目标纹理句柄 id
 * @param w 宽
 * @param h 高
 * @param text 文本内容
 * @param fontCss CSS 字体串
 * @param foreArgb 前景（文字）色，32 位 ARGB
 * @param backArgb 背景色；<0 不填充
 * @param selBackArgb 选区背景色；<0 不画选区
 * @param caretArgb 光标颜色
 * @param selStart 选区起始字符下标
 * @param selLength 选区长度（<=0 表示无选区）
 * @param caretPos 光标所在字符下标
 * @param caretVisible 是否绘制光标竖条
 * @param verticalCenter 文本是否垂直居中
 */
export const drawTextBox = (handle: number, w: number, h: number, text: string, fontCss: string, foreArgb: number, backArgb: number, selBackArgb: number, caretArgb: number, selStart: number, selLength: number, caretPos: number, caretVisible: boolean, verticalCenter: boolean): void => {
    try {
        w = Math.max(1, w | 0); h = Math.max(1, h | 0);
        const cv = document.createElement('canvas');
        cv.width = w; cv.height = h;
        const c = cv.getContext('2d');
        if (!c) return; // 拿不到 2D 上下文（理论上不会发生）时放弃本次绘制
        c.clearRect(0, 0, w, h);
        if (backArgb >= 0) { c.fillStyle = toCss(backArgb); c.fillRect(0, 0, w, h); }
        if (!text) { createImage(handle, new Uint8ClampedArray(c.getImageData(0, 0, w, h).data), w, h); return; }

        c.font = fontCss;
        c.textBaseline = 'top';
        const fs = parseInt(String((fontCss.match(/(\d+)(px|pt)/) || [])[1] || 12));
        const _fm = c.measureText('Mg');
        const lineHeight = Math.max(1, Math.round((_fm.fontBoundingBoxAscent || fs) + (_fm.fontBoundingBoxDescent || fs * 0.3)));
        const padX = 1;
        let top = verticalCenter ? Math.max(0, (h - lineHeight) / 2) : 0;
        top += Math.max(1, Math.round(fs * 0.1));
        const xOf = (i: number): number => padX + c.measureText(text.substring(0, i)).width;

        if (selLength > 0 && selBackArgb >= 0) {
            const s = Math.min(selStart, selStart + selLength);
            const e = Math.max(selStart, selStart + selLength);
            const sx = xOf(s), ex = xOf(e);
            c.fillStyle = toCss(selBackArgb);
            c.fillRect(sx, top, Math.max(1, ex - sx), lineHeight);
        }

        c.fillStyle = toCss(foreArgb);
        c.fillText(text, padX, top);

        if (caretVisible) {
            const cx = xOf(caretPos);
            c.fillStyle = toCss(caretArgb);
            c.fillRect(cx, top, Math.max(1, Math.round(fs / 12)), lineHeight);
        }

        createImage(handle, new Uint8ClampedArray(c.getImageData(0, 0, w, h).data), w, h);
    } catch (e) { console.error('[gl] drawTextBox failed', e); }
};
