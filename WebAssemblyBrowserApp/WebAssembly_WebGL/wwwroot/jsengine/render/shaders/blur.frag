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
