// mirengine/render/webgl/webgl-engine.ts
// WebGL 控件树引擎（DXControl 后端）。对应 JSBind/BrowserCanvas.cs（mir.cr* 函数）。
// 离屏画布 / RenderTarget 经 gfx.offscreens 管理；精灵纹理由 webgl/webgl2d.js 写入 gfx.textures 后此处读取。
//
// 重要：这些函数都通过 .NET WASM 的 [JSImport] 调用。若 JS 侧抛出异常，WASM 运行时无法把异常
// 传回托管代码，会直接在 WASM 里执行 `unreachable` 终止整个实例（RuntimeError: unreachable）。
// 因此每个函数都必须保证不向外抛异常（加 gfx.cur 守卫 + try/catch 兜底）。
import { dom, gfx } from '../../shared.js';
import { ensureGL, resolveSource, drawTexQuad, drawTexQuadTransform, drawSolidQuad, drawSolidQuadPts } from './glcore.js';
/**
 * 创建离屏绘制目标（RenderTarget）。
 * @param w 宽（像素）
 * @param h 高（像素）
 * @returns 离屏句柄 id；失败返回 0
 */
export const crCreateOffscreen = (w, h) => {
    try {
        const gl = dom.gl;
        if (!gl)
            return 0;
        ensureGL();
        w = Math.max(1, w | 0);
        h = Math.max(1, h | 0);
        const tex = gl.createTexture();
        gl.bindTexture(gl.TEXTURE_2D, tex);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA8, w, h, 0, gl.RGBA, gl.UNSIGNED_BYTE, null);
        const fbo = gl.createFramebuffer();
        gl.bindFramebuffer(gl.FRAMEBUFFER, fbo);
        gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, tex, 0);
        gl.bindFramebuffer(gl.FRAMEBUFFER, null);
        const id = gfx.nextOffId++;
        gfx.offscreens.set(id, { fbo, tex, w, h });
        return id;
    }
    catch (e) {
        console.error('[gl] createOffscreen failed', e);
        return 0;
    }
};
/**
 * 切换当前绘制目标。
 * @param id 离屏句柄；0 或未知 id 一律回退到主画布
 */
export const crSetTarget = (id) => {
    try {
        // 未知 id（或非法句柄）一律回退到主画布，避免 gfx.cur 变 undefined 导致后续 clear/draw 崩溃。
        gfx.cur = id === 0 ? gfx.mainTarget : (gfx.offscreens.get(id) || gfx.mainTarget);
        // 切换绘制表面时复位混合模式：混合状态是全局的，若上一次绘制（如灯光 LIGHTMAP/特效）
        // 留下非 source-over 且本次未在当前表面显式 SetBlend 复位，会导致后续绘制被错误混合而失踪。
        gfx.blendOp = 'source-over';
    }
    catch (e) {
        gfx.cur = gfx.mainTarget;
    }
};
// 混合表分两套。Crystal(Mir2) 和 Zircon(Mir3) 的原版 D3D9 SetBlend 语义完全不同，
// 共用一张表会把 Mir2 的加色特效当成 screen 混合 —— 画面就是"黑底 + 亮块"。
//   crystal：原版 Crystal 客户端（Client/MirGraphics/DXManager.cs:353）
//     除 INVLIGHT 外一律 SourceBlend=SourceAlpha / DestinationBlend=One，即加色；
//     Blending=false 时走 SpriteFlags.AlphaBlend，即标准 alpha。
//   zircon：Mir3 RenderingCore 的 D3D11 混合状态表
//     LIGHTMAP(12)=multiply / COLORFY(8)、HIGHLIGHT(10)=lighter / LIGHT、NORMAL 等=screen
//     NONE(-1) / Blending=false = source-over（标准 alpha）
let blendProfile = 'zircon';
/**
 * 选择混合表（由 BrowserCanvas.SetBlendProfile 调用）。
 * @param name "crystal"（Mir2）或 "zircon"（Mir3，默认）
 */
export const setBlendProfile = (name) => {
    blendProfile = String(name).toLowerCase() === 'crystal' ? 'crystal' : 'zircon';
};
/**
 * 设置当前混合模式。
 * @param mode 原版 D3D 混合模式序号（见下方表的 case 注释）
 * @param rate 混合因子 0..1（INVLIGHT / HIGHLIGHT 用）
 * @param enabled Blending 总开关；false 一律回落到标准 alpha 混合
 */
export const crSetBlend = (mode, rate, enabled) => {
    try {
        gfx.blendRate = rate;
        if (!enabled) {
            gfx.blendOp = 'source-over';
            return;
        }
        if (blendProfile === 'crystal') {
            // 纹理已预乘 alpha，配合 (ONE, ONE) 因子恰好等价于 D3D9 的 SrcAlpha*Color + Dst（加色）。
            switch (mode) {
                case 4:
                    gfx.blendOp = 'invlight';
                    break; // INVLIGHT: BlendFactor / InverseSourceColor
                default:
                    gfx.blendOp = 'lighter';
                    break; // 其余模式在原版都落到 default：加色
            }
            return;
        }
        switch (mode) {
            case 12:
                gfx.blendOp = 'multiply';
                break; // LIGHTMAP
            case 8:
                gfx.blendOp = 'lighter';
                break; // COLORFY
            case 10:
                gfx.blendOp = 'lighter';
                break; // HIGHLIGHT
            case 1: // LIGHT
            case 2: // LIGHTINV
            case 0: // NORMAL（Blending=true 时 DX 走 screen）
            case 3: // INVNORMAL
            case 5: // INVLIGHTINV
            case 6: // INVCOLOR
            case 7: // INVBACKGROUND
                gfx.blendOp = 'screen';
                break;
            case 4: // INVLIGHT
            case 9: // MASK
            case 11: // EFFECTMASK
                gfx.blendOp = 'lighter';
                break;
            case -1: // NONE
            default:
                gfx.blendOp = 'source-over';
                break;
        }
    }
    catch (e) {
        gfx.blendOp = 'source-over';
    }
};
/**
 * 清空当前目标。
 * @param r 红 0..255
 * @param g 绿 0..255
 * @param b 蓝 0..255
 * @param a 透明 0..255
 */
export const crClear = (r, g, b, a) => {
    try {
        const gl = dom.gl;
        if (!gl)
            return;
        const tgt = gfx.cur || gfx.mainTarget;
        if (tgt.fbo) {
            gl.bindFramebuffer(gl.FRAMEBUFFER, tgt.fbo);
            gl.viewport(0, 0, tgt.w, tgt.h);
        }
        else {
            gl.bindFramebuffer(gl.FRAMEBUFFER, null);
            gl.viewport(0, 0, dom.canvas.width, dom.canvas.height);
        }
        gl.disable(gl.BLEND);
        gl.clearColor(r / 255, g / 255, b / 255, a / 255);
        gl.clear(gl.COLOR_BUFFER_BIT);
        gl.enable(gl.BLEND);
    }
    catch (e) {
        console.error('[gl] crClear failed', e);
    }
};
/**
 * 把纹理的源矩形绘制到目标矩形。
 * @param tex 纹理 / 离屏句柄
 * @param sx 源矩形 x
 * @param sy 源矩形 y
 * @param sw 源矩形宽（<=0 时跳过）
 * @param sh 源矩形高（<=0 时跳过）
 * @param dx 目标 x（可为浮点）
 * @param dy 目标 y
 * @param dw 目标宽
 * @param dh 目标高
 * @param colorArgb 颜色，高 8 位作为整体 alpha
 */
export const crDraw = (tex, sx, sy, sw, sh, dx, dy, dw, dh, colorArgb) => {
    try {
        const src = resolveSource(tex);
        if (!src || sw <= 0 || sh <= 0)
            return;
        let al = ((colorArgb >>> 24) & 0xFF) / 255;
        // HIGHLIGHT: 结果 = BlendFactor*Src + Dst，用 alpha 承载 rate
        if (gfx.blendOp === 'lighter' && gfx.blendRate !== 1)
            al *= gfx.blendRate;
        drawTexQuad(src, sx, sy, sw, sh, dx, dy, dw, dh, al, true);
    }
    catch (e) {
        console.error('[gl] crDraw failed', e);
    }
};
// 矩阵变换绘制：把源矩形 (sx,sy,sw,sh) 当作基础几何 (0,0,sw,sh)，经 2D 仿射矩阵变换后再绘制。
/**
 * 矩阵变换绘制（语义对齐原版 DrawTexture 的 transform / center / translation 契约）。
 * @param tex 纹理 / 离屏句柄
 * @param sx 源矩形 x
 * @param sy 源矩形 y
 * @param sw 源矩形宽
 * @param sh 源矩形高
 * @param m11 仿射矩阵 [0][0]
 * @param m12 仿射矩阵 [1][0]
 * @param m21 仿射矩阵 [0][1]
 * @param m22 仿射矩阵 [1][1]
 * @param m31 平移 x
 * @param m32 平移 y
 * @param colorArgb 颜色，高 8 位作为整体 alpha
 */
export const crDrawTransform = (tex, sx, sy, sw, sh, m11, m12, m21, m22, m31, m32, colorArgb) => {
    try {
        const src = resolveSource(tex);
        if (!src || sw <= 0 || sh <= 0)
            return;
        let al = ((colorArgb >>> 24) & 0xFF) / 255;
        if (gfx.blendOp === 'lighter' && gfx.blendRate !== 1)
            al *= gfx.blendRate;
        drawTexQuadTransform(src, sx, sy, sw, sh, m11, m12, m21, m22, m31, m32, al, true);
    }
    catch (e) {
        console.error('[gl] crDrawTransform failed', e);
    }
};
/**
 * 量算文本尺寸。
 * @param text 待量算的文本
 * @param fontCss CSS 字体串（含 px/pt 字号）
 * @param maxWidth 最大宽度（当前未用于折行计算，仅由调用方约束）
 * @returns "宽,高" 字符串；失败返回 "0,12"
 */
export const crMeasureText = (text, fontCss, maxWidth) => {
    try {
        if (!dom.measureCtx)
            return '0,12';
        dom.measureCtx.font = fontCss;
        const m = dom.measureCtx.measureText(text);
        const fs = parseInt(String((fontCss.match(/(\d+)(px|pt)/) || [])[1] || 12));
        return `${Math.ceil(m.width)},${Math.ceil(fs * 1.3)}`;
    }
    catch (e) {
        return '0,12';
    }
};
/**
 * 纯色填充矩形。
 * @param x 左上角 x
 * @param y 左上角 y
 * @param w 宽
 * @param h 高
 * @param colorArgb 颜色，32 位 ARGB
 */
export const crFillRect = (x, y, w, h, colorArgb) => {
    drawSolidQuad(x, y, w, h, colorArgb);
};
/**
 * 画线（退化为沿法线外扩的实心四边形）。
 * @param x1 起点 x
 * @param y1 起点 y
 * @param x2 终点 x
 * @param y2 终点 y
 * @param w 线宽
 * @param colorArgb 颜色，32 位 ARGB
 */
export const crDrawLine = (x1, y1, x2, y2, w, colorArgb) => {
    try {
        const dx = x2 - x1, dy = y2 - y1;
        const len = Math.hypot(dx, dy) || 1;
        const nx = -dy / len * (w / 2), ny = dx / len * (w / 2);
        drawSolidQuadPts([
            [x1 + nx, y1 + ny],
            [x2 + nx, y2 + ny],
            [x1 - nx, y1 - ny],
            [x2 - nx, y2 - ny],
        ], colorArgb);
    }
    catch (e) {
        console.error('[gl] crDrawLine failed', e);
    }
};
/** 立即提交（当前 WebGL 后端无需显式 flush）。 */
export const crFlush = () => { };
