precision highp float;
attribute vec2 a_pos;
varying vec2 v_uv;
void main() {
    // 全屏 quad 无 clip.y 翻转：视口底部(a_pos.y=-1) ↔ 纹理底部(v=1)，保证模糊结果不倒置
    v_uv = vec2(a_pos.x * 0.5 + 0.5, 0.5 - a_pos.y * 0.5);
    gl_Position = vec4(a_pos, 0.0, 1.0);
}
