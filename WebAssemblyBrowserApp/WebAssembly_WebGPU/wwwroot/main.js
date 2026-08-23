// =====================================================================
//  WebGPU 后端 —— 通过 [JSImport] 被 C# (.NET 10 WASM) 直接调用
//  渲染模型：实例化四边形 + WGSL（矩形 / 圆 / 圆角矩形 / 线）
//            + 离屏纹理两次高斯模糊实现真阴影
//            + Canvas2D 离屏烘焙 → GPUTexture 实现真文本 / 图片贴图
// =====================================================================

const SHAPE_STRIDE = 13;           // 每个实例的 float 数
const SHAPE_FLOATS = SHAPE_STRIDE;

let _device = null;
let _ctx = null;
let _canvas = null;
let _format = null;
let _viewW = 960, _viewH = 540;

// 实例批次（CPU 侧累积，帧末一次性上传）
let _shapeData = [];          // 普通形状
let _shadowData = [];         // 阴影副本（开启阴影时额外压入）
let _shapeBuffer = null;      // GPUBuffer (VERTEX, 实例属性)
let _shadowBuffer = null;

// 纹理（文本 / 图片）
let _texBuffer = null;        // 纹理实例属性 buffer
let _texData = [];
const _textures = [];         // {texture, view, w, h}

// 离屏阴影资源
let _shadowTex = null, _shadowView = null, _shadowTexW = 0, _shadowTexH = 0;
let _blurTex = null, _blurView = null;

// 状态
let _matrix = [1, 0, 0, 0, 1, 0];       // [a,b,c,d,e,f] 仿射
const _matrixStack = [];
let _alpha = 1.0;
let _shadow = null;                       // {ox,oy,blur,r,g,b,a}
let _clearColor = [0.05, 0.07, 0.09, 1];

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
  @location(0) rectPos  : vec2<f32>,   // 左上角 (CSS px)
  @location(1) rectSize : vec2<f32>,   // 宽高
  @location(2) halfSize : vec2<f32>,   // 用于 SDF 的半尺寸
  @location(3) radius   : f32,         // 圆角半径 / 圆半径
  @location(4) uType    : f32,         // 0=rect 1=roundRect 2=circle 3=line
  @location(5) color    : vec4<f32>,
  @location(6) lineW    : f32,
};

struct VSOut {
  @builtin(position) pos : vec4<f32>,
  @location(0) uv       : vec2<f32>,   // 局部坐标 -1..1
  @location(1) half     : vec2<f32>,
  @location(2) radius   : f32,
  @location(3) uType    : f32,
  @location(4) color    : vec4<f32>,
  @location(5) lineW    : f32,
};

@vertex
fn vs(@builtin(vertex_index) vi : u32, inst : Inst) -> VSOut {
  // 单位四边形 (-1..1)
  var quad = array<vec2<f32>, 6>(
    vec2<f32>(-1.0, -1.0), vec2<f32>( 1.0, -1.0), vec2<f32>(-1.0, 1.0),
    vec2<f32>(-1.0,  1.0), vec2<f32>( 1.0, -1.0), vec2<f32>( 1.0, 1.0)
  );
  let q = quad[vi];
  let local = q * inst.halfSize;                 // 像素偏移
  let world = inst.rectPos + inst.halfSize + local; // 像素坐标
  var out : VSOut;
  // 翻转到 WebGPU 坐标 (y 朝下 → 朝上)
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
    // 矩形：永远填充
    d = -1.0;
  } else if (in.uType < 1.5) {
    d = sdRoundRect(in.uv * in.half, in.half, in.radius);
  } else if (in.uType < 2.5) {
    d = length(in.uv * in.half) - in.radius;
  } else {
    // 线：到线段的距离
    let x = abs(in.uv.x);
    let dist = max(x - (1.0 - in.lineW / (2.0 * in.half.x)),
                   abs(in.uv.y) - in.lineW / (2.0 * in.half.y));
    d = dist;
  }
  // 抗锯齿
  let aa = 1.0;
  var a = 1.0 - smoothstep(-aa, aa, d);
  if (a <= 0.0) { discard; }
  return vec4<f32>(in.color.rgb, in.color.a * a * G.alpha);
}
`;

// 纯色铺满全屏（用于清屏 / 模糊 pass 合成 / 半透明遮罩）
const FULLSCREEN_WGSL = `
struct Globals { resolution : vec2<f32>, alpha : f32, _pad : f32, };
@group(0) @binding(0) var<uniform> G : Globals;
@vertex
fn vs(@builtin(vertex_index) vi : u32) -> @builtin(position) vec4<f32> {
  var p = array<vec2<f32>, 3>(
    vec2<f32>(-1.0,-1.0), vec2<f32>(3.0,-1.0), vec2<f32>(-1.0,3.0)
  );
  return vec4<f32>(p[vi], 0.0, 1.0);
}
@fragment
fn fs() -> @location(0) vec4<f32> {
  return vec4<f32>(0.0,0.0,0.0,1.0);
}
`;

// 纹理（文本 / 图片）采样贴图
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

// 高斯模糊（9-tap），dir.x/dir.y 决定方向
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
let _shapePipeline = null, _shapeBindGroup = null;
let _texPipeline = null;
let _blurPipeline = null;
let _globalsBuffer = null;
let _texGlobalsBuffer = null, _blurDirBuffer = null;
let _sampler = null;

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

async function gpu_init() {
  if (!navigator.gpu) throw new Error("此浏览器不支持 WebGPU");
  const adapter = await navigator.gpu.requestAdapter();
  if (!adapter) throw new Error("找不到 WebGPU adapter");
  _device = await adapter.requestDevice();

  _canvas = document.querySelector("#game");
  _ctx = _canvas.getContext("webgpu");
  _format = navigator.gpu.getPreferredCanvasFormat();
  _ctx.configure({ device: _device, format: _format, alphaMode: "opaque" });

  _viewW = _canvas.width;
  _viewH = _canvas.height;

  const shapeModule = _device.createShaderModule({ code: SHAPE_WGSL });
  _shapePipeline = _device.createRenderPipeline({
    layout: "auto",
    vertex: { module: shapeModule, entryPoint: "vs" },
    fragment: { module: shapeModule, entryPoint: "fs", targets: [{ format: _format }] },
    primitive: { topology: "triangle-list" },
  });
  makeGlobalsBuffer();
  _shapeBindGroup = _device.createBindGroup({
    layout: _shapePipeline.getBindGroupLayout(0),
    entries: [{ binding: 0, resource: { buffer: _globalsBuffer } }],
  });

  // 实例缓冲（动态，足够大：4096 个实例）
  _shapeBuffer = _device.createBuffer({
    size: SHAPE_FLOATS * 4096 * 4,
    usage: GPUBufferUsage.VERTEX | GPUBufferUsage.COPY_DST,
  });
  _shadowBuffer = _device.createBuffer({
    size: SHAPE_FLOATS * 4096 * 4,
    usage: GPUBufferUsage.VERTEX | GPUBufferUsage.COPY_DST,
  });

  // 纹理管线
  const texModule = _device.createShaderModule({ code: TEX_WGSL });
  _texPipeline = _device.createRenderPipeline({
    layout: "auto",
    vertex: { module: texModule, entryPoint: "vs" },
    fragment: { module: texModule, entryPoint: "fs", targets: [{ format: _format }] },
    primitive: { topology: "triangle-list" },
  });
  _texGlobalsBuffer = _device.createBuffer({ size: 16, usage: GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST });
  _sampler = _device.createSampler({ magFilter: "linear", minFilter: "linear" });

  // 模糊管线
  const blurModule = _device.createShaderModule({ code: BLUR_WGSL });
  _blurPipeline = _device.createRenderPipeline({
    layout: "auto",
    vertex: { module: blurModule, entryPoint: "vs" },
    fragment: { module: blurModule, entryPoint: "fs", targets: [{ format: _format }] },
    primitive: { topology: "triangle-list" },
  });
  _blurDirBuffer = _device.createBuffer({ size: 16, usage: GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST });

  initOffscreen();
  console.log("[WebGPU] 设备就绪");
  // 通知 C# 侧：设备已就绪，可以启动场景与主循环
  if (typeof GameEngine !== "undefined" && GameEngine.EngineReady) {
    GameEngine.EngineReady();
  }
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
//  矩阵工具
// ---------------------------------------------------------------------
function mul(a, b) { // a * b  都是 [a,b,c,d,e,f]
  return [
    a[0]*b[0] + a[2]*b[1],
    a[1]*b[0] + a[3]*b[1],
    a[0]*b[2] + a[2]*b[3],
    a[1]*b[2] + a[3]*b[3],
    a[0]*b[4] + a[2]*b[5] + a[4],
    a[1]*b[4] + a[3]*b[5] + a[5],
  ];
}
function translateMatrix(x, y) { return [1,0,0,1,x,y]; }

// 把 (x,y) 经当前矩阵变换（用于把形状局部坐标正确放置，但着色器里我们用 CPU 预变换 rectPos）
function tx(x, y) {
  return [
    _matrix[0]*x + _matrix[2]*y + _matrix[4],
    _matrix[1]*x + _matrix[3]*y + _matrix[5],
  ];
}

function parseColor(c) {
  if (typeof c !== "string") return [0,0,0,1];
  c = c.trim();
  if (c[0] === "#") {
    let hex = c.slice(1);
    if (hex.length === 3) hex = hex.split("").map(x => x+x).join("");
    if (hex.length === 8) {
      const r = parseInt(hex.slice(0,2),16)/255;
      const g = parseInt(hex.slice(2,4),16)/255;
      const b = parseInt(hex.slice(4,6),16)/255;
      const a = parseInt(hex.slice(6,8),16)/255;
      return [r,g,b,a];
    }
    const r = parseInt(hex.slice(0,2),16)/255;
    const g = parseInt(hex.slice(2,4),16)/255;
    const b = parseInt(hex.slice(4,6),16)/255;
    return [r,g,b,1];
  }
  // rgb() / rgba()
  const m = c.match(/rgba?\(([^)]+)\)/);
  if (m) {
    const p = m[1].split(",").map(s => parseFloat(s));
    return [p[0]/255, p[1]/255, p[2]/255, p[3] === undefined ? 1 : p[3]];
  }
  return [1,1,1,1];
}

// ---------------------------------------------------------------------
//  批处理写入
// ---------------------------------------------------------------------
function pushInstance(arr, px, py, w, h, radius, type, color, lineW) {
  const cc = parseColor(color);
  const cx = px + w / 2, cy = py + h / 2;
  // 左上角经矩阵变换到屏幕
  const tl = tx(px, py);
  const halfW = w / 2, halfH = h / 2;
  // 矩阵含旋转时半尺寸不准确，这里对纯平移/缩放足够
  arr.push(
    tl[0], tl[1],          // rectPos
    w, h,                  // rectSize（传给着色器用于位置计算实际用 rectPos+half）
    halfW, halfH,          // halfSize（SDF 局部）
    radius, type,
    cc[0], cc[1], cc[2], cc[3],
    lineW || 0
  );
}

// ---------------------------------------------------------------------
//  公开 API（被 C# [JSImport] 调用）
// ---------------------------------------------------------------------
globalThis.gpu = {
  init: gpu_init,

  resize(w, h) {
    if (!_canvas) return;
    _canvas.width = w; _canvas.height = h;
    _viewW = w; _viewH = h;
    if (_device) initOffscreen();
  },

  beginFrame(r, g, b, a) { _clearColor = [r, g, b, a]; _shapeData = []; _shadowData = []; _texData = []; },

  clear(color) {
    const c = parseColor(color);
    _clearColor = c;
    _shapeData = []; _shadowData = []; _texData = [];
  },

  endFrame() {
    renderFrame();
    _shapeData = []; _shadowData = []; _texData = [];
  },

  setTransform(m11, m12, m21, m22, dx, dy) { _matrix = [m11, m12, m21, m22, dx, dy]; },
  resetTransform() { _matrix = [1, 0, 0, 0, 1, 0]; },
  saveTransform() { _matrixStack.push(_matrix.slice()); },
  restoreTransform() { if (_matrixStack.length) _matrix = _matrixStack.pop(); },
  translate(x, y) { _matrix = mul(_matrix, translateMatrix(x, y)); },
  setAlpha(a) { _alpha = a; },

  fillRect(x, y, w, h, color) { pushInstance(_shapeData, x, y, w, h, 0, 0, color, 0); shadowPush(x, y, w, h, 0, 0, color); },
  roundedRect(x, y, w, h, r, color) { pushInstance(_shapeData, x, y, w, h, r, 1, color, 0); shadowPush(x, y, w, h, r, 1, color); },
  fillCircle(cx, cy, r, color) { pushInstance(_shapeData, cx - r, cy - r, r * 2, r * 2, r, 2, color, 0); shadowPush(cx - r, cy - r, r * 2, r * 2, r, 2, color); },
  drawLine(x1, y1, x2, y2, width, color) {
    const minx = Math.min(x1, x2), miny = Math.min(y1, y2);
    const w = Math.abs(x2 - x1) + width, h = Math.abs(y2 - y1) + width;
    pushInstance(_shapeData, minx, miny, w, h, 0, 3, color, width);
  },

  shadow(ox, oy, blur, r, g, b, a) { _shadow = { ox, oy, blur, color: [r, g, b, a] }; },
  shadowColor(color, blur) { const c = parseColor(color); _shadow = { ox: 0, oy: 0, blur, color: c }; },
  noShadow() { _shadow = null; },

  fillText(text, x, y, font, color, align) { drawTextSprite(text, x, y, font, color, align); },
  loadImage(src) { return loadImageTexture(src); },
  drawImage(id, x, y, w, h) { drawImageTexture(id, x, y, w, h); },
  measureText(text, font) {
    _measureCtx.font = font;
    return _measureCtx.measureText(text).width;
  },
};

function shadowPush(x, y, w, h, r, type, color) {
  if (!_shadow) return;
  // 阴影用其自身颜色，稍偏移
  pushInstance(_shadowData, x + _shadow.ox, y + _shadow.oy, w, h, r, type, _shadow.color, 0);
}

// ---------------------------------------------------------------------
//  文本 / 图片：离屏 Canvas2D 烘焙 → GPUTexture
// ---------------------------------------------------------------------
const _off = document.createElement("canvas");
const _octx = _off.getContext("2d");
const _measure = document.createElement("canvas");
const _measureCtx = _measure.getContext("2d");

function bakeToTexture(drawFn, w, h) {
  _off.width = w; _off.height = h;
  _octx.clearRect(0, 0, w, h);
  drawFn(_octx);
  const tex = _device.createTexture({
    size: [w, h], format: "rgba8unorm",
    usage: GPUTextureUsage.TEXTURE_BINDING | GPUTextureUsage.COPY_DST | GPUTextureUsage.RENDER_ATTACHMENT,
  });
  _device.queue.writeTexture(
    { texture: tex },
    new Uint8Array(_octx.getImageData(0, 0, w, h).data.buffer),
    { bytesPerRow: w * 4, rowsPerPixel: 1 },
    { width: w, height: h }
  );
  return tex;
}

function drawTextSprite(text, x, y, font, color, align) {
  _measureCtx.font = font;
  const m = _measureCtx.measureText(text);
  const w = Math.ceil(m.width) + 8;
  const h = 48;
  const tex = bakeToTexture((c) => {
    c.font = font;
    c.fillStyle = color;
    c.textAlign = "left";
    c.textBaseline = "middle";
    c.fillText(text, 4, h / 2);
  }, w, h);
  const id = _textures.length;
  _textures.push({ texture: tex, view: tex.createView(), w, h });
  // align 决定水平锚点
  let dx = x;
  if (align === "center") dx = x - w / 2;
  else if (align === "right") dx = x - w;
  _texData.push({ id, x: dx, y: y - h / 2, w, h });
}

function loadImageTexture(src) {
  const img = new Image();
  img.crossOrigin = "anonymous";
  const id = _textures.length;
  _textures.push(null); // 占位
  img.onload = () => {
    const w = img.width, h = img.height;
    const tex = bakeToTexture((c) => c.drawImage(img, 0, 0), w, h);
    _textures[id] = { texture: tex, view: tex.createView(), w, h };
  };
  img.src = src;
  return id;
}

function drawImageTexture(id, x, y, w, h) {
  if (!_textures[id]) return;
  _texData.push({ id, x, y, w, h });
}

// ---------------------------------------------------------------------
//  帧渲染
// ---------------------------------------------------------------------
function renderFrame() {
  if (!_device) return;

  // 1) 若有阴影：先把阴影批次渲到离屏纹理，再两次模糊，最后合成到屏幕（在形状之前）
  if (_shadowData.length > 0) {
    renderShapesToView(_shadowData, _shadowView, 1.0, false);
    // H 模糊 shadowTex -> blurTex
    runBlur(_shadowView, _blurView, [1, 0]);
    // V 模糊 blurTex -> 屏幕（合成）
    compositeBlur(_blurView);
  }

  // 2) 主形状渲到屏幕
  const encoder = _device.createCommandEncoder();
  const pass = encoder.beginRenderPass({
    colorAttachments: [{
      view: _ctx.getCurrentTexture().createView(),
      clearValue: { r: _clearColor[0], g: _clearColor[1], b: _clearColor[2], a: _clearColor[3] },
      loadOp: "clear", storeOp: "store",
    }],
  });
  writeGlobals(_alpha);
  if (_shapeData.length > 0) {
    const data = new Float32Array(_shapeData);
    _device.queue.writeBuffer(_shapeBuffer, 0, data);
    pass.setPipeline(_shapePipeline);
    pass.setBindGroup(0, _shapeBindGroup);
    pass.setVertexBuffer(0, _shapeBuffer);
    pass.draw(6, _shapeData.length / SHAPE_FLOATS);
  }
  // 3) 纹理（文本 / 图片）
  for (const t of _texData) drawTexturePass(encoder, t, _ctx.getCurrentTexture().createView());
  pass.end();
  _device.queue.submit([encoder.finish()]);
}

function renderShapesToView(data, view, alpha, clear) {
  const encoder = _device.createCommandEncoder();
  const pass = encoder.beginRenderPass({
    colorAttachments: [{
      view,
      clearValue: { r: 0, g: 0, b: 0, a: 0 },
      loadOp: clear ? "clear" : "clear", storeOp: "store",
    }],
  });
  writeGlobals(alpha);
  const arr = new Float32Array(data);
  _device.queue.writeBuffer(_shadowBuffer, 0, arr);
  pass.setPipeline(_shapePipeline);
  pass.setBindGroup(0, _shapeBindGroup);
  pass.setVertexBuffer(0, _shadowBuffer);
  pass.draw(6, data.length / SHAPE_FLOATS);
  pass.end();
  _device.queue.submit([encoder.finish()]);
}

function runBlur(srcView, dstView, dir) {
  const encoder = _device.createCommandEncoder();
  const pass = encoder.beginRenderPass({
    colorAttachments: [{ view: dstView, clearValue: { r: 0, g: 0, b: 0, a: 0 }, loadOp: "clear", storeOp: "store" }],
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

function compositeBlur(blurView) {
  // 把模糊后的阴影纹理用加法式合成到屏幕（alpha 混合即可产生柔和阴影）
  const encoder = _device.createCommandEncoder();
  const pass = encoder.beginRenderPass({
    colorAttachments: [{ view: _ctx.getCurrentTexture().createView(), loadOp: "load", storeOp: "store" }],
  });
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
  pass.end();
  _device.queue.submit([encoder.finish()]);
}

function drawTexturePass(encoder, t, screenView) {
  const tex = _textures[t.id];
  if (!tex) return;
  const pass = encoder.beginRenderPass({
    colorAttachments: [{ view: screenView, loadOp: "load", storeOp: "store" }],
  });
  writeGlobals(1.0);
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
  pass.end();
}

// ---------------------------------------------------------------------
//  引擎主循环（requestAnimationFrame 驱动 C# 的 GameBridge.Tick）
// ---------------------------------------------------------------------
globalThis.engine = {
  startLoop() {
    let last = performance.now();
    function frame(now) {
      const dt = (now - last) / 1000;
      last = now;
      GameBridge.Tick(dt);
      requestAnimationFrame(frame);
    }
    requestAnimationFrame(frame);
  }
};

// 输入桥
function keyName(e) { return e.code; }
window.addEventListener("keydown", (e) => {
  if (!globalThis.__keys) globalThis.__keys = {};
  globalThis.__keys[e.code] = true;
});
window.addEventListener("keyup", (e) => {
  if (globalThis.__keys) globalThis.__keys[e.code] = false;
});
globalThis.input = {
  isKeyDown(code) { return !!(globalThis.__keys && globalThis.__keys[code]); },
  mouseX() { return (globalThis.__mouseX || 0); },
  mouseY() { return (globalThis.__mouseY || 0); },
};
window.addEventListener("mousemove", (e) => {
  const c = document.querySelector("#game");
  const r = c.getBoundingClientRect();
  globalThis.__mouseX = (e.clientX - r.left) * (c.width / r.width);
  globalThis.__mouseY = (e.clientY - r.top) * (c.height / r.height);
});
globalThis.__mousePressed = false;
window.addEventListener("mousedown", () => { globalThis.__mousePressed = true; });
window.addEventListener("mouseup", () => { globalThis.__mousePressed = false; });
globalThis.input.isMousePressed = () => globalThis.__mousePressed;

// ---- C# 需要的输入 API ----
globalThis.Input = {
  init() {},
  isKeyDown(code) { return globalThis.input.isKeyDown(code); },
  isKeyPressed(code) { return globalThis.input.isKeyDown(code); },
  mouseX() { return globalThis.input.mouseX(); },
  mouseY() { return globalThis.input.mouseY(); },
  isMousePressed() { return globalThis.input.isMousePressed(); },
  endFrame() {},
};
globalThis.Input.ArrowLeft = "ArrowLeft";
globalThis.Input.ArrowRight = "ArrowRight";
globalThis.Input.KeyA = "KeyA";
globalThis.Input.KeyD = "KeyD";
globalThis.Input.Space = "Space";
globalThis.Input.Enter = "Enter";

// ---- 音频桥（WebAudio 波形音效） ----
globalThis.Audio = {
  init() {},
  beep(freq, dur, type, vol) {
    try {
      const AC = window.AudioContext || window.webkitAudioContext;
      if (!AC) return;
      if (!globalThis.__ac) globalThis.__ac = new AC();
      const ac = globalThis.__ac;
      const o = ac.createOscillator();
      const g = ac.createGain();
      o.type = type || "square";
      o.frequency.value = freq;
      g.gain.value = vol || 0.1;
      o.connect(g); g.connect(ac.destination);
      o.start();
      g.gain.exponentialRampToValueAtTime(0.0001, ac.currentTime + (dur || 0.1));
      o.stop(ac.currentTime + (dur || 0.1));
    } catch (e) {}
  }
};

// ---- 存储桥（localStorage） ----
globalThis.Storage = {
  get(k, d) { try { return localStorage.getItem(k) ?? d; } catch (e) { return d; } },
  set(k, v) { try { localStorage.setItem(k, v); } catch (e) {} },
};

// ---- 别名（C# 直接调用的方法在 globalThis.gpu / globalThis.engine 上） ----
window.addEventListener("DOMContentLoaded", () => {
  // 标记 WebGPU 模式
  document.body.setAttribute("data-renderer", "webgpu");
});
