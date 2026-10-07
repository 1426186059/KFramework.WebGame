using KFramework.MonoGame;
using System.Diagnostics;

namespace KFramework.Test.WebGL20.Tests
{
    /// <summary>
    /// 自定义 Effect 测试：用 SpriteBatch.Begin(effect: 自定义着色器) 把一段 GLSL 片元着色器套到精灵上。
    /// <para>
    /// 每个 Effect 都是 <see cref="ShaderEffect"/>：场景每帧写入 <see cref="ShaderEffect.Time"/> / <see cref="ShaderEffect.Params"/>，
    /// 绘制时由 WebGL 后端的自定义精灵程序灌入 uTime / uParams uniform。本测试固定走 WebGL 2.0，效果真实生效。
    /// </para>
    /// <para>空格键：网格总览 ⇄ 单图查看。共 8 个效果：高斯模糊 / 发光描边 / 水波 / Metaball / 阴影 / 马赛克 / 反相 / 扫描线。</para>
    /// </summary>
    public sealed class EffectScene : DemoScene
    {
        public override string Title => "自定义 Effect 测试（高斯模糊 / 描边发光 / 水波 / Metaball / 阴影 / 马赛克 / 反相 / 扫描线）";

        protected override string Description =>
            "用 SpriteBatch.Begin(effect: 自定义 GLSL 片元着色器) 套用效果（WebGL2 真实编译生效）。" +
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

        public override void Update()
        {
            if (Input_KeyBoard.GetKeyDown(Keys.Space))
            {
                _solo = !_solo;
                if (_effects.Count > 0)
                    _soloIndex = (_soloIndex + 1) % _effects.Count;
            }
        }

        private bool _drawFaultLogged;

        public override void Draw()
        {
            // 外层兜底：任何漏网的异常都就地吞掉并只记录一次，避免主循环因单个效果崩溃而整屏黑掉。
            try
            {
                DrawCore();
            }
            catch (Exception ex)
            {
                if (!_drawFaultLogged)
                {
                    _drawFaultLogged = true;
                    Console.Error.WriteLine($"[EffectScene] Draw 异常（已抑制，主循环继续）：{ex}");
                }
            }
        }

        private void DrawCore()
        {
            SpriteBatch batch = Batch;
            RenderOffscreen(batch);

            if (_tex == null) _tex = MakeChart(256);
            if (_effects.Count == 0) BuildEffects();
            float t = (float)_sw.Elapsed.TotalSeconds;

            // 页眉 / 页脚用默认材质（独立的 Begin/End，不与下方自定义 Effect 的 Begin 嵌套）
            batch.Begin();
            DrawHeader(batch);
            DrawFooter(batch);
            batch.End();

            if (_solo && _effects.Count > 0)
            {
                EffectEntry e = _effects[_soloIndex];
                float size = Math.Min(Device.Viewport.Width, Device.Viewport.Height) - 200f;
                float x = (Device.Viewport.Width - size) / 2f;
                float y = 84f + 16f;
                try
                {
                    e.Effect.Time = t;
                    e.Effect.Params = e.BaseParams;
                    batch.Begin(SpriteSortMode.Deferred, null, null, null, null, e.Effect);
                    batch.Draw(_tex, new Rectangle((int)x, (int)y, (int)size, (int)size), Color.White);
                    batch.End();

                    batch.Begin();
                    batch.DrawString(Font, $"{e.Name}（空格返回总览）", new Vector2(x, y - 28f), new Color(220, 235, 255));
                    batch.End();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[EffectScene] 效果「{e.Name}」绘制异常：{ex}");
                    try { batch.End(); } catch { }
                }
                return;
            }

            const float cell = 200f, gap = 24f;
            int cols = Math.Max(1, (int)((Device.Viewport.Width - 56f) / (cell + gap)));
            float x0 = 28f, y0 = 84f + 8f;

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
            try
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
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[EffectScene] 效果「{e.Name}」绘制异常：{ex}");
                // 失败时把批处理状态复位，避免 _beginCalled 卡死导致后续所有 Begin 连环报错。
                try { batch.End(); } catch { }
            }
        }

        private void BuildEffects()
        {
            int w = _tex?.Width ?? 256;
            int h = _tex?.Height ?? 256;
            var V4 = (float a, float b, float c, float d) => new System.Numerics.Vector4(a, b, c, d);

            void Add(string name, string frag, System.Numerics.Vector4 p)
            {
                try
                {
                    var eff = (ShaderEffect)Device.CreateShaderEffect(frag);
                    _effects.Add(new EffectEntry { Name = name, Effect = eff, BaseParams = p });
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[EffectScene] 效果「{name}」着色器编译失败：{ex.Message}");
                }
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

        /// <summary>程序化生成一张带软边、带彩色渐变的圆盘图（不依赖任何资源文件），便于演示各类效果。</summary>
        private Texture2D MakeChart(int size)
        {
            var tex = Device.CreateTexture(size, size);
            var data = new byte[size * size * 4];
            float cx = size / 2f, cy = size / 2f;
            float radius = size * 0.42f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    int idx = (y * size + x) * 4;

                    // 软边：外圈 15% 半径内 alpha 渐隐，方便发光描边 / 阴影检测边缘
                    float a = Math.Clamp(1f - (dist - radius * 0.85f) / (radius * 0.15f), 0f, 1f);
                    if (a <= 0f) { data[idx + 3] = 0; continue; }

                    float t = dist / radius; // 0 中心 → 1 边缘
                    data[idx] = (byte)(255 * (1f - t));
                    data[idx + 1] = (byte)(120f + 120f * (1f - t));
                    data[idx + 2] = (byte)(255 * t);
                    data[idx + 3] = (byte)(255 * a);
                }
            }

            tex.SetData(data);
            return tex;
        }

        // ============================================================
        // 自定义片元着色器（GLSL ES 3.00）
        // 约定：顶点复用标准精灵顶点（aPosition / aTexCoord / aColor + uProjection）；
        // 片元可用 vTexCoord / vColor / uTexture，外加 uTime（动画时间）与 uParams（vec4）。
        // ============================================================

        private const string FragBlur = @"#version 300 es
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

        private const string FragGlow = @"#version 300 es
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

        private const string FragWater = @"#version 300 es
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

        private const string FragMetaball = @"#version 300 es
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

        private const string FragShadow = @"#version 300 es
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

        private const string FragPixelate = @"#version 300 es
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

        private const string FragInvert = @"#version 300 es
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

        private const string FragScanline = @"#version 300 es
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
