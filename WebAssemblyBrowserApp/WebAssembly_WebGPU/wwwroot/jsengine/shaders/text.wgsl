// 纹理（文本 / 图片）着色器：把离屏 Canvas2D 烘焙的纹理按 quad 画到屏幕。
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
