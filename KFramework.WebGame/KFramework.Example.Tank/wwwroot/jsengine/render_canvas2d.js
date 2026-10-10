// 【依赖 C#】由 KFramework.MonoGame.JSBind_Canvas2D 经 [JSImport(module: "render_canvas2d")] 调用；
// 产物 render_canvas2d.js 由各示例 SyncJsEngine 复制到 wwwroot/jsengine。
//
// Canvas2D 渲染后端：不碰 WebGL / WebGPU，直接把精灵批次"回放"成 Canvas2D 的 drawImage。
// 定位是【功能对照 / 兜底后端】，不是性能路径 —— 适合验证排版、字形、混合与整体流程。
//
// 与 render_webgl20 的分工（保持一致）：
//   · 画布元素仍归 html_canvas 管，本模块只负责 getContext('2d') 与绘制；
//   · 没有着色器：C# 传来的矩阵在 C# 侧就被还原成"局部像素 → 画布像素"的仿射，这里只 setTransform；
//   · 顶点批次（VertexPositionColorTexture，28 字节/顶点）在这里解码成四边形：每个四边形用一个 2×3 仿射
//     把【纹理单位方格 uv∈[0,1]²】映射到该四边形，然后 drawImage(整张纹理, → 单位方格)。
//     旋转 / 缩放 / 翻转 / 图集子图（UV 矩形）全都由这个仿射自动覆盖，不需要任何分支。
import { getCanvas } from './html_canvas.js';
import { ByteCache } from './custom_data_byte_cache.js';
/** 单个顶点的字节数（VertexPositionColorTexture：pos 4×f32 + color 4×u8 + uv 2×f32）。 */
const VERTEX_STRIDE = 28;
/** 顶点数据用的字节缓冲（与 WebGL 后端的顶点缓冲同量级：4096 四边形 × 4 × 28B ≈ 448KB）。 */
const _vertexCache = new ByteCache(64 * 1024, 1024 * 1024);
/** 着色副本缓存上限（Canvas2D 没有逐绘制着色，只能按颜色预乘一份；满了整体丢弃）。 */
const TINT_CACHE_LIMIT = 8;
/** 主画布（屏幕）。 */
let screen = null;
/** 当前绘制目标（未绑定时 = 主画布）。 */
let current = null;
/** 渲染目标表：句柄 → Surface。这些 canvas 同时登记在 textures 里，因此能直接被 drawImage 采样。 */
const targets = new Map();
/** 纹理表：C# 的 Texture2D.Handle（int）→ 承载像素的离屏 canvas + 尺寸。 */
const textures = new Map();
const texSizes = new Map();
/** 着色副本：(纹理 id|RGB) → 已着色的离屏 canvas。 */
const tintedCache = new Map();
/** 当前绑定的纹理 id。 */
let currentTexture = 0;
/** 当前"局部像素 → 画布像素"仿射（由 C# 侧从投影矩阵还原，见 Canvas2DShaderProgram.Apply）。 */
let m = { a: 1, b: 0, c: 0, d: 1, e: 0, f: 0 };
/** 逐批应用的状态（不直接写 ctx：scissor 的 save/restore 会把这些状态一起还原，故每次绘制前重设）。 */
let blend = 0; // 0=source-over 1=lighter 2=source-over(不透明) 3=multiply
let smoothing = false;
/** 建一块 Surface：设好初始变换并压入基准 save（setScissor 的 restore 要回到这一层）。 */
function makeSurface(id, canvas, context) {
    context.setTransform(1, 0, 0, 1, 0, 0);
    context.save();
    return {
        id,
        canvas,
        ctx: context,
        lastWidth: canvas.width,
        lastHeight: canvas.height,
        scissorActive: false,
    };
}
/**
 * 取当前绘制目标的上下文，并处理一个容易踩的坑：给 canvas.width / height 赋值会<b>重置整个 2D 状态</b>
 * （变换、clip、设置全部回到默认，save 栈也被清空）。引擎每帧可能按 CSS 尺寸同步主画布 backing 尺寸，
 * 所以每次取上下文都检查一次，发现尺寸变了就重建基准 save，避免后续 restore 把栈弹空。
 */
function gpu() {
    const s = current;
    if (!s)
        throw new Error('[canvas2d] 上下文尚未初始化');
    if (s.canvas.width !== s.lastWidth || s.canvas.height !== s.lastHeight) {
        s.lastWidth = s.canvas.width;
        s.lastHeight = s.canvas.height;
        s.scissorActive = false;
        s.ctx.setTransform(1, 0, 0, 1, 0, 0);
        s.ctx.save();
    }
    return s.ctx;
}
function toBytes(view) {
    return _vertexCache.copyFrom(view);
}
// ============ 初始化 / 查询 ============
export function init(antialias) {
    const element = getCanvas();
    if (!element) {
        console.error('[canvas2d] 无法找到画布元素');
        return false;
    }
    const context = element.getContext('2d', { alpha: false });
    if (!context) {
        console.error('[canvas2d] 无法创建 Canvas2D 上下文（该画布已绑定到别的上下文类型？）');
        return false;
    }
    // Canvas2D 的几何抗锯齿由浏览器自行处理，antialias 参数在这里没有对应开关，仅记录。
    context.imageSmoothingEnabled = false;
    // 基准 save：setScissor / clearScissor 依靠 restore 回到这里
    screen = makeSurface(-1, element, context);
    current = screen;
    console.log(`[canvas2d] 就绪 | Canvas2D | 画布 ${element.width}x${element.height} | 请求 MSAA=${antialias}（Canvas2D 不提供开关）`);
    return true;
}
export function getRenderer() { return 'Canvas2D'; }
export function getMaxTextureSize() { return 8192; }
export function getError() { return 0; }
/** Canvas2D 随 rAF 自动呈现，无需收帧动作。 */
export function endFrame() { }
/** 视口在 Canvas2D 下只影响逻辑坐标（C# 侧投影已按视口算好），这里不做处理。 */
export function setViewport(_x, _y, _w, _h) { }
// ============ 裁剪 / 清屏 ============
export function setScissor(x, y, w, h) {
    const c = gpu();
    const s = current;
    if (s.scissorActive)
        c.restore();
    c.save();
    s.scissorActive = true;
    // clip 的路径按当前变换求值，故先在单位变换下建立矩形裁剪
    c.setTransform(1, 0, 0, 1, 0, 0);
    c.beginPath();
    c.rect(x, y, w, h);
    c.clip();
}
export function clearScissor() {
    const s = current;
    if (!s || !s.scissorActive)
        return;
    gpu().restore();
    s.scissorActive = false;
}
export function clear(r, g, b, a) {
    const c = gpu();
    const w = c.canvas.width;
    const h = c.canvas.height;
    c.save();
    c.setTransform(1, 0, 0, 1, 0, 0);
    c.globalAlpha = 1;
    if (a <= 0) {
        // 全透明清屏 = 擦除（与 WebGL 清成 (0,0,0,0) 后再由浏览器按不透明合成一致）
        c.globalCompositeOperation = 'source-over';
        c.clearRect(0, 0, w, h);
    }
    else if (a >= 1) {
        c.globalCompositeOperation = 'source-over';
        c.fillStyle = rgbOf(r, g, b);
        c.fillRect(0, 0, w, h);
    }
    else {
        // 半透明清屏：相当于整体替换成该颜色（copy 忽略目标既有像素）
        c.globalCompositeOperation = 'copy';
        c.globalAlpha = a;
        c.fillStyle = rgbOf(r, g, b);
        c.fillRect(0, 0, w, h);
    }
    c.restore();
}
function rgbOf(r, g, b) {
    return `rgb(${Math.round(r * 255)},${Math.round(g * 255)},${Math.round(b * 255)})`;
}
// ============ 状态 ============
/** 混合模式：0=普通 alpha 混合 1=加色 2=不透明 3=正片叠底。 */
export function setBlendState(kind) { blend = kind | 0; }
export function setSampler(linear) { smoothing = !!linear; }
function compositeOf() {
    switch (blend) {
        case 1: return 'lighter';
        case 3: return 'multiply';
        default: return 'source-over';
    }
}
/** 绑定当前纹理（C# 每次切批都会调）。 */
export function bindTexture(id) { currentTexture = id | 0; }
/**
 * 投影（更准确地说：局部像素 → 画布像素的仿射）。
 * C# 侧已经把 view = projection × P⁻¹ 算好，这里只存 2×3。
 */
export function setProjection(a, b, c, d, e, f) {
    m = { a, b, c, d, e, f };
}
// ============ 纹理 ============
export function createTexture(id, w, h) {
    const canvas = document.createElement('canvas');
    canvas.width = Math.max(1, w | 0);
    canvas.height = Math.max(1, h | 0);
    const c = canvas.getContext('2d');
    if (c)
        c.imageSmoothingEnabled = false;
    textures.set(id, canvas);
    texSizes.set(id, { w: canvas.width, h: canvas.height });
    tintedCache.clear();
}
export function deleteTexture(id) {
    // 渲染目标同时登记在"目标表"与"纹理表"里（RenderTarget2D.Dispose → DeleteTexture 会走到这里），
    // 故一并摘掉；普通纹理在 targets 里查不到，删了也没影响。
    if (current && current.id === id)
        bindRenderTarget(MAIN_SURFACE_ID);
    targets.delete(id);
    textures.delete(id);
    texSizes.delete(id);
    tintedCache.clear();
}
// ============ 渲染目标（离屏 canvas）============
/** 主画布（屏幕）的目标句柄。 */
const MAIN_SURFACE_ID = -1;
/**
 * 建一个渲染目标：本质是"再要一张离屏 canvas"，同一 id 同时登记进目标表与纹理表 ——
 * 既能被画进去（<see cref="bindRenderTarget"/>），也能当普通纹理被 drawImage 采样（上屏 / 当中间图）。
 * 与 createTexture 的两点差别：① 上下文必须带 alpha（离屏分层靠透明层叠加合成）；
 * ② 不参与 putImageData 上传（它的内容只能靠绘制产生）。
 */
export function createRenderTarget(id, w, h) {
    const canvas = document.createElement('canvas');
    canvas.width = Math.max(1, w | 0);
    canvas.height = Math.max(1, h | 0);
    const context = canvas.getContext('2d');
    if (!context) {
        console.error('[canvas2d] 无法为渲染目标创建 2D 上下文');
        return;
    }
    context.imageSmoothingEnabled = false;
    textures.set(id, canvas);
    texSizes.set(id, { w: canvas.width, h: canvas.height });
    targets.set(id, makeSurface(id, canvas, context));
    tintedCache.clear();
}
/** 绑定渲染目标：把后续绘制切到该目标的 canvas；id &lt; 0 或查不到 → 回主画布。 */
export function bindRenderTarget(id) {
    if (!screen)
        throw new Error('[canvas2d] 上下文尚未初始化');
    const next = id < 0 ? screen : (targets.get(id) ?? screen);
    if (next === current)
        return;
    // 离开旧目标前先撤掉它上面的裁剪：clip 是每上下文各自的状态，留着的话下次切回该目标会带着旧裁剪。
    if (current && current.scissorActive)
        clearScissor();
    current = next;
    // 新目标的变换/裁剪栈可能是很久以前留下的（drawBatch 每次都重设变换与合成算子，但裁剪栈不是），统一复位。
    gpu().setTransform(1, 0, 0, 1, 0, 0);
}
/** 释放渲染目标：从目标表与纹理表一起摘掉（canvas 交给 GC；Canvas2D 没有 GL 对象要销毁）。 */
export function deleteRenderTarget(id) {
    if (current && current.id === id)
        bindRenderTarget(MAIN_SURFACE_ID);
    targets.delete(id);
    textures.delete(id);
    texSizes.delete(id);
    tintedCache.clear();
}
/** 整张上传（level 只支持 0；压缩格式由 C# 侧拦截）。 */
export function uploadTexture(id, level, bytes) {
    const canvas = textures.get(id);
    if (!canvas || level !== 0)
        return;
    const data = toBytes(bytes);
    if (!data)
        return;
    const w = canvas.width;
    const h = canvas.height;
    const need = w * h * 4;
    if (data.byteLength < need) {
        console.error(`[canvas2d] 纹理上传字节不足：需要 ${need}，收到 ${data.byteLength}`);
        return;
    }
    const pixels = new Uint8ClampedArray(need);
    pixels.set(data.subarray(0, need));
    canvas.getContext('2d').putImageData(new ImageData(pixels, w, h), 0, 0);
    tintedCache.clear();
}
/** 局部上传（动态字形图集走这条）。 */
export function uploadSubTexture(id, level, x, y, w, h, bytes) {
    const canvas = textures.get(id);
    if (!canvas || level !== 0)
        return;
    const data = toBytes(bytes);
    if (!data)
        return;
    const need = w * h * 4;
    if (data.byteLength < need) {
        console.error(`[canvas2d] 纹理局部上传字节不足：需要 ${need}，收到 ${data.byteLength}`);
        return;
    }
    const pixels = new Uint8ClampedArray(need);
    pixels.set(data.subarray(0, need));
    canvas.getContext('2d').putImageData(new ImageData(pixels, w, h), x, y);
    // 该纹理的着色副本已过期
    for (const key of Array.from(tintedCache.keys())) {
        if (key.startsWith(`${id}|`))
            tintedCache.delete(key);
    }
}
// ============ 绘制 ============
/**
 * 回放一段顶点批次：每 4 个顶点 = 1 个四边形（TL,TR,BR,BL），逐个 drawImage。
 * 顶点坐标已经是"局部像素"，由 setProjection 下发的仿射映射到画布像素。
 *
 * 注意：每个四边形都按自己的 uv 子矩形取样 —— drawImage(src, sx, sy, sw, sh, 0, 0, 1, 1) 配合 transform，
 * 而不是把整张纹理映射过去。后者会让浏览器按整幅图集做降采样，字形被邻格"出血"糊掉（图集越大越明显）。
 */
export function drawBatch(vertices, start, end) {
    const count = end - start;
    if (count <= 0)
        return;
    const tex = textures.get(currentTexture);
    const size = texSizes.get(currentTexture);
    if (!tex || !size)
        return;
    const data = toBytes(vertices);
    if (!data)
        return;
    const need = end * VERTEX_STRIDE;
    if (data.byteLength < need) {
        console.error(`[canvas2d] 顶点数据不足：需要 ${need}，收到 ${data.byteLength}`);
        return;
    }
    const view = new DataView(data.buffer, data.byteOffset, data.byteLength);
    const c = gpu();
    c.globalCompositeOperation = compositeOf();
    c.imageSmoothingEnabled = smoothing;
    for (let i = start; i < end; i += 4) {
        const o0 = i * VERTEX_STRIDE;
        const o1 = o0 + VERTEX_STRIDE;
        const o2 = o0 + VERTEX_STRIDE * 2;
        const x0 = view.getFloat32(o0, true);
        const y0 = view.getFloat32(o0 + 4, true);
        const x1 = view.getFloat32(o1, true);
        const y1 = view.getFloat32(o1 + 4, true);
        const x2 = view.getFloat32(o2, true);
        const y2 = view.getFloat32(o2 + 4, true);
        const u0 = view.getFloat32(o0 + 20, true);
        const v0 = view.getFloat32(o0 + 24, true);
        const u1 = view.getFloat32(o1 + 20, true);
        const v1 = view.getFloat32(o1 + 24, true);
        const u2 = view.getFloat32(o2 + 20, true);
        const v2 = view.getFloat32(o2 + 24, true);
        // 顶点色（unorm8x4）只在整段里取一次：同一次 Draw 的 4 个顶点颜色相同
        const r = view.getUint8(o0 + 16);
        const g = view.getUint8(o0 + 17);
        const b = view.getUint8(o0 + 18);
        const al = view.getUint8(o0 + 19);
        if (al === 0)
            continue; // 全透明：直接跳过
        const source = tintedSource(tex, size, currentTexture, r, g, b);
        // 正片叠底（kind=3）：D3D 的 dst*src.rgb 不看源 alpha，故这里忽略顶点 alpha ——
        // 否则 globalAlpha<1 会把 'multiply' 变成"往原图插值"，压暗效果被削弱。
        c.globalAlpha = blend === 3 ? 1 : al / 255;
        // uv → 四边形 的仿射：由 (u0,v0)->P0、(u1,v1)->P1、(u2,v2)->P2 三点决定
        const dux = u1 - u0, duy = v1 - v0;
        const dvx = u2 - u0, dvy = v2 - v0;
        const det = dux * dvy - duy * dvx;
        if (det === 0)
            continue; // 退化（零面积）
        const k = 1 / det;
        const ta = (dvy * (x1 - x0) - duy * (x2 - x0)) * k;
        const tb = (dvy * (y1 - y0) - duy * (y2 - y0)) * k;
        const tc = (dux * (x2 - x0) - dvx * (x1 - x0)) * k;
        const td = (dux * (y2 - y0) - dvx * (y1 - y0)) * k;
        // 这个四边形用到的 uv 子矩形（包围盒）。UV 永远轴对齐（旋转只作用在位置上，不作用在 uv 上），
        // 所以包围盒 == uv 平行四边形，含翻转时同样成立；BR 由平行四边形关系补出来。
        // 越界（平铺 WrapMode）夹到 [0,1]：Canvas2D 没有 REPEAT，这里退化成"只画一格"。
        const u3 = u1 + u2 - u0, v3 = v1 + v2 - v0;
        const uMin = Math.max(0, Math.min(u0, u1, u2, u3));
        const uMax = Math.min(1, Math.max(u0, u1, u2, u3));
        const vMin = Math.max(0, Math.min(v0, v1, v2, v3));
        const vMax = Math.min(1, Math.max(v0, v1, v2, v3));
        if (uMax <= uMin || vMax <= vMin)
            continue;
        // 只把「这个 uv 子矩形」交给 drawImage —— 绝不能把整张纹理映射过去：
        // 图集有 1024²，而一个字形只有 ~30px，源图比目标大几十倍时浏览器会按整幅源图做降采样，
        // 邻格字形与留白会被平均进来（图集出血），字就发虚发灰；只给子矩形则采样范围被严格限制在自己的格子里。
        // 先把「子矩形局部单位方格」映射到 uv 子区间，再套上面的 uv → 局部像素 仿射：
        //   S: unit → uv 子区间（线性部分乘以跨度，平移改从子矩形左上角起算），翻转由 T 自身承担。
        const uw = uMax - uMin, vh = vMax - vMin;
        const sa = ta * uw, sb = tb * uw;
        const sc = tc * vh, sd = td * vh;
        const se = ta * uMin + tc * vMin + x0 - ta * u0 - tc * v0;
        const sf = tb * uMin + td * vMin + y0 - tb * u0 - td * v0;
        // 与投影仿射复合：final = m ∘ quad
        const fa = m.a * sa + m.c * sb;
        const fb = m.b * sa + m.d * sb;
        const fc = m.a * sc + m.c * sd;
        const fd = m.b * sc + m.d * sd;
        const fe = m.a * se + m.c * sf + m.e;
        const ff = m.b * se + m.d * sf + m.f;
        c.setTransform(fa, fb, fc, fd, fe, ff);
        c.drawImage(source, uMin * size.w, vMin * size.h, uw * size.w, vh * size.h, 0, 0, 1, 1);
    }
    c.setTransform(1, 0, 0, 1, 0, 0);
    c.globalAlpha = 1;
}
/**
 * 取"带着色"的纹理：Canvas2D 没有逐绘制 tint，只能按颜色预乘一份离屏副本。
 * 白色（最常见：UI 文字与白色精灵）直接返回原纹理，不产生任何副本。
 * 颜色的 alpha 不参与缓存键，改由 drawBatch 的 globalAlpha 承担（避免 alpha 被乘两次）。
 */
function tintedSource(tex, size, texId, r, g, b) {
    if (r === 255 && g === 255 && b === 255)
        return tex;
    const key = `${texId}|${r},${g},${b}`;
    const cached = tintedCache.get(key);
    if (cached)
        return cached;
    if (tintedCache.size >= TINT_CACHE_LIMIT)
        tintedCache.clear();
    const copy = document.createElement('canvas');
    copy.width = size.w;
    copy.height = size.h;
    const cc = copy.getContext('2d');
    cc.imageSmoothingEnabled = false;
    cc.drawImage(tex, 0, 0);
    // 正片叠底上色（alpha 保持 1，避免把 alpha 也乘一遍）
    cc.globalCompositeOperation = 'multiply';
    cc.fillStyle = `rgb(${r},${g},${b})`;
    cc.fillRect(0, 0, size.w, size.h);
    // 还原原始 alpha：只在原纹理不透明处保留上色结果
    cc.globalCompositeOperation = 'destination-in';
    cc.drawImage(tex, 0, 0);
    tintedCache.set(key, copy);
    return copy;
}
// ============ 读像素 ============
/** 读一个像素（x/y 原点在左上，与 Canvas2D 一致，故无需 Y 换算）。 */
export function readPixel(x, y, out) {
    const data = gpu().getImageData(x | 0, y | 0, 1, 1).data;
    const bytes = new Uint8Array(data.buffer, data.byteOffset, 4);
    out.set(bytes, 0);
}
/** 读矩形区域像素（x/y 原点在左上），写入 out（长度需 w*h*4）。 */
export function readPixels(x, y, w, h, out) {
    const data = gpu().getImageData(x | 0, y | 0, w | 0, h | 0).data;
    const bytes = new Uint8Array(data.buffer, data.byteOffset, data.byteLength);
    out.set(bytes, 0);
}
