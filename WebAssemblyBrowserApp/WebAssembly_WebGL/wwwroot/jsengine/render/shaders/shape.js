// =====================================================================
// 形状着色器（SHAPE program）
// GLSL ES 3.00 + 显式 layout(location)：任何 GPU 驱动行为一致，
// 避免 ES 1.00 下 attribute 位置由编译器分配导致的平台差异。
// 用于绘制矩形 / 圆角矩形 / 圆（SDF 抗锯齿）。
// =====================================================================

export const SHAPE_VERT = `#version 300 es
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
`

export const SHAPE_FRAG = `#version 300 es
precision highp float;
in vec2 v_uv;
in vec2 v_half;
in vec4 v_color;
in vec2 v_params;
out vec4 fragColor;
// 标准圆角矩形 SDF（像素单位）：b=半宽高，r=圆角半径(像素)。
// 半径允许达到短边一半（胶囊形），不会退化成圆。
float sdRoundedBox(vec2 p, vec2 b, float r) {
    vec2 q = abs(p) - b + r;
    return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
}
void main() {
    float kind = v_params.y;
    float alpha = 1.0;
    if (kind > 0.5) {
        // 圆：v_params.x 为半径(像素)
        alpha = 1.0 - smoothstep(-1.0, 1.0, length(v_uv) - v_params.x);
    } else {
        float r = clamp(v_params.x, 0.0, min(v_half.x, v_half.y));
        alpha = 1.0 - smoothstep(-1.0, 1.0, sdRoundedBox(v_uv, v_half, r));
    }
    if (alpha <= 0.001) discard;
    fragColor = vec4(v_color.rgb, v_color.a * alpha);
}
`
