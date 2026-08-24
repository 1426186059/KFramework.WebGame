// 高斯模糊着色器（两趟：水平 / 垂直），用于阴影柔化。
// 全屏三角形，uv = p*0.5+0.5。
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
