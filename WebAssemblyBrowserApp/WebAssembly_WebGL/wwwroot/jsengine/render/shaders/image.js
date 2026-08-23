// =====================================================================
// 图片/文字着色器（IMG program）
// GLSL ES 3.00 + 显式 layout(location)。
// 用于绘制纹理（离屏 Canvas2D 生成的文字纹理、加载的图片）。
// =====================================================================

export const IMG_VERT = `#version 300 es
precision highp float;
layout(location = 0) in vec2 a_pos;
layout(location = 1) in vec4 a_rect;     // x, y, w, h
layout(location = 2) in vec4 a_color;    // r, g, b, a
layout(location = 3) in mat3 a_matrix;   // 占 3,4,5
uniform vec2 u_resolution;
uniform vec4 u_uvRect;     // UV 裁剪区域（归一化）：x, y = 偏移，z, w = 宽高
out vec2 v_uv;
out vec4 v_color;
void main() {
    // a_rect = (x, y, w, h)，x/y 为左上角：quad 精确覆盖 [x, x+w]×[y, y+h]（1:1，文字不缩放）
    vec2 local = vec2((a_pos.x * 0.5 + 0.5) * a_rect.z, (a_pos.y * 0.5 + 0.5) * a_rect.w);
    vec2 world = vec2(a_rect.x, a_rect.y) + local;
    vec3 t = a_matrix * vec3(world, 1.0);
    vec2 clip = (t.xy / (u_resolution * 0.5)) - 1.0;
    clip.y = -clip.y;
    gl_Position = vec4(clip, 0.0, 1.0);
    v_uv = vec2(u_uvRect.x + (a_pos.x * 0.5 + 0.5) * u_uvRect.z,
                u_uvRect.y + (0.5 + a_pos.y * 0.5) * u_uvRect.w);   // 纹理正立：v=0（图片顶部）在 quad 上边
    v_color = a_color;
}
`

export const IMG_FRAG = `#version 300 es
precision highp float;
in vec2 v_uv;
in vec4 v_color;
uniform sampler2D u_tex;
out vec4 fragColor;
void main() {
    vec4 tex = texture(u_tex, v_uv);
    fragColor = tex * v_color;
}
`
