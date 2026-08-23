// =====================================================================
// 高斯模糊着色器（BLUR program）
// 9 抽头高斯核。当前阴影实现为「同批次半透明偏移副本」（见 renderer.js
// pushInstance），此 FBO 模糊管线暂未启用，保留以备后续阴影质量升级。
// =====================================================================

export const BLUR_VERT = `
precision highp float;
attribute vec2 a_pos;
varying vec2 v_uv;
void main() {
    // 全屏 quad 无 clip.y 翻转：视口底部(a_pos.y=-1) ↔ 纹理底部(v=1)，保证模糊结果不倒置
    v_uv = vec2(a_pos.x * 0.5 + 0.5, 0.5 - a_pos.y * 0.5);
    gl_Position = vec4(a_pos, 0.0, 1.0);
}
`

export const BLUR_FRAG = `
precision highp float;
varying vec2 v_uv;
uniform sampler2D u_tex;
uniform vec2 u_texel;
uniform vec2 u_dir;
void main() {
    vec4 sum = vec4(0.0);
    sum += texture2D(u_tex, v_uv + u_dir * u_texel * -4.0) * 0.05;
    sum += texture2D(u_tex, v_uv + u_dir * u_texel * -3.0) * 0.09;
    sum += texture2D(u_tex, v_uv + u_dir * u_texel * -2.0) * 0.12;
    sum += texture2D(u_tex, v_uv + u_dir * u_texel * -1.0) * 0.15;
    sum += texture2D(u_tex, v_uv) * 0.18;
    sum += texture2D(u_tex, v_uv + u_dir * u_texel * 1.0) * 0.15;
    sum += texture2D(u_tex, v_uv + u_dir * u_texel * 2.0) * 0.12;
    sum += texture2D(u_tex, v_uv + u_dir * u_texel * 3.0) * 0.09;
    sum += texture2D(u_tex, v_uv + u_dir * u_texel * 4.0) * 0.05;
    gl_FragColor = sum;
}
`
