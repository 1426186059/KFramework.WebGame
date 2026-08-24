#version 300 es
precision highp float;
in vec2 v_uv;
in vec4 v_color;
uniform sampler2D u_tex;
out vec4 fragColor;
void main() {
    vec4 tex = texture(u_tex, v_uv);
    fragColor = tex * v_color;
}
