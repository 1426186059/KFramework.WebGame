using KFramework.MonoGame;
using System.Diagnostics;

namespace KFramework.Test.WebGL20.Tests
{
    /// <summary>
    /// 效果属性测试：6 个格子各自一个 <see cref="ShaderEffect"/>（同一段片元着色器源码）+ 一个 <see cref="Material"/>
    /// （材质只带渲染状态），每格只改一类效果属性，直接对比「<c>ShaderEffect.SetXXX</c> 到底改了什么」。
    /// <para>
    /// 演示的 Unity 风格接口（全部映射到同一段着色器里的 uniform）：
    /// <see cref="ShaderProperties.SetFloat"/> → uPulse（亮度脉动）、
    /// <see cref="ShaderProperties.SetColor"/> → uTint（颜色）、
    /// <see cref="ShaderProperties.SetInt"/> → uOrientation（采样朝向）、
    /// <see cref="ShaderProperties.SetMatrix"/> → uUvMatrix（UV 旋转）、
    /// <see cref="ShaderProperties.SetTexture"/> → uMask（棋盘遮罩，绑 1 号纹理单元）、
    /// <see cref="ShaderProperties.SetVector"/> → uParams（色相偏移等）。
    /// </para>
    /// <para>
    /// 每格都把自己那份属性设全（没演示到的设成中性值）—— 属性是"程序级状态"，漏设就会串到上一格留下的值。
    /// 底部那行是 <c>GetFloat / GetColor / GetInt / GetVector</c> 的读数，用来验证写入确实落到了效果上。
    /// </para>
    /// <para>交互：空格暂停 / 继续动画；鼠标点格子切换采样朝向（SetInt）；鼠标悬停换色（SetColor）。</para>
    /// </summary>
    public sealed class MaterialScene : DemoScene
    {
        public override string Title => "效果属性测试（ShaderEffect.SetXXX）";

        protected override string Description =>
            "6 个格子各自一个效果实例（同一段片元着色器源码）+ 一个材质（只带状态），各自只改一类效果属性；空格暂停动画，点格子切采样朝向，悬停换色。";

        private const int TileCount = 6;
        private const float TileSize = 185f;
        private const float TileGap = 18f;

        /// <summary>一行格子占的高度：格子 + 间距 + 下方两行文字。</summary>
        private const float TileRowPitch = TileSize + TileGap + 62f;

        private readonly Stopwatch _sw = Stopwatch.StartNew();

        /// <summary>每格一个效果实例（同一段片元着色器源码）：属性值挂在效果上（材质只描述"怎么画"）。</summary>
        private readonly ShaderEffect[] _effects = new ShaderEffect[TileCount];

        /// <summary>每格一个材质：只带渲染状态 + 引用该格的效果。</summary>
        private readonly Material[] _materials = new Material[TileCount];

        private readonly Rectangle[] _tiles = new Rectangle[TileCount];
        private readonly int[] _orientations = new int[TileCount];

        private Texture2D? _chart;
        private Texture2D? _mask;
        private string? _shaderError;

        private float _time;
        private bool _paused;

        /// <summary>格子标题：就是着色器里的 uniform 名（短的，免得相邻格子文字互相压）。</summary>
        private static readonly string[] TileNames =
        [
            "uPulse",
            "uTint",
            "uOrientation",
            "uUvMatrix",
            "uMask",
            "uParams",
        ];

        public override void LoadContent()
        {
            _chart = MakeChart(256);
            _mask = MakeChecker(256, Color.White, new Color(115, 125, 150));

            try
            {
                for (int i = 0; i < TileCount; i++)
                {
                    _effects[i] = Device.CreateShaderEffect(FragmentSource);
                    _materials[i] = new Material { Effect = _effects[i] };
                }
            }
            catch (Exception ex)
            {
                _shaderError = ex.Message;
                Console.Error.WriteLine($"[MaterialScene] 着色器创建失败：{ex.Message}");
            }
        }

        public override void Update()
        {
            if (Input_KeyBoard.GetKeyDown(Keys.Space)) _paused = !_paused;
            if (!_paused) _time = (float)_sw.Elapsed.TotalSeconds;

            for (int i = 0; i < TileCount; i++)
            {
                if (_tiles[i].Width > 0 && _tiles[i].Contains(Input_Mouse.Position)
                    && Input_Mouse.GetButtonDown(MouseButton.Left))
                {
                    _orientations[i] = (_orientations[i] + 1) % 3;
                }
            }
        }

        public override void Draw()
        {
            SpriteBatch batch = Batch;
            float t = _time;

            batch.Begin();
            DrawHeader(batch);
            DrawFooter(batch);
            batch.End();

            if (_shaderError is not null)
            {
                batch.Begin();
                DrawLine(batch, $"着色器不可用：{_shaderError}", 28f, 96f, new Color(255, 150, 150));
                batch.End();
                return;
            }

            LayoutTiles();

            for (int i = 0; i < TileCount; i++)
            {
                Material material = _materials[i];
                ConfigureMaterial(i, _effects[i], t, _tiles[i].Contains(Input_Mouse.Position));

                // 一个格子一个材质（各自引用自己的效果）= 一个独立的 Begin/End（同一段着色器源码，不同属性值）。
                batch.Begin(material);
                batch.Draw(_chart, _tiles[i], null, Color.White);
                batch.End();
            }

            // 标签与读数统一一批画完，避免每格都切状态。
            batch.Begin();
            for (int i = 0; i < TileCount; i++)
            {
                Rectangle tile = _tiles[i];
                float textY = tile.Y + tile.Height + 4f;
                DrawLine(batch, TileNames[i], tile.X, textY, new Color(200, 220, 255));
                DrawLine(batch, TileValueText(i), tile.X, textY + Font.LineSpacing + 6f, new Color(140, 158, 190));
            }
            DrawReadBack(batch);
            batch.End();
        }

        /// <summary>按格子的演示重点配置这一格的效果属性：先全部设成中性值，再把这一格要演示的那一项改成"有差异"的值。</summary>
        private void ConfigureMaterial(int index, ShaderEffect effect, float t, bool hover)
        {
            int w = _chart?.Width ?? 256;
            int h = _chart?.Height ?? 256;

            // 中性默认值（每格都设全：属性是效果级状态，漏设会串到这一格上一次画的值）。
            effect.SetVector("uParams", new System.Numerics.Vector4(w, h, 0f, 0f));
            effect.SetColor("uTint", Color.White);
            effect.SetFloat("uPulse", 1f);
            effect.SetInt("uOrientation", _orientations[index]);
            effect.SetMatrix("uUvMatrix", Matrix4x4.Identity);
            effect.SetTexture("uMask", null);
            effect.SetFloat("uTime", t);

            switch (index)
            {
                case 0: // SetFloat：亮度脉动
                    effect.SetFloat("uPulse", 0.65f + 0.35f * MathF.Sin(t * 2.2f) + 0.35f);
                    break;

                case 1: // SetColor：颜色循环
                    effect.SetColor("uTint", hover ? new Color(255, 255, 255) : CycleTint(t));
                    break;

                case 2: // SetInt：采样朝向（点一下换一档）
                    break;

                case 3: // SetMatrix：UV 绕中心缓慢旋转
                    effect.SetMatrix("uUvMatrix", RotationAboutCenter(t * 0.6f));
                    break;

                case 4: // SetTexture：棋盘遮罩（uParams.w = 1 启用；材质纹理走 1 号单元）
                    effect.SetTexture("uMask", _mask);
                    effect.SetVector("uParams", new System.Numerics.Vector4(w, h, 0f, 1f));
                    break;

                case 5: // SetVector：色相偏移（随时间循环）
                    effect.SetVector("uParams", new System.Numerics.Vector4(w, h, (t * 0.15f) % 1f, 0f));
                    break;
            }
        }

        /// <summary>每格的当前值文本（用 SetXXX 设的、再用 GetXXX 读回来，顺便验证属性是可读可写的）。</summary>
        private string TileValueText(int index)
        {
            ShaderEffect e = _effects[index];
            switch (index)
            {
                case 0: return $"SetFloat = {e.GetFloat("uPulse"):F2}";
                case 1:
                    Color c = e.GetColor("uTint");
                    return $"SetColor = #{c.R:X2}{c.G:X2}{c.B:X2}";
                case 2: return $"SetInt = {e.GetInt("uOrientation")}";
                case 3: return $"SetMatrix = 旋转";
                case 4: return $"SetTexture = {(e.GetTexture("uMask") is null ? "null" : "棋盘")}";
                default:
                    System.Numerics.Vector4 p = e.GetVector("uParams");
                    return $"SetVector = {p.Z:F2}";
            }
        }

        /// <summary>底部读数：直接验证写入的值确实存在效果上（GetXXX / HasProperty）。</summary>
        private void DrawReadBack(SpriteBatch batch)
        {
            ShaderEffect e = _effects[0];
            Color tint = e.GetColor("uTint");
            System.Numerics.Vector4 p = e.GetVector("uParams");

            float y = Device.Viewport.Height - 100f;
            string line1 = $"GetXXX 读数（效果 1）：HasProperty(\"uPulse\")={e.HasProperty("uPulse")}  " +
                           $"GetFloat(\"uPulse\")={e.GetFloat("uPulse"):F2}  " +
                           $"GetColor(\"uTint\")=#{tint.R:X2}{tint.G:X2}{tint.B:X2}  " +
                           $"GetInt(\"uOrientation\")={e.GetInt("uOrientation")}";
            string line2 = $"GetVector(\"uParams\")=({p.X:F0},{p.Y:F0},{p.Z:F2},{p.W:F0})  " +
                           $"GetTexture(\"uMask\")={(e.GetTexture("uMask") is null ? "null" : "棋盘")}  " +
                           $"属性条数={e.Properties.Count}  属性版本={e.PropertiesVersion}";

            batch.DrawString(Font, line1, new Vector2(28f, y), new Color(120, 200, 160));
            batch.DrawString(Font, line2, new Vector2(28f, y + Font.LineSpacing + 6f), new Color(120, 200, 160));
        }

        /// <summary>按视口把 6 个格子排成 3 列 2 行。</summary>
        private void LayoutTiles()
        {
            const int cols = 3;
            float x0 = 28f;
            float y0 = 92f;

            for (int i = 0; i < TileCount; i++)
            {
                int c = i % cols;
                int r = i / cols;
                float x = x0 + c * (TileSize + TileGap);
                float y = y0 + r * TileRowPitch;
                _tiles[i] = new Rectangle((int)x, (int)y, (int)TileSize, (int)TileSize);
            }
        }

        /// <summary>在几个预设色之间循环插值（用于 SetColor 演示）。</summary>
        private static Color CycleTint(float t)
        {
            Color[] palette =
            [
                new Color(255, 150, 150),
                new Color(150, 255, 180),
                new Color(150, 190, 255),
                new Color(255, 225, 150),
            ];

            float f = t * 0.5f;
            int i0 = (int)(f % palette.Length);
            int i1 = (i0 + 1) % palette.Length;
            float k = f - MathF.Floor(f);
            Color a = palette[i0];
            Color b = palette[i1];

            return new Color((byte)(a.R + (b.R - a.R) * k),
                             (byte)(a.G + (b.G - a.G) * k),
                             (byte)(a.B + (b.B - a.B) * k),
                             (byte)255);
        }

        /// <summary>程序化生成一张带软边的彩色圆盘（不依赖资源文件），作为材质的主图。</summary>
        private Texture2D MakeChart(int size)
        {
            Texture2D tex = Device.CreateTexture(size, size);
            byte[] data = new byte[size * size * 4];
            float cx = size / 2f, cy = size / 2f;
            float radius = size * 0.42f;

            // 左上偏上放一个亮色标记：圆盘是中心对称的，没有标记的话 SetInt（翻转）/ SetMatrix（旋转）肉眼看不出差异。
            float markX = size * 0.32f, markY = size * 0.26f, markR = size * 0.075f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    int i = (y * size + x) * 4;

                    float a = Math.Clamp(1f - (dist - radius * 0.85f) / (radius * 0.15f), 0f, 1f);
                    if (a <= 0f) continue;

                    float k = dist / radius;
                    data[i] = (byte)(255 * (1f - k));
                    data[i + 1] = (byte)(120f + 120f * (1f - k));
                    data[i + 2] = (byte)(255 * k);
                    data[i + 3] = (byte)(255 * a);

                    float mdx = x - markX, mdy = y - markY;
                    if (mdx * mdx + mdy * mdy <= markR * markR)
                    {
                        data[i] = 255;
                        data[i + 1] = 245;
                        data[i + 2] = 120;
                        data[i + 3] = 255;
                    }
                }
            }

            tex.SetData(data);
            return tex;
        }

        /// <summary>
        /// 演示用的片元着色器：把能设的每一类属性都用上一遍。
        /// uTexture 是精灵纹理，其余 uniform 全部由 <c>effect.SetXXX("名字", …)</c> 写进来
        /// （uTime / uParams 只是约定名，引擎不做特殊处理）。
        /// </summary>
        private const string FragmentSource = @"#version 300 es
precision highp float;
in vec2 vTexCoord;
in vec4 vColor;

uniform sampler2D uTexture;   // 精灵纹理（0 号单元，SpriteBatch 绑）
uniform sampler2D uMask;      // 第二张纹理（1 号单元，effect.SetTexture 绑）
uniform float uTime;          // effect.SetFloat
uniform vec4  uParams;        // effect.SetVector：x=W y=H z=色相偏移 w=是否启用遮罩
uniform vec4  uTint;          // effect.SetColor
uniform float uPulse;         // effect.SetFloat
uniform int   uOrientation;   // effect.SetInt
uniform mat4  uUvMatrix;      // effect.SetMatrix

out vec4 fragColor;

vec3 hueShift(vec3 c, float h){
  const vec3 k = vec3(0.57735);
  float a = h * 6.2831853;
  return c * cos(a) + cross(k, c) * sin(a) + k * dot(k, c) * (1.0 - cos(a));
}

void main(){
  vec2 uv = (uUvMatrix * vec4(vTexCoord, 0.0, 1.0)).xy;
  if (uOrientation == 1) uv.y = 1.0 - uv.y;
  else if (uOrientation == 2) uv.x = 1.0 - uv.x;
  if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0) { fragColor = vec4(0.0); return; }

  vec4 c = texture(uTexture, uv) * vColor;
  c.rgb = hueShift(c.rgb, uParams.z);
  c.rgb *= uTint.rgb * uTint.a;
  c.rgb *= uPulse;

  float m = mix(1.0, texture(uMask, vTexCoord).r, uParams.w);
  fragColor = vec4(c.rgb * m, c.a);
}";
    }
}
