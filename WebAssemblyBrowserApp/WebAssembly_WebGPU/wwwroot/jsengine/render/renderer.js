// =====================================================================
// 只提供 WebGPU API —— 薄 API 层。
// 渲染架构（与 WebGL 层一致）：C# 侧组装合批缓冲、维护矩阵栈/透明度/阴影。
// 本模块只负责：
//   - 初始化 WebGPU 适配器/设备、编译 WGSL 着色器、创建管线/缓冲
//   - 上传 C# 准备好的批量实例数据并 draw(6, instanceCount)
//   - 基础清屏、视口、文本/图片纹理烘焙（浏览器专属 API）
// =====================================================================

const RENDERER_VERSION = '2026-08-24c';  // 改 renderer.js 时递增，便于确认浏览器是否加载到新版（排查缓存）
const SHAPE_STRIDE = 13;           // 与 C# WebGPU.cs 的 Stride=13 一致

let _device = null;
let _ctx = null;
let _canvas = null;
let _format = null;
let _viewW = 960, _viewH = 540;

// 实例缓冲（GPU 侧）
let _shapeBuffer = null;
let _shadowBuffer = null;

// 纹理（文本 / 图片）
let _texData = [];
const _textures = [];         // {texture, view, w, h}

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
//  WGSL 着色器
// ---------------------------------------------------------------------

const SHAPE_WGSL = `
struct Globals {
  resolution : vec2<f32>,
  alpha      : f32,
  _pad       : f32,
};
@group(0) @binding(0) var<uniform> G : Globals;

struct Inst {
  @location(0) rectPos  : vec2<f32>,
  @location(1) rectSize : vec2<f32>,
  @location(2) halfSize : vec2<f32>,
  @location(3) radius   : f32,
  @location(4) uType    : f32,
  @location(5) color    : vec4<f32>,
  @location(6) lineW    : f32,
};

struct VSOut {
  @builtin(position) pos : vec4<f32>,
  @location(0) uv       : vec2<f32>,
  @location(1) half     : vec2<f32>,
  @location(2) radius   : f32,
  @location(3) uType    : f32,
  @location(4) color    : vec4<f32>,
  @location(5) lineW    : f32,
};

@vertex
fn vs(@builtin(vertex_index) vi : u32, inst : Inst) -> VSOut {
  var quad = array<vec2<f32>, 6>(
    vec2<f32>(-1.0, -1.0), vec2<f32>( 1.0, -1.0), vec2<f32>(-1.0, 1.0),
    vec2<f32>(-1.0,  1.0), vec2<f32>( 1.0, -1.0), vec2<f32>( 1.0, 1.0)
  );
  let q = quad[vi];
  let local = q * inst.halfSize;
  let world = inst.rectPos + inst.halfSize + local;
  var out : VSOut;
  let ndc = vec2<f32>(
    (world.x / G.resolution.x) * 2.0 - 1.0,
    1.0 - (world.y / G.resolution.y) * 2.0
  );
  out.pos = vec4<f32>(ndc, 0.0, 1.0);
  out.uv = q;
  out.half = inst.halfSize;
  out.radius = inst.radius;
  out.uType = inst.uType;
  out.color = inst.color;
  out.lineW = inst.lineW;
  return out;
}

fn sdRoundRect(p : vec2<f32>, b : vec2<f32>, r : f32) -> f32 {
  let q = abs(p) - (b - vec2<f32>(r, r));
  return length(max(q, vec2<f32>(0.0, 0.0))) + min(max(q.x, q.y), 0.0) - r;
}

@fragment
fn fs(in : VSOut) -> @location(0) vec4<f32> {
  var d : f32;
  if (in.uType < 0.5) {
    d = -1.0;
  } else if (in.uType < 1.5) {
    d = sdRoundRect(in.uv * in.half, in.half, in.radius);
  } else if (in.uType < 2.5) {
    d = length(in.uv * in.half) - in.radius;
  } else {
    let x = abs(in.uv.x);
    let dist = max(x - (1.0 - in.lineW / (2.0 * in.half.x)),
                   abs(in.uv.y) - in.lineW / (2.0 * in.half.y));
    d = dist;
  }
  let aa = 1.0;
  var a = 1.0 - smoothstep(-aa, aa, d);
  if (a <= 0.0) { discard; }
  return vec4<f32>(in.color.rgb, in.color.a * a * G.alpha);
}
`;

// 阴影专用：渲染到离屏纹理，不做 Y 翻转（纹理 v 轴与世界 y 向下一致，
// 这样后续 BLUR(全屏 uv=p*0.5+0.5)/composite 才能与原始形状朝向一致，
// 否则阴影会被垂直镜像成"对面的重影"）。Fragment 与 SHAPE 相同。
const SHADOW_WGSL = `
struct Globals {
  resolution : vec2<f32>,
  alpha      : f32,
  _pad       : f32,
};
@group(0) @binding(0) var<uniform> G : Globals;

struct Inst {
  @location(0) rectPos  : vec2<f32>,
  @location(1) rectSize : vec2<f32>,
  @location(2) halfSize : vec2<f32>,
  @location(3) radius   : f32,
  @location(4) uType    : f32,
  @location(5) color    : vec4<f32>,
  @location(6) lineW    : f32,
};

struct VSOut {
  @builtin(position) pos : vec4<f32>,
  @location(0) uv       : vec2<f32>,
  @location(1) half     : vec2<f32>,
  @location(2) radius   : f32,
  @location(3) uType    : f32,
  @location(4) color    : vec4<f32>,
  @location(5) lineW    : f32,
};

@vertex
fn vs(@builtin(vertex_index) vi : u32, inst : Inst) -> VSOut {
  var quad = array<vec2<f32>, 6>(
    vec2<f32>(-1.0, -1.0), vec2<f32>( 1.0, -1.0), vec2<f32>(-1.0, 1.0),
    vec2<f32>(-1.0,  1.0), vec2<f32>( 1.0, -1.0), vec2<f32>( 1.0, 1.0)
  );
  let q = quad[vi];
  let local = q * inst.halfSize;
  let world = inst.rectPos + inst.halfSize + local;
  var out : VSOut;
  let ndc = vec2<f32>(
    (world.x / G.resolution.x) * 2.0 - 1.0,
    (world.y / G.resolution.y) * 2.0 - 1.0
  );
  out.pos = vec4<f32>(ndc, 0.0, 1.0);
  out.uv = q;
  out.half = inst.halfSize;
  out.radius = inst.radius;
  out.uType = inst.uType;
  out.color = inst.color;
  out.lineW = inst.lineW;
  return out;
}

fn sdRoundRect(p : vec2<f32>, b : vec2<f32>, r : f32) -> f32 {
  let q = abs(p) - (b - vec2<f32>(r, r));
  return length(max(q, vec2<f32>(0.0, 0.0))) + min(max(q.x, q.y), 0.0) - r;
}

@fragment
fn fs(in : VSOut) -> @location(0) vec4<f32> {
  var d : f32;
  if (in.uType < 0.5) {
    d = -1.0;
  } else if (in.uType < 1.5) {
    d = sdRoundRect(in.uv * in.half, in.half, in.radius);
  } else if (in.uType < 2.5) {
    d = length(in.uv * in.half) - in.radius;
  } else {
    let x = abs(in.uv.x);
    let dist = max(x - (1.0 - in.lineW / (2.0 * in.half.x)),
                   abs(in.uv.y) - in.lineW / (2.0 * in.half.y));
    d = dist;
  }
  let aa = 1.0;
  var a = 1.0 - smoothstep(-aa, aa, d);
  if (a <= 0.0) { discard; }
  return vec4<f32>(in.color.rgb, in.color.a * a * G.alpha);
}
`;

const TEX_WGSL = `
struct Globals { resolution : vec2<f32>, alpha : f32, _pad : f32, };
@group(0) @binding(0) var<uniform> G : Globals;
struct TexGlobals { pos : vec2<f32>, size : vec2<f32>, };
@group(0) @binding(1) var<uniform> T : TexGlobals;
@group(0) @binding(2) var samp : sampler;
@group(0) @binding(3) var tex  : texture_2d<f32>;

struct VSOut { @builtin(position) pos : vec4<f32>, @location(0) uv : vec2<f32> };
@vertex
fn vs(@builtin(vertex_index) vi : u32) -> VSOut {
  var quad = array<vec2<f32>, 6>(
    vec2<f32>(0.0,0.0), vec2<f32>(1.0,0.0), vec2<f32>(0.0,1.0),
    vec2<f32>(0.0,1.0), vec2<f32>(1.0,0.0), vec2<f32>(1.0,1.0)
  );
  let uv = quad[vi];
  let world = T.pos + uv * T.size;
  let ndc = vec2<f32>(
    (world.x / G.resolution.x) * 2.0 - 1.0,
    1.0 - (world.y / G.resolution.y) * 2.0
  );
  var out : VSOut;
  out.pos = vec4<f32>(ndc, 0.0, 1.0);
  out.uv = uv;
  return out;
}
@fragment
fn fs(in : VSOut) -> @location(0) vec4<f32> {
  let c = textureSample(tex, samp, in.uv);
  return vec4<f32>(c.rgb, c.a * G.alpha);
}
`;

const BLUR_WGSL = `
struct Globals { resolution : vec2<f32>, alpha : f32, _pad : f32, };
@group(0) @binding(0) var<uniform> G : Globals;
struct Dir { dir : vec2<f32>, texel : vec2<f32>, };
@group(0) @binding(1) var<uniform> D : Dir;
@group(0) @binding(2) var samp : sampler;
@group(0) @binding(3) var tex  : texture_2d<f32>;

struct VSOut { @builtin(position) pos : vec4<f32>, @location(0) uv : vec2<f32> };
@vertex
fn vs(@builtin(vertex_index) vi : u32) -> VSOut {
  var p = array<vec2<f32>, 3>(vec2<f32>(-1.0,-1.0), vec2<f32>(3.0,-1.0), vec2<f32>(-1.0,3.0));
  var out : VSOut;
  out.pos = vec4<f32>(p[vi], 0.0, 1.0);
  out.uv = p[vi] * 0.5 + 0.5;
  return out;
}
@fragment
fn fs(in : VSOut) -> @location(0) vec4<f32> {
  let w = array<f32,5>(0.227, 0.194, 0.121, 0.054, 0.016);
  var col = textureSample(tex, samp, in.uv) * w[0];
  for (var i = 1; i < 5; i = i + 1) {
    let off = D.dir * D.texel * f32(i) * 1.5;
    col = col + textureSample(tex, samp, in.uv + off) * w[i];
    col = col + textureSample(tex, samp, in.uv - off) * w[i];
  }
  return col;
}
`;

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
    _canvas.style.width = (_viewW * s) + 'px';
    _canvas.style.height = (_viewH * s) + 'px';
  };
  window.addEventListener('resize', fit);
  fit();

  // 异步请求设备，设备就绪后标记
  (async () => {
    try {
      const adapter = await navigator.gpu.requestAdapter();
      if (!adapter) throw new Error('no adapter');
      try { _dbg.adapterInfo = await adapter.requestAdapterInfo(); } catch (_) {}
      _device = await adapter.requestDevice();
      _device.lost.then((info) => {
        console.warn('[WebGPU] device lost:', info?.message);
        _dbg.lastErrors.push('device_lost: ' + (info?.message ?? 'unknown'));
      });
      // WebGPU validation error 监听：直接把 GPU 验证失败打印出来
      _device.addEventListener('uncaughterror', (e) => {
        const msg = e?.error?.message || String(e);
        console.error('[WebGPU] uncaughterror:', msg);
        _dbg.lastErrors.push('uncaughterror: ' + msg);
        if (_dbg.lastErrors.length > 32) _dbg.lastErrors.splice(0, _dbg.lastErrors.length - 32);
      });

      _ctx = _canvas.getContext('webgpu');
      _format = navigator.gpu.getPreferredCanvasFormat();
      _canvas.width = _viewW;
      _canvas.height = _viewH;
      _ctx.configure({
        device: _device,
        format: _format,
        alphaMode: 'opaque',
        usage: GPUTextureUsage.RENDER_ATTACHMENT | GPUTextureUsage.COPY_SRC | GPUTextureUsage.COPY_DST,
      });

      // 创建管线
      const shapeModule = _device.createShaderModule({ code: SHAPE_WGSL });
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
      const shadowModule = _device.createShaderModule({ code: SHADOW_WGSL });
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
      const texModule = _device.createShaderModule({ code: TEX_WGSL });
      _texPipeline = _device.createRenderPipeline({
        layout: texBlurLayout,
        vertex: { module: texModule, entryPoint: 'vs' },
        fragment: { module: texModule, entryPoint: 'fs', targets: [{ format: _format, blend: BLEND_PREMULTIPLIED }] },
        primitive: { topology: 'triangle-list' },
      });
      _texGlobalsBuffer = _device.createBuffer({ size: 16, usage: GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST });
      _sampler = _device.createSampler({ magFilter: 'linear', minFilter: 'linear' });

      // 模糊管线
      const blurModule = _device.createShaderModule({ code: BLUR_WGSL });
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
    _canvas.width = w; _canvas.height = h;
    _viewW = w; _viewH = h;
    if (_device) initOffscreen();
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
  if (_dbg.frameCount < 3) console.log('[TEX bake]', { w, h, nonTransparent: _nonTransparent, font: _dbg.lastFont });
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
  const m = _measureCtx.measureText(text);
  const w = Math.ceil(m.width) + 8;
  const h = 48;
  const tex = bakeToTexture((c) => {
    c.font = font;
    c.fillStyle = color;
    c.textAlign = 'left';
    c.textBaseline = 'middle';
    c.fillText(text, 4, h / 2);
  }, w, h);
  if (!tex) return;
  const id = _textures.length;
  _textures.push({ texture: tex, view: tex.createView(), w, h });
  let dx = x;
  if (align === 'center') dx = x - w / 2;
  else if (align === 'right') dx = x - w;
  _texData.push({ id, x: dx, y: y - h / 2, w, h });
}

function loadImageTexture(src) {
  if (!_device) return -1;
  const img = new Image();
  img.crossOrigin = 'anonymous';
  const id = _textures.length;
  _textures.push(null);
  img.onload = () => {
    const w = img.width, h = img.height;
    const tex = bakeToTexture((c) => c.drawImage(img, 0, 0), w, h);
    if (tex) _textures[id] = { texture: tex, view: tex.createView(), w, h };
  };
  img.onerror = () => { /* 忽略加载失败 */ };
  img.src = src;
  return id;
}

function drawImageTexture(id, x, y, w, h) {
  if (!_textures[id]) return;
  _texData.push({ id, x, y, w, h });
}

// ---------------------------------------------------------------------
//  帧渲染（整帧一次性）
// ---------------------------------------------------------------------
function renderFrame(shapesRaw, shadowsRaw) {
  const shapes = _toFloat32(shapesRaw);
  const shadows = _toFloat32(shadowsRaw);
  _dbg.frameCount++;

  if (_dbg.frameCount <= 3) {
    console.log('[frame]', _dbg.frameCount, 'texData=', _texData.map(t => ({ x: t.x|0, y: t.y|0, w: t.w|0, h: t.h|0, id: t.id })));
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
    writeGlobals(_alpha);
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
    // 2c) 纹理（文本 / 图片）：同一 pass，切换到 tex pipeline 每个 draw 一个 quad
    for (const t of _texData) drawTexturePass(pass, encoder, t, screen);
  }
  pass.end();
  _device.queue.submit([encoder.finish()]);
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

function drawTexturePass(pass, encoder, t, screenView) {
  const tex = _textures[t.id];
  if (!tex) return;
  _device.queue.writeBuffer(_texGlobalsBuffer, 0, new Float32Array([t.x, t.y, t.w, t.h]));
  const bg = _device.createBindGroup({
    layout: _texPipeline.getBindGroupLayout(0),
    entries: [
      { binding: 0, resource: { buffer: _globalsBuffer } },
      { binding: 1, resource: { buffer: _texGlobalsBuffer } },
      { binding: 2, resource: _sampler },
      { binding: 3, resource: tex.view },
    ],
  });
  pass.setPipeline(_texPipeline);
  pass.setBindGroup(0, bg);
  pass.draw(6);
}
