#version 300 es
precision highp float;
layout(location = 0) in vec2 a_pos;
layout(location = 1) in vec4 a_rect;     // x, y, w, h (逻辑像素)
layout(location = 2) in vec4 a_color;    // r, g, b, a
layout(location = 3) in vec2 a_params;   // radius(像素), kind(0=圆角矩形,1=圆)
layout(location = 4) in mat3 a_matrix;   // 绘制调用时烘焙的变换（占 4,5,6）
uniform vec2 u_resolution;
out vec2 v_uv;         // 局部坐标，原点在矩形中心，单位=像素
out vec2 v_half;       // 半宽、半高（像素）
out vec4 v_color;
out vec2 v_params;
void main() {
    // a_rect = (x, y, w, h)，x/y 为左上角：quad 精确覆盖 [x, x+w]×[y, y+h]（1:1）
    vec2 local = vec2((a_pos.x * 0.5 + 0.5) * a_rect.z, (a_pos.y * 0.5 + 0.5) * a_rect.w);
    vec2 world = vec2(a_rect.x, a_rect.y) + local;
    vec3 t = a_matrix * vec3(world, 1.0);
    vec2 clip = (t.xy / (u_resolution * 0.5)) - 1.0;
    clip.y = -clip.y;
    gl_Position = vec4(clip, 0.0, 1.0);
    v_uv = local - a_rect.zw * 0.5;   // 中心原点（像素），供 SDF 使用
    v_half = a_rect.zw * 0.5;
    v_color = a_color;
    v_params = a_params;
}
