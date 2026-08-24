#version 300 es
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
