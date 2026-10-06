using System;
using System.Collections.Generic;
using System.Diagnostics;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.WebGPU.Tests
{
    /// <summary>
    /// 自定义 Effect 测试：用 SpriteBatch.Begin(effect: 自定义着色器) 把一段 GLSL 片元着色器套到精灵上。
    /// <para>
    /// 每个 Effect 都是 <see cref="ShaderEffect"/>：场景每帧写入 <see cref="ShaderEffect.Time"/> / <see cref="ShaderEffect.Params"/>，
    /// 绘制时由后端的自定义精灵程序灌入 uTime / uParams uniform。WebGL 下真正生效；WebGPU 当前回落默认精灵着色器（画面照常，效果不套用）。
    /// </para>
    /// <para>空格键：网格总览 ⇄ 单图查看。共 8 个效果：高斯模糊 / 发光描边 / 水波 / Metaball / 阴影 / 马赛克 / 反相 / 扫描线。</para>
    /// </summary>
    public sealed class EffectScene : DemoScene
    {
        public override string Title => "自定义 Effect 测试（高斯模糊 / 描边发光 / 水波 / Metaball / 阴影 / 马赛克 / 反相 / 扫描线）";

        protected override string Description =>
            "用 SpriteBatch.Begin(effect: 自定义 GLSL 片元着色器) 套用效果；WebGL 下生效，WebGPU 回落默认精灵着色器。" +
            "每帧写入 Effect.Time 驱动动画。空格键：网格总览 ⇄ 单图查看。";

        private Texture2D? _tex;
        private readonly List<EffectEntry> _effects = new();
        private bool _solo;
        private int _soloIndex;
        private readonly Stopwatch _sw = Stopwatch.StartNew();

        private sealed class EffectEntry
        {
            public required string Name;
            public required ShaderEffect Effect;
            public System.Numerics.Vector4 BaseParams;
        }

        protected override void UpdateBody()
        {
            if (Input_KeyBoard.GetKeyDown(Keys.Space))
            {
                _solo = !_solo;
                if (_effects.Count > 0)
                    _soloIndex = (_soloIndex + 1) % _effects.Count;
            }
        }

        protected override void DrawBody(SpriteBatch batch, float top)
        {
            _tex ??= MakeTestChart(256);
            if (_effects.Count == 0) BuildEffects();

            float t = (float)_sw.Elapsed.TotalSeconds;

            if (_solo && _effects.Count > 0)
            {
                EffectEntry e = _effects[_soloIndex];
                float size = Math.Min(Device.Viewport.Width, Device.Viewport.Height) - 200f;
                float x = (Device.Viewport.Width - size) / 2f;
                float y = top + 16f;
                e.Effect.Time = t;
                e.Effect.Params = e.BaseParams;
                batch.Begin(SpriteSortMode.Deferred, null, null, null, null, e.Effect);
                batch.Draw(_tex, new Rectangle((int)x, (int)y, (int)size, (int)size), Color.White);
                batch.End();

                batch.Begin();
                batch.DrawString(Font, $"{e.Name}（空格返回总览）", new Vector2(x, y - 28f), new Color(220, 235, 255));
                batch.End();
                return;
            }

            const float cell = 200f, gap = 24f;
            int cols = Math.Max(1, (int)((Device.Viewport.Width - 56f) / (cell + gap)));
            float x0 = 28f, y0 = top + 8f;

            for (int i = 0; i < _effects.Count; i++)
            {
                int c = i % cols;
                int r = i / cols;
                float x = x0 + c * (cell + gap);
                float y = y0 + r * (cell + gap + 26f);
                DrawCell(batch, _effects[i], t, x, y, cell);
            }
        }

        private void DrawCell(SpriteBatch batch, EffectEntry e, float t, float x, float y, float size)
        {
            e.Effect.Time = t;
            e.Effect.Params = e.BaseParams;
            batch.Begin(SpriteSortMode.Deferred, null, null, null, null, e.Effect);
            batch.Draw(_tex, new Rectangle((int)x, (int)y, (int)size, (int)size), Color.White);
            batch.End();

            batch.Begin();
            batch.DrawString(Font, e.Name, new Vector2(x, y + size + 4f), new Color(200, 220, 255));
            batch.End();
        }

        private void BuildEffects()
        {
            int w = _tex?.Width ?? 256;
            int h = _tex?.Height ?? 256;
            var V4 = (float a, float b, float c, float d) => new System.Numerics.Vector4(a, b, c, d);

            void Add(string name, string frag, System.Numerics.Vector4 p)
            {
                var eff = (ShaderEffect)Device.CreateShaderEffect(frag);
                _effects.Add(new EffectEntry { Name = name, Effect = eff, BaseParams = p });
            }

            Add("高斯模糊", FragBlur, V4(w, h, 2f, 0f));
            Add("发光描边", FragGlow, V4(w, h, 0.35f, 0f));
            Add("水波特效", FragWater, V4(0f, 0f, 2f, 0f));
            Add("Metaball", FragMetaball, System.Numerics.Vector4.Zero);
            Add("阴影", FragShadow, V4(0f, 0f, 0.04f, 0.06f));
            Add("马赛克", FragPixelate, V4(14f, 0f, 0f, 0f));
            Add("反相", FragInvert, System.Numerics.Vector4.Zero);
            Add("扫描线/CRT", FragScanline, V4(220f, 0f, 0f, 0f));
        }

        // ============================================================
        // 自定义片元着色器（GLSL ES 3.00）
        // 约定：顶点复用标准精灵顶点（aPosition / aTexCoord / aColor + uProjection）；
        // 片元可用 vTexCoord / vColor / uTexture，外加 uTime（动画时间）与 uParams（vec4）。
        // ============================================================

        private const string FragBlur = @"
#version 300 es
precision highp float;
in vec2 vTexCoord;
in vec4 vColor;
uniform sampler2D uTexture;
uniform float uTime;
uniform vec4 uParams; // x=W, y=H, z=模糊半径(px)
out vec4 fragColor;
void main(){
  vec2 texel = vec2(1.0/uParams.x, 1.0/uParams.y);
  float r = max(uParams.z, 0.0);
  vec4 sum = vec4(0.0);
  float wsum = 0.0;
  for(int i=-2;i<=2;i++){
    for(int j=-2;j<=2;j++){
      vec2 off = vec2(float(i), float(j)) * texel * r;
      float w = exp(-(float(i*i + j*j)) / 4.0);
      sum += texture(uTexture, vTexCoord + off) * w;
      wsum += w;
    }
  }
  fragColor = sum / wsum * vColor;
}";

        private const string FragGlow = @"
#version 300 es
precision highp float;
in vec2 vTexCoord;
in vec4 vColor;
uniform sampler2D uTexture;
uniform float uTime;
uniform vec4 uParams; // x=W, y=H, z=边缘阈值
out vec4 fragColor;
void main(){
  vec2 texel = vec2(1.0/uParams.x, 1.0/uParams.y);
  float a = texture(uTexture, vTexCoord).a;
  float edge = 0.0;
  for(int i=-1;i<=1;i++){
    for(int j=-1;j<=1;j++){
      if(i==0 && j==0) continue;
      float na = texture(uTexture, vTexCoord + vec2(float(i), float(j)) * texel * 2.0).a;
      edge += step(uParams.z, na) * (1.0 - step(uParams.z, a));
    }
  }
  vec3 glow = vec3(0.25, 0.9, 1.0);
  vec4 base = texture(uTexture, vTexCoord) * vColor;
  float g = clamp(edge, 0.0, 1.0);
  fragColor = vec4(base.rgb + glow * g, max(base.a, g));
}";

        private const string FragWater = @"
#version 300 es
precision highp float;
in vec2 vTexCoord;
in vec4 vColor;
uniform sampler2D uTexture;
uniform float uTime;
uniform vec4 uParams; // z=振幅
out vec4 fragColor;
void main(){
  float amp = uParams.z * 0.01;
  float freq = 16.0;
  vec2 uv = vTexCoord;
  uv.x += sin(vTexCoord.y * freq + uTime * 2.0) * amp;
  uv.y += cos(vTexCoord.x * freq + uTime * 1.7) * amp;
  fragColor = texture(uTexture, uv) * vColor;
}";

        private const string FragMetaball = @"
#version 300 es
precision highp float;
in vec2 vTexCoord;
in vec4 vColor;
uniform sampler2D uTexture;
uniform float uTime;
uniform vec4 uParams;
out vec4 fragColor;
void main(){
  vec2 p = vTexCoord;
  float t = uTime;
  float f = 0.0;
  for(int k=0;k<3;k++){
    float fk = float(k);
    vec2 c = vec2(0.5 + 0.32 * sin(t * 1.1 + fk * 2.1),
                  0.5 + 0.32 * cos(t * 0.9 + fk * 1.7));
    float d = distance(p, c);
    f += 0.03 / (d * d + 0.002);
  }
  float ball = smoothstep(0.8, 1.3, f);
  vec4 tex = texture(uTexture, p);
  vec3 col = mix(tex.rgb, vec3(0.3, 0.85, 1.0), ball);
  fragColor = vec4(col * vColor.rgb, tex.a);
}";

        private const string FragShadow = @"
#version 300 es
precision highp float;
in vec2 vTexCoord;
in vec4 vColor;
uniform sampler2D uTexture;
uniform float uTime;
uniform vec4 uParams; // z=偏移X(uv), w=偏移Y(uv)
out vec4 fragColor;
void main(){
  vec2 off = uParams.zw;
  float a = texture(uTexture, vTexCoord - off).a;
  fragColor = vec4(0.0, 0.0, 0.0, a * 0.6);
}";

        private const string FragPixelate = @"
#version 300 es
precision highp float;
in vec2 vTexCoord;
in vec4 vColor;
uniform sampler2D uTexture;
uniform float uTime;
uniform vec4 uParams; // x=格子数
out vec4 fragColor;
void main(){
  float cells = max(uParams.x, 2.0);
  vec2 uv = floor(vTexCoord * cells) / cells + 0.5 / cells;
  fragColor = texture(uTexture, uv) * vColor;
}";

        private const string FragInvert = @"
#version 300 es
precision highp float;
in vec2 vTexCoord;
in vec4 vColor;
uniform sampler2D uTexture;
uniform float uTime;
uniform vec4 uParams;
out vec4 fragColor;
void main(){
  vec4 c = texture(uTexture, vTexCoord) * vColor;
  fragColor = vec4(1.0 - c.rgb, c.a);
}";

        private const string FragScanline = @"
#version 300 es
precision highp float;
in vec2 vTexCoord;
in vec4 vColor;
uniform sampler2D uTexture;
uniform float uTime;
uniform vec4 uParams; // x=扫描线数
out vec4 fragColor;
void main(){
  vec4 c = texture(uTexture, vTexCoord) * vColor;
  float scan = 0.85 + 0.15 * sin(vTexCoord.y * uParams.x + uTime * 6.0);
  fragColor = vec4(c.rgb * scan, c.a);
}";
    }
}
