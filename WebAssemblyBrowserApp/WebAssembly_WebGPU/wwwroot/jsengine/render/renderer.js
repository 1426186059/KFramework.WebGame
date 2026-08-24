// =====================================================================
// 只提供 WebGPU API —— 薄 API 层。
// 渲染架构（与 WebGL 层一致）：C# 侧组装合批缓冲、维护矩阵栈/透明度/阴影。
// 本模块只负责：
//   - 初始化 WebGPU 适配器/设备、编译 WGSL 着色器、创建管线/缓冲
//   - 上传 C# 准备好的批量实例数据并 draw(6, instanceCount)
//   - 基础清屏、视口、文本/图片纹理烘焙（浏览器专属 API）
// =====================================================================

const RENDERER_VERSION = '2026-08-24e';  // 改 renderer.js 时递增，便于确认浏览器是否加载到新版（排查缓存）
const SHAPE_STRIDE = 13;           // 与 C# WebGPU.cs 的 Stride=13 一致

let _device = null;
let _ctx = null;
let _canvas = null;
let _format = null;
let _viewW = 960, _viewH = 540;
let _cssScale = 1;            // CSS 显示缩放（fit 计算；canvas 物理像素 = 逻辑分辨率 × 该值）

// 实例缓冲（GPU 侧）
let _shapeBuffer = null;
let _shadowBuffer = null;

// 纹理（文本 / 图片）
let _texData = [];            // {view, w, h, x, y} —— 当前帧要画的纹理 quad（文本/图片）
const _textures = [];         // 图片纹理 {texture, view, w, h}（按 id 索引，由 loadImageTexture 填充）
const _dynTextures = new Map(); // 动态纹理（Texture2D）：'dyn:<id>' -> {texture, view, w, h}
const _textCache = new Map(); // 文字纹理缓存：key = text+font+color → {texture, view, w, h}
const TEXT_CACHE_LIMIT = 128; // 最多缓存 128 个文字纹理，超限淘汰最旧
const TEXT_SUPERSAMPLE = 2;   // 文字纹理烘焙超采样倍率：2x 像素烘焙，显示按逻辑尺寸缩小 → 高 DPI/放大时文字更清晰

// 离屏阴影资源
let _shadowTex = null, _shadowView = null, _shadowTexW = 0, _shadowTexH = 0;
let _blurTex = null, _blurView = null;

// 状态（C# 侧有一份副本，JS 侧用这些值做渲染时组合）
let _alpha = 1.0;
let _clearColor = [0.05, 0.07, 0.09, 1];

// ------------------------- 调试探针（仅保留错误追踪） -------------------------
const _dbg = {
  lastErrors: [],
  deviceReady: false, adapterInfo: null,
  frameCount: 0,
  lastFont: '',
};
if (typeof window !== 'undefined') {
  window.__renderDbg = _dbg;
}

// ---------------------------------------------------------------------
//  着色器：外部 .wgsl 文件（wwwroot/jsengine/shaders/）
//  WGSL 源码独立维护，与 JS 逻辑解耦；运行时 fetch 文本编译。
// ---------------------------------------------------------------------
const SHADER_DIR = './jsengine/shaders/';
async function fetchShader(name) {
  const res = await fetch(SHADER_DIR + name);
  if (!res.ok) throw new Error('[WebGPU] shader 加载失败: ' + name + ' (' + res.status + ')');
  return await res.text();
}

// ---------------------------------------------------------------------
//  管线 / 资源
// ---------------------------------------------------------------------
const BLEND_PREMULTIPLIED = {
  color: { srcFactor: 'one', dstFactor: 'one-minus-src-alpha', operation: 'add' },
  alpha: { srcFactor: 'one', dstFactor: 'one-minus-src-alpha', operation: 'add' },
};
let _shapePipeline = null, _shapeBindGroup = null;
let _shadowPipeline = null, _shadowBindGroup = null;
let _texPipeline = null;
let _blurPipeline = null;
let _globalsBuffer = null;
let _texGlobalsBuffer = null, _blurDirBuffer = null;
let _sampler = null;

// 离屏文本/图片烘焙 Canvas
let _offscreenCanvas = null;
let _offscreenCtx = null;
let _measureCanvas = null;
let _measureCtx = null;

function makeGlobalsBuffer() {
  _globalsBuffer = _device.createBuffer({
    size: 16, usage: GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST
  });
}
function writeGlobals(alpha) {
  _device.queue.writeBuffer(_globalsBuffer, 0, new Float32Array([
    _viewW, _viewH, alpha, 0
  ]));
}

// ---------------------------------------------------------------------
//  初始化（立即触发异步请求，不阻塞 C# 主线）
// ---------------------------------------------------------------------
function gpu_init() {
  console.info('[WebGPU] renderer.js version =', RENDERER_VERSION);
  if (!navigator.gpu) {
    console.warn('[WebGPU] 浏览器不支持 WebGPU，已跳过初始化');
    _dbg.lastErrors.push('browser_not_support_webgpu');
    return;
  }
  _canvas = document.querySelector('#game');
  if (!_canvas) {
    console.warn('[WebGPU] 找不到 #game canvas');
    _dbg.lastErrors.push('canvas_not_found');
    return;
  }
  _offscreenCanvas = document.createElement('canvas');
  _offscreenCtx = _offscreenCanvas.getContext('2d', { willReadFrequently: true });
  _measureCanvas = document.createElement('canvas');
  _measureCtx = _measureCanvas.getContext('2d', { willReadFrequently: true });

  // 画布自适应（与 WebGL 相同的 fit 策略）
  const fit = () => {
    const scale = Math.min(
      (window.innerWidth - 24) / _viewW,
      (window.innerHeight - 24) / _viewH
    );
    const s = Math.min(1.6, Math.max(0.25, scale));
    _cssScale = s;
    _canvas.style.width = (_viewW * s) + 'px';
    _canvas.style.height = (_viewH * s) + 'px';
    // 逻辑分辨率挂到 canvas，供输入层做坐标换算（鼠标 CSS 像素 → 逻辑像素）
    _canvas.dataset.vw = String(_viewW);
    _canvas.dataset.vh = String(_viewH);
    // 物理像素对齐 CSS 显示像素：避免浏览器把低分辨率 canvas 拉伸导致画面/文字模糊
    if (_device) {
      _canvas.width = Math.max(2, Math.round(_viewW * s));
      _canvas.height = Math.max(2, Math.round(_viewH * s));
    }
  };
  window.addEventListener('resize', fit);
  fit();

  // 异步请求设备，设备就绪后标记
  (async () => {
    try {
      // 加载外部 WGSL 着色器（wwwroot/jsengine/shaders/*.wgsl）
      const [shapeWgsl, shadowWgsl, texWgsl, blurWgsl] = await Promise.all([
        fetchShader('shape.wgsl'),
        fetchShader('shadow.wgsl'),
        fetchShader('text.wgsl'),
        fetchShader('blur.wgsl'),
      ]);
      const adapter = await navigator.gpu.requestAdapter();
      if (!adapter) throw new Error('no adapter');
      try { _dbg.adapterInfo = await adapter.requestAdapterInfo(); } catch (_) {}
      _device = await adapter.requestDevice();
      _device.lost.then((info) => {
        console.warn('[WebGPU] device lost:', info?.message);
        _dbg.lastErrors.push('device_lost: ' + (info?.message ?? 'unknown'));
      });
      // WebGPU validation error 监听：直接把 GPU 验证失败打印出来
      _device.addEventListener('uncapturederror', (e) => {
        const msg = e?.error?.message || String(e);
        console.error('[WebGPU] uncapturederror:', msg);
        _dbg.lastErrors.push('uncapturederror: ' + msg);
        if (_dbg.lastErrors.length > 32) _dbg.lastErrors.splice(0, _dbg.lastErrors.length - 32);
      });

      _ctx = _canvas.getContext('webgpu');
      _format = navigator.gpu.getPreferredCanvasFormat();
      _canvas.width = Math.max(2, Math.round(_viewW * (_cssScale || 1)));
      _canvas.height = Math.max(2, Math.round(_viewH * (_cssScale || 1)));
      _ctx.configure({
        device: _device,
        format: _format,
        alphaMode: 'opaque',
        usage: GPUTextureUsage.RENDER_ATTACHMENT | GPUTextureUsage.COPY_SRC | GPUTextureUsage.COPY_DST,
      });

      // 创建管线
      const shapeModule = _device.createShaderModule({ code: shapeWgsl });
      _shapePipeline = _device.createRenderPipeline({
        layout: 'auto',
        vertex: {
          module: shapeModule, entryPoint: 'vs',
          buffers: [{
            arrayStride: SHAPE_STRIDE * 4, stepMode: 'instance',
            attributes: [
              { shaderLocation: 0, offset: 0,  format: 'float32x2' },
              { shaderLocation: 1, offset: 8,  format: 'float32x2' },
              { shaderLocation: 2, offset: 16, format: 'float32x2' },
              { shaderLocation: 3, offset: 24, format: 'float32'   },
              { shaderLocation: 4, offset: 28, format: 'float32'   },
              { shaderLocation: 5, offset: 32, format: 'float32x4' },
              { shaderLocation: 6, offset: 48, format: 'float32'   },
            ],
          }],
        },
        fragment: { module: shapeModule, entryPoint: 'fs', targets: [{ format: _format, blend: BLEND_PREMULTIPLIED }] },
        primitive: { topology: 'triangle-list' },
      });
      makeGlobalsBuffer();
      _shapeBindGroup = _device.createBindGroup({
        layout: _shapePipeline.getBindGroupLayout(0),
        entries: [{ binding: 0, resource: { buffer: _globalsBuffer } }],
      });

      // 阴影专用管线（vs 不翻转 Y，配平离屏纹理与 BLUR/composite 的坐标约定）
      const shadowModule = _device.createShaderModule({ code: shadowWgsl });
      _shadowPipeline = _device.createRenderPipeline({
        layout: 'auto',
        vertex: {
          module: shadowModule, entryPoint: 'vs',
          buffers: [{
            arrayStride: SHAPE_STRIDE * 4, stepMode: 'instance',
            attributes: [
              { shaderLocation: 0, offset: 0,  format: 'float32x2' },
              { shaderLocation: 1, offset: 8,  format: 'float32x2' },
              { shaderLocation: 2, offset: 16, format: 'float32x2' },
              { shaderLocation: 3, offset: 24, format: 'float32'   },
              { shaderLocation: 4, offset: 28, format: 'float32'   },
              { shaderLocation: 5, offset: 32, format: 'float32x4' },
              { shaderLocation: 6, offset: 48, format: 'float32'   },
            ],
          }],
        },
        fragment: { module: shadowModule, entryPoint: 'fs', targets: [{ format: _format, blend: BLEND_PREMULTIPLIED }] },
        primitive: { topology: 'triangle-list' },
      });
      _shadowBindGroup = _device.createBindGroup({
        layout: _shadowPipeline.getBindGroupLayout(0),
        entries: [{ binding: 0, resource: { buffer: _globalsBuffer } }],
      });

      // 实例缓冲（动态，4096 实例）
      _shapeBuffer = _device.createBuffer({
        size: SHAPE_STRIDE * 4096 * 4,
        usage: GPUBufferUsage.VERTEX | GPUBufferUsage.COPY_DST,
      });
      _shadowBuffer = _device.createBuffer({
        size: SHAPE_STRIDE * 4096 * 4,
        usage: GPUBufferUsage.VERTEX | GPUBufferUsage.COPY_DST,
      });

      // 纹理 / 模糊管线的显式 bind group 布局（避免 auto 推断把 G uniform 优化掉）
      // binding 0 = Globals  uniform(16B), 1 = Tex/Dir uniform(16B), 2 = sampler, 3 = texture
      const texBlurBGL = _device.createBindGroupLayout({
        entries: [
          { binding: 0, visibility: GPUShaderStage.FRAGMENT | GPUShaderStage.VERTEX, buffer: { type: 'uniform' } },
          { binding: 1, visibility: GPUShaderStage.FRAGMENT | GPUShaderStage.VERTEX, buffer: { type: 'uniform' } },
          { binding: 2, visibility: GPUShaderStage.FRAGMENT,                sampler: { type: 'filtering' } },
          { binding: 3, visibility: GPUShaderStage.FRAGMENT,                texture: { sampleType: 'float' } },
        ],
      });
      const texBlurLayout = _device.createPipelineLayout({ bindGroupLayouts: [texBlurBGL] });

      // 纹理管线
      const texModule = _device.createShaderModule({ code: texWgsl });
      _texPipeline = _device.createRenderPipeline({
        layout: texBlurLayout,
        vertex: { module: texModule, entryPoint: 'vs' },
        fragment: { module: texModule, entryPoint: 'fs', targets: [{ format: _format, blend: BLEND_PREMULTIPLIED }] },
        primitive: { topology: 'triangle-list' },
      });
      _texGlobalsBuffer = _device.createBuffer({ size: 16, usage: GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST });
      _sampler = _device.createSampler({ magFilter: 'linear', minFilter: 'linear' });

      // 模糊管线
      const blurModule = _device.createShaderModule({ code: blurWgsl });
      _blurPipeline = _device.createRenderPipeline({
        layout: texBlurLayout,
        vertex: { module: blurModule, entryPoint: 'vs' },
        fragment: { module: blurModule, entryPoint: 'fs', targets: [{ format: _format, blend: BLEND_PREMULTIPLIED }] },
        primitive: { topology: 'triangle-list' },
      });
      _blurDirBuffer = _device.createBuffer({ size: 16, usage: GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST });

      initOffscreen();

      _dbg.deviceReady = true;
      console.log('[WebGPU] 设备就绪', _dbg.adapterInfo ? (_dbg.adapterInfo.vendor + '/' + _dbg.adapterInfo.architecture) : '');

      document.getElementById('loading')?.remove();
    } catch (e) {
      console.error('[WebGPU] 初始化失败:', e);
      _dbg.lastErrors.push('init_failed: ' + (e?.message ?? String(e)));
      const loading = document.getElementById('loading');
      if (loading) {
        loading.textContent = 'WebGPU 初始化失败: ' + (e?.message ?? String(e));
        loading.style.color = '#ff6b6b';
      }
    }
  })();
}

function initOffscreen() {
  const w = Math.max(2, _viewW), h = Math.max(2, _viewH);
  _shadowTexW = w; _shadowTexH = h;
  if (_shadowTex) _shadowTex.destroy();
  if (_blurTex) _blurTex.destroy();
  _shadowTex = _device.createTexture({
    size: [w, h], format: _format,
    usage: GPUTextureUsage.RENDER_ATTACHMENT | GPUTextureUsage.TEXTURE_BINDING,
  });
  _shadowView = _shadowTex.createView();
  _blurTex = _device.createTexture({
    size: [w, h], format: _format,
    usage: GPUTextureUsage.RENDER_ATTACHMENT | GPUTextureUsage.TEXTURE_BINDING,
  });
  _blurView = _blurTex.createView();
}

// ---------------------------------------------------------------------
//  工具（颜色解析）—— 现在 C# 侧已经把字符串颜色解析成 rgba float，
//  但 clear() 仍可能传十六进制字符串，保留一份薄实现。
// ---------------------------------------------------------------------
function parseColor(c) {
  if (typeof c !== 'string') return [0,0,0,1];
  c = c.trim();
  if (c[0] === '#') {
    let hex = c.slice(1);
    if (hex.length === 3) hex = hex.split('').map(x => x+x).join('');
    if (hex.length === 8) {
      return [
        parseInt(hex.slice(0,2),16)/255,
        parseInt(hex.slice(2,4),16)/255,
        parseInt(hex.slice(4,6),16)/255,
        parseInt(hex.slice(6,8),16)/255,
      ];
    }
    return [
      parseInt(hex.slice(0,2),16)/255,
      parseInt(hex.slice(2,4),16)/255,
      parseInt(hex.slice(4,6),16)/255, 1,
    ];
  }
  const m = c.match(/rgba?\(([^)]+)\)/);
  if (m) {
    const p = m[1].split(',').map(s => parseFloat(s));
    return [p[0]/255, p[1]/255, p[2]/255, p[3] === undefined ? 1 : p[3]];
  }
  return [1,1,1,1];
}

// ---------------------------------------------------------------------
//  强制 Float32Array 转换（C# double[] → JS Float64Array → GPU 需要 Float32Array）
// ---------------------------------------------------------------------
function _toFloat32(arr) {
  if (!arr) return null;
  if (arr instanceof Float32Array) return arr;
  // Float64Array / Array / TypedArray 都可以直接通过构造器转换
  return new Float32Array(arr);
}

// ---------------------------------------------------------------------
//  公开薄 API（被 C# [JSImport] 调用，通过 main.js setModuleImports）
// ---------------------------------------------------------------------
export const gpu = {
  init: gpu_init,

  resize(w, h) {
    if (!_canvas) return;
    _viewW = w; _viewH = h;
    _canvas.dataset.vw = String(w);
    _canvas.dataset.vh = String(h);
    if (_device) {
      _canvas.width = Math.max(2, Math.round(w * (_cssScale || 1)));
      _canvas.height = Math.max(2, Math.round(h * (_cssScale || 1)));
      initOffscreen();
    }
  },

  beginFrame(r, g, b, a) { _clearColor = [r, g, b, a]; },

  clear(color) {
    const c = parseColor(color);
    _clearColor = c;
  },

  // 整帧图元一次性提交（C# 侧已累积好 double[]）
  submit(shapes, shadows, alpha) {
    if (!_device || !_ctx) return;  // 设备未就绪：静默跳过
    _alpha = alpha;
    renderFrame(shapes, shadows);
    _texData = [];
  },

  // 变换/透明度/阴影 —— C# 侧 CPU 预变换形状，这里仅保留空壳以兼容 API。
  setTransform() {},
  resetTransform() {},
  saveTransform() {},
  restoreTransform() {},
  translate() {},
  setAlpha() {},

  shadowColor(color, blur) {
    // C# 侧 Push() 时直接把阴影实例塞进 shadowBatch，此处薄 API 壳子保证兼容。
  },
  noShadow() {},

  fillText(text, x, y, font, color, align) { drawTextSprite(text, x, y, font, color, align); },
  loadImage(src) { return loadImageTexture(src); },
  drawImage(id, x, y, w, h) { drawImageTexture(id, x, y, w, h); },
  uploadTexture(id, w, h, argb) { uploadTexturePixels(id, w, h, argb); },
  disposeTexture(id) { disposeTexturePixels(id); },
  measureText(text, font) {
    if (!_measureCtx) return 0;
    _measureCtx.font = font;
    return _measureCtx.measureText(text).width;
  },

  // 辅助：画布信息
  getCanvas() { return _canvas; },
  getWidth() { return _viewW; },
  getHeight() { return _viewH; },
};

// ---------------------------------------------------------------------
//  文本 / 图片：离屏 Canvas2D 烘焙 → GPUTexture
// ---------------------------------------------------------------------
function bakeToTexture(drawFn, w, h) {
  if (!_device || !_offscreenCtx) return null;
  _offscreenCanvas.width = w; _offscreenCanvas.height = h;
  _offscreenCtx.clearRect(0, 0, w, h);
  drawFn(_offscreenCtx);
  const tex = _device.createTexture({
    size: [w, h], format: 'rgba8unorm',
    usage: GPUTextureUsage.TEXTURE_BINDING | GPUTextureUsage.COPY_DST | GPUTextureUsage.RENDER_ATTACHMENT,
  });
  // WebGPU 要求 writeTexture 的 bytesPerRow 必须是 256 的倍数
  const src = _offscreenCtx.getImageData(0, 0, w, h).data;
  let _nonTransparent = 0;
  for (let i = 3; i < src.length; i += 4) if (src[i] > 0) _nonTransparent++;
  if (_dbg.frameCount < 3) console.log('[TEX bake] ' + JSON.stringify({ w, h, nonTransparent: _nonTransparent, font: _dbg.lastFont }));
  const srcRowBytes = w * 4;
  const bytesPerRow = Math.ceil(srcRowBytes / 256) * 256;
  let pixels = src;
  if (bytesPerRow !== srcRowBytes) {
    // 按对齐行重排：每行补 0 到 bytesPerRow
    pixels = new Uint8Array(bytesPerRow * h);
    for (let y = 0; y < h; y++) {
      pixels.set(src.subarray(y * srcRowBytes, y * srcRowBytes + srcRowBytes), y * bytesPerRow);
    }
  }
  _device.queue.writeTexture(
    { texture: tex },
    pixels,
    { bytesPerRow, rowsPerImage: h },
    { width: w, height: h }
  );
  return tex;
}

function drawTextSprite(text, x, y, font, color, align) {
  if (!_measureCtx || !_device) return;
  _dbg.lastFont = font;
  _measureCtx.font = font;
  // 文字纹理缓存：同一 (text, font, color) 只烘焙一次，避免每帧创建 GPU 纹理导致资源累积。
  const key = text + '\u0001' + font + '\u0001' + color;
  let entry = _textCache.get(key);
  if (!entry) {
    const m = _measureCtx.measureText(text);
    const lw = Math.ceil(m.width) + 8;   // 逻辑尺寸（屏幕坐标）
    const lh = 48;
    const ts = TEXT_SUPERSAMPLE;
    const pw = Math.ceil(lw * ts);       // 纹理像素尺寸 = 逻辑 × 超采样
    const ph = Math.ceil(lh * ts);
    const sfont = ts === 1 ? font : font.replace(/(\d+(?:\.\d+)?)px/g, (_, n) => (parseFloat(n) * ts) + 'px');
    const tex = bakeToTexture((c) => {
      c.font = sfont;
      c.fillStyle = color;
      c.textAlign = 'left';
      c.textBaseline = 'middle';
      c.fillText(text, 4 * ts, ph / 2);
    }, pw, ph);
    if (!tex) return;
    entry = { texture: tex, view: tex.createView(), lw, lh };
    _textCache.set(key, entry);
    if (_textCache.size > TEXT_CACHE_LIMIT) {
      const oldest = _textCache.keys().next().value;
      _textCache.delete(oldest);
    }
  }
  let dx = x;
  if (align === 'center') dx = x - entry.lw / 2;
  else if (align === 'right') dx = x - entry.lw;
  _texData.push({ view: entry.view, w: entry.lw, h: entry.lh, x: dx, y: y - entry.lh / 2 });
}

function loadImageTexture(src) {
  if (!_device) return Promise.resolve({ id: -1, w: 0, h: 0 });
  return new Promise((resolve) => {
    const img = new Image();
    img.crossOrigin = 'anonymous';
    const id = _textures.length;
    _textures.push(null);
    img.onload = () => {
      const w = img.width, h = img.height;
      const tex = bakeToTexture((c) => c.drawImage(img, 0, 0), w, h);
      if (tex) _textures[id] = { texture: tex, view: tex.createView(), w, h };
      resolve({ id, w, h });
    };
    img.onerror = () => resolve({ id: -1, w: 0, h: 0 });
    img.src = src;
  });
}

function drawImageTexture(id, x, y, w, h) {
  // 先查图片纹理（数字 id 索引），再查动态纹理（'dyn:'+id，Texture2D）
  let tex = _textures[id];
  if (!tex || !tex.view) tex = _dynTextures.get('dyn:' + id);
  if (!tex || !tex.view) return;
  _texData.push({ view: tex.view, w, h, x, y });
}

// 动态纹理（Texture2D）：把 ARGB 像素重传到 GPU 纹理（'dyn:'+id 命名空间，与图片 id 隔离）
function uploadTexturePixels(id, w, h, argb) {
  if (!_device) return;
  const key = 'dyn:' + id;
  let tex = _dynTextures.get(key);
  if (!tex || tex.w !== w || tex.h !== h) {
    if (tex && tex.texture) tex.texture.destroy();
    const texture = _device.createTexture({
      size: [w, h], format: 'rgba8unorm',
      usage: GPUTextureUsage.TEXTURE_BINDING | GPUTextureUsage.COPY_DST,
    });
    tex = { texture, view: texture.createView(), w, h };
    _dynTextures.set(key, tex);
  }
  // WebGPU 要求 writeTexture 的 bytesPerRow 必须是 256 的倍数（与 bakeToTexture 一致）
  const srcRowBytes = w * 4;
  const bytesPerRow = Math.ceil(srcRowBytes / 256) * 256;
  let pixels = argbToRgba(argb);
  if (bytesPerRow !== srcRowBytes) {
    const padded = new Uint8Array(bytesPerRow * h);
    for (let y = 0; y < h; y++) {
      padded.set(pixels.subarray(y * srcRowBytes, y * srcRowBytes + srcRowBytes), y * bytesPerRow);
    }
    pixels = padded;
  }
  _device.queue.writeTexture(
    { texture: tex.texture },
    pixels,
    { bytesPerRow, rowsPerImage: h },
    { width: w, height: h }
  );
}

function disposeTexturePixels(id) {
  const tex = _dynTextures.get('dyn:' + id);
  if (tex && tex.texture) tex.texture.destroy();
  _dynTextures.delete('dyn:' + id);
}

// ARGB8888 int[] → RGBA Uint8Array（straight alpha，与 text.wgsl 的预乘假设匹配）
function argbToRgba(argb) {
  const n = argb.length;
  const rgba = new Uint8Array(n * 4);
  for (let i = 0; i < n; i++) {
    const c = argb[i];
    rgba[i * 4] = (c >> 16) & 0xff;
    rgba[i * 4 + 1] = (c >> 8) & 0xff;
    rgba[i * 4 + 2] = c & 0xff;
    rgba[i * 4 + 3] = (c >>> 24) & 0xff;
  }
  return rgba;
}

// ---------------------------------------------------------------------
//  帧渲染（整帧一次性）
// ---------------------------------------------------------------------
function renderFrame(shapesRaw, shadowsRaw) {
  const shapes = _toFloat32(shapesRaw);
  const shadows = _toFloat32(shadowsRaw);
  _dbg.frameCount++;

  if (_dbg.frameCount <= 3) {
    console.log('[frame]', _dbg.frameCount, 'texData=', JSON.stringify(_texData.map(t => ({ x: t.x|0, y: t.y|0, w: t.w|0, h: t.h|0 }))));
  }

  const screen = _ctx.getCurrentTexture().createView();

  // 1) 阴影：离屏渲染（独立 encoder，submit 到离屏 shadowTex）→ 两次模糊（submit 到离屏 shadowTex/blurTex）
  if (shadows && shadows.length > 0) {
    renderShapesToView(shadows, _shadowView, 1.0);
    runBlur(_shadowView, _blurView,   [1, 0]); // 水平
    runBlur(_blurView,   _shadowView, [0, 1]); // 垂直
  }

  // 2) 所有到屏幕 swapchain 的绘制：必须合并到 【同一个 encoder + 同一个 render pass】，最后只 submit 一次！
  //    (WebGPU 规范：getCurrentTexture 拿到的 swapchain 图像在一帧内只能被 submit 一次)
  //    同一个 encoder 里不能对同一个 screen view 连续多次 beginRenderPass（Chrome 下 undefined behavior → 全透明黑屏）
  //    因此：shape → blur → 文本/图片 全部放在同一个 pass 里用 setPipeline 切换，连续 draw！
  //
  // 2.5) 纹理（文本/图片）draw 的准备：在 pass 记录【之前】为每个 draw 预建独立 16 字节 UBO + bind group。
  //      不要在 pass 内 / 循环内对共享 _texGlobalsBuffer 多次 writeBuffer ——
  //      bindGroup 不快照 buffer 数据（按 queue 顺序在 draw 时读取 buffer 当时内容），
  //      但 swiftshader / 部分驱动会读到最后一次写入，导致所有文字画到同一位置（错位/覆盖）。
  //      独立 UBO + 预建 bind group 在任何实现上行为都可预测。
  const texDraws = [];
  const texUbos = [];   // 每帧临时 UBO，submit 后统一 destroy，避免 GPU 内存累积
  for (const t of _texData) {
    if (!t.view) continue;
    const ubo = _device.createBuffer({ size: 16, usage: GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST });
    _device.queue.writeBuffer(ubo, 0, new Float32Array([t.x, t.y, t.w, t.h]));
    texUbos.push(ubo);
    texDraws.push(_device.createBindGroup({
      layout: _texPipeline.getBindGroupLayout(0),
      entries: [
        { binding: 0, resource: { buffer: _globalsBuffer } },
        { binding: 1, resource: { buffer: ubo } },
        { binding: 2, resource: _sampler },
        { binding: 3, resource: t.view },
      ],
    }));
  }

  // 3) 全局 uniform（分辨率/alpha）也在 pass 外写入，供 shape / text / blur 所有 draw 使用
  writeGlobals(_alpha);

  const encoder = _device.createCommandEncoder();
  const pass = encoder.beginRenderPass({
    colorAttachments: [{
      view: screen,
      clearValue: { r: _clearColor[0], g: _clearColor[1], b: _clearColor[2], a: _clearColor[3] },
      loadOp: 'clear', storeOp: 'store',
    }],
  });
  {
    // 2a) 主形状渲到屏幕
    if (shapes && shapes.length > 0) {
      const instanceCount = Math.floor(shapes.length / SHAPE_STRIDE);
      const elementCount = instanceCount * SHAPE_STRIDE;
      let data = shapes;
      let dataElCount = elementCount;
      if (shapes.length !== elementCount) {
        data = shapes.subarray(0, elementCount);
        dataElCount = data.length;
      }
      const capacityEl = 4096 * SHAPE_STRIDE;
      if (dataElCount > capacityEl) {
        dataElCount = capacityEl;
        data = data.subarray(0, dataElCount);
      }
      _device.queue.writeBuffer(_shapeBuffer, 0, data, 0, dataElCount);
      pass.setPipeline(_shapePipeline);
      pass.setBindGroup(0, _shapeBindGroup);
      pass.setVertexBuffer(0, _shapeBuffer);
      pass.draw(6, instanceCount);
    }
    // 2b) 阴影 composite（同一 pass，切换到 blur pipeline draw）
    if (shadows && shadows.length > 0) {
      compositeBlurInEncoder(pass, encoder, _shadowView, screen);
    }
    // 2c) 纹理（文本 / 图片）：同一 pass，切换到 tex pipeline，用预建的 bind group 逐个 draw
    for (const bg of texDraws) {
      pass.setPipeline(_texPipeline);
      pass.setBindGroup(0, bg);
      pass.draw(6);
    }
  }
  pass.end();
  _device.queue.submit([encoder.finish()]);
  // 释放本帧临时 UBO（submit 后 destroy 安全，避免 GPU 内存累积）
  for (const u of texUbos) u.destroy();
}

function renderShapesToView(arr, view, alpha) {
  const encoder = _device.createCommandEncoder();
  const pass = encoder.beginRenderPass({
    colorAttachments: [{
      view,
      clearValue: { r: 0, g: 0, b: 0, a: 0 },
      loadOp: 'clear', storeOp: 'store',
    }],
  });
  writeGlobals(alpha);
  _device.queue.writeBuffer(_shadowBuffer, 0, arr);
  pass.setPipeline(_shadowPipeline);
  pass.setBindGroup(0, _shadowBindGroup);
  pass.setVertexBuffer(0, _shadowBuffer);
  pass.draw(6, arr.length / SHAPE_STRIDE);
  pass.end();
  _device.queue.submit([encoder.finish()]);
}

function runBlur(srcView, dstView, dir) {
  const encoder = _device.createCommandEncoder();
  const pass = encoder.beginRenderPass({
    colorAttachments: [{ view: dstView, clearValue: { r: 0, g: 0, b: 0, a: 0 }, loadOp: 'clear', storeOp: 'store' }],
  });
  _device.queue.writeBuffer(_blurDirBuffer, 0, new Float32Array([dir[0], dir[1], 1 / _shadowTexW, 1 / _shadowTexH]));
  const bg = _device.createBindGroup({
    layout: _blurPipeline.getBindGroupLayout(0),
    entries: [
      { binding: 0, resource: { buffer: _globalsBuffer } },
      { binding: 1, resource: { buffer: _blurDirBuffer } },
      { binding: 2, resource: _sampler },
      { binding: 3, resource: srcView },
    ],
  });
  pass.setPipeline(_blurPipeline);
  pass.setBindGroup(0, bg);
  pass.draw(3);
  pass.end();
  _device.queue.submit([encoder.finish()]);
}

function compositeBlur(blurView, screen) {
  const encoder = _device.createCommandEncoder();
  compositeBlurInEncoder(encoder, blurView, screen);
  _device.queue.submit([encoder.finish()]);
}
function compositeBlurInEncoder(pass, encoder, blurView, screen) {
  _device.queue.writeBuffer(_blurDirBuffer, 0, new Float32Array([0, 0, 1 / _shadowTexW, 1 / _shadowTexH]));
  const bg = _device.createBindGroup({
    layout: _blurPipeline.getBindGroupLayout(0),
    entries: [
      { binding: 0, resource: { buffer: _globalsBuffer } },
      { binding: 1, resource: { buffer: _blurDirBuffer } },
      { binding: 2, resource: _sampler },
      { binding: 3, resource: blurView },
    ],
  });
  pass.setPipeline(_blurPipeline);
  pass.setBindGroup(0, bg);
  pass.draw(3);
}
