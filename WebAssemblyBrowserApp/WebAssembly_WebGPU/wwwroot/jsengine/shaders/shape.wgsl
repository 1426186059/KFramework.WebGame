// 主形状着色器：矩形 / 圆角矩形 / 圆 / 线（SDF + 抗锯齿）。
// 渲染到屏幕 swapchain，vs 做 Y 翻转（世界 y 向下 → NDC 向上）。
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
