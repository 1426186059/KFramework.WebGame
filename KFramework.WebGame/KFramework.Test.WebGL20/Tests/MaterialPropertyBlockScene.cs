using System;
using System.Diagnostics;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.WebGL20.Tests
{
    /// <summary>
    /// MaterialPropertyBlock 测试：<b>一个共享材质 + 一个重复使用的属性块</b> 画出 8 种不同外观。
    /// <para>
    /// 对应 Unity 的做法：不要为了改几个 uniform 就给每个实例 new 一个 Material，
    /// 而是共用 Material（着色器 + 渲染状态 + 属性基线），每次绘制前把要盖的属性写进一个
    /// <see cref="MaterialPropertyBlock"/>（<c>Clear()</c> → <c>SetXXX</c>），再
    /// <c>batch.Begin(material, block)</c> —— 绘制时先下发材质属性、再下发 Block 的覆盖值。
    /// </para>
    /// <para>
    /// 8 个格子逐个演示：第 1 格不打 Block（看材质基线），其余每格只改 Block 的一类属性
    /// （SetColor / SetFloat / SetVector / SetMatrix / SetInt / SetTexture），最后一格演示
    /// <see cref="MaterialPropertyBlock.CopyFrom"/>（从模板块整体拷贝后再追加覆盖）。
    /// 底部读数是块上的 GetXXX（值确实存在块上）+ 块的状态（IsEmpty / 属性条数 / 版本号）。
    /// </para>
    /// <para>交互：空格暂停动画；鼠标悬停某格时给那一格再盖一条白色 tint —— 只影响这一格（证明覆盖是逐次绘制的）。</para>
    /// </summary>
    public sealed class MaterialPropertyBlockScene : DemoScene
    {
        public override string Title => "MaterialPropertyBlock 测试（每次绘制覆盖材质属性）";

        protected override string Description =>
            "8 个格子共用同一个 Material，只用一个重复使用的 MaterialPropertyBlock 逐格 Clear → SetXXX 覆盖。";

        private const int TileCount = 8;
        private const float TileSize = 165f;
        private const float TileGap = 16f;
        private const float RowPitch = TileSize + TileGap + 50f;

        private readonly Rectangle[] _tiles = new Rectangle[TileCount];
        private readonly string[] _values = new string[TileCount];
        private readonly Stopwatch _sw = Stopwatch.StartNew();

        private SpriteFont? _small;
        private Texture2D? _chart;
        private Texture2D? _mask;
        private ShaderEffect? _fx;
        private Material? _material;
        private MaterialPropertyBlock? _block;
        private MaterialPropertyBlock? _template;
        private string _error = string.Empty;

        private float _time;
        private bool _paused;

        /// <summary>格子标题（第 1 格是材质基线，其余是块上改的那一类属性）。</summary>
        private static readonly string[] TileTitles =
        [
            "不打 Block（材质基线）",
            "block.SetColor",
            "block.SetFloat",
            "block.SetVector",
            "block.SetMatrix",
            "block.SetInt",
            "block.SetTexture",
            "CopyFrom + SetMatrix",
        ];

        public override void LoadContent()
        {
            _chart = MakeChart(256);
            _mask = MakeChecker(256, Color.White, new Color(115, 125, 150));
            _small = new SpriteFont(Device, 13f);

            try
            {
                _fx = (ShaderEffect)Device.CreateShaderEffect(FragmentSource);
            }
            catch (Exception ex)
            {
                _error = ex.Message;
                Console.Error.WriteLine($"[MaterialPropertyBlockScene] 着色器创建失败：{ex.Message}");
                return;
            }

            // 共享材质：着色器 + 渲染状态 + 属性基线（基线只设一次，之后靠 Block 覆盖）。
            _material = new Material { Effect = _fx };
            _material.SetFloat("uTime", 0f);
            _material.SetColor("uTint", Color.White);
            _material.SetFloat("uPulse", 1f);
            _material.SetInt("uOrientation", 0);
            _material.SetMatrix("uUvMatrix", Matrix4x4.Identity);
            _material.SetTexture("uMask", null);
            _material.SetVector("uParams", new System.Numerics.Vector4(_chart.Width, _chart.Height, 0f, 0f));

            // 重复使用的那个块（8 格共用）与一个模板块（演示 CopyFrom）。
            _block = new MaterialPropertyBlock();
            _template = new MaterialPropertyBlock();
            _template.SetColor("uTint", new Color(255, 205, 120));
            _template.SetFloat("uPulse", 0.9f);
        }

        public override void Update()
        {
            if (Input_KeyBoard.GetKeyDown(Keys.Space)) _paused = !_paused;
            if (!_paused) _time = (float)_sw.Elapsed.TotalSeconds;
        }

        public override void Draw()
        {
            SpriteBatch batch = Batch;
            float t = _time;

            batch.Begin();
            DrawHeader(batch);
            DrawFooter(batch);
            batch.End();

            if (_material is null || _block is null || FixturesMissing(batch))
                return;

            LayoutTiles();
            Vector2 mouse = Input_Mouse.Position;

            for (int i = 0; i < TileCount; i++)
            {
                bool useBlock = ConfigureBlock(i, t, _tiles[i].Contains(mouse));
                _values[i] = DescribeBlock(i);

                // 同一个材质实例 + 同一个块实例：只有块里的值在变（第 1 格不打块，走材质基线）。
                if (useBlock) batch.Begin(_material, _block);
                else batch.Begin(_material);
                batch.Draw(_chart, _tiles[i], Color.White);
                batch.End();
            }

            batch.Begin();
            for (int i = 0; i < TileCount; i++)
            {
                Rectangle tile = _tiles[i];
                float textY = tile.Y + tile.Height + 4f;
                batch.DrawString(_small!, TileTitles[i], new Vector2(tile.X, textY), new Color(200, 220, 255));
                batch.DrawString(_small!, _values[i], new Vector2(tile.X, textY + 18f), new Color(140, 158, 190));
            }
            DrawReadBack(batch);
            batch.End();
        }

        /// <summary>按格子把「这一绘制要盖的属性」写进共享块；返回 false 表示这一格故意不打块（看材质基线）。</summary>
        private bool ConfigureBlock(int index, float t, bool hover)
        {
            // 复用同一个块：先清空，再设这一格要盖的（照 Unity 的高效写法）。
            _block!.Clear();

            if (index == 0) return false;   // 不打块：直接看材质基线（白 tint / pulse 1 / 无旋转）

            int w = _chart?.Width ?? 256;
            int h = _chart?.Height ?? 256;

            switch (index)
            {
                case 1:
                    _block.SetColor("uTint", new Color(255, 140, 140));
                    break;

                case 2:
                    _block.SetFloat("uPulse", 0.45f);
                    break;

                case 3:
                    _block.SetVector("uParams", new System.Numerics.Vector4(w, h, 0.33f, 0f));
                    break;

                case 4:
                    _block.SetMatrix("uUvMatrix", RotationAboutCenter(MathF.PI * 0.25f));
                    break;

                case 5:
                    _block.SetInt("uOrientation", 1);
                    break;

                case 6:
                    _block.SetTexture("uMask", _mask);
                    _block.SetVector("uParams", new System.Numerics.Vector4(w, h, 0f, 1f));
                    break;

                default:
                    // CopyFrom：整体拷贝模板块的属性（tint / pulse），再追加一条旋转矩阵。
                    _block.CopyFrom(_template!);
                    _block.SetMatrix("uUvMatrix", RotationAboutCenter(t * 0.8f));
                    break;
            }

            // 悬停：再往这个块上盖一条 tint —— 因为块一清一设、只作用于本次 Begin/End，所以只影响这一格。
            if (hover) _block.SetColor("uTint", Color.White);

            return true;
        }

        /// <summary>直接从块上读回值（证明这些值确实存在块里，而材质本身没变）。</summary>
        private string DescribeBlock(int index)
        {
            MaterialPropertyBlock b = _block!;
            switch (index)
            {
                case 0:
                    Color baseTint = _material!.GetColor("uTint");
                    return $"材质: uTint=#{baseTint.R:X2}{baseTint.G:X2}{baseTint.B:X2}";
                case 1:
                {
                    Color c = b.GetColor("uTint");
                    return $"Get uTint = #{c.R:X2}{c.G:X2}{c.B:X2}";
                }
                case 2:
                    return $"Get uPulse = {b.GetFloat("uPulse"):F2}";
                case 3:
                    return $"Get uParams.z = {b.GetVector("uParams").Z:F2}";
                case 4:
                    return $"Get uUvMatrix = 旋转";
                case 5:
                    return $"Get uOrientation = {b.GetInt("uOrientation")}";
                case 6:
                    return $"Get uMask = {(b.GetTexture("uMask") is null ? "null" : "棋盘")}";
                default:
                {
                    Color c = b.GetColor("uTint");
                    return $"CopyFrom → #{c.R:X2}{c.G:X2}{c.B:X2} + 旋转";
                }
            }
        }

        /// <summary>底部说明与块状态：强调「一个材质 + 一个块」与块的复用状态。</summary>
        private void DrawReadBack(SpriteBatch batch)
        {
            MaterialPropertyBlock b = _block!;
            float y = Device.Viewport.Height - 92f;

            string line1 = "1 个 Material（属性基线：uTint=白 / uPulse=1 / 无旋转）+ 1 个重复使用的 MaterialPropertyBlock → 8 种外观；" +
                           "Block 每次绘制都是先 Clear() 再 SetXXX，不 new 材质";

            string line2 = $"块状态：IsEmpty={b.IsEmpty}  属性条数={b.Properties.Count}  块版本={b.PropertiesVersion}  " +
                           $"材质属性条数={_material!.Properties.Count}  模板块（CopyFrom 用）属性条数={_template!.Properties.Count}";

            batch.DrawString(_small!, line1, new Vector2(28f, y), new Color(120, 200, 160));
            batch.DrawString(_small!, line2, new Vector2(28f, y + 22f), new Color(120, 200, 160));
        }

        /// <summary>把 8 个格子排成 4 列 2 行。</summary>
        private void LayoutTiles()
        {
            const int cols = 4;
            float x0 = 28f;
            float y0 = 92f;

            for (int i = 0; i < TileCount; i++)
            {
                int c = i % cols;
                int r = i / cols;
                float x = x0 + c * (TileSize + TileGap);
                float y = y0 + r * RowPitch;
                _tiles[i] = new Rectangle((int)x, (int)y, (int)TileSize, (int)TileSize);
            }
        }

        /// <summary>着色器不可用时就地提示（避免整页空白看不出原因）。</summary>
        private bool FixturesMissing(SpriteBatch batch)
        {
            if (_fx is not null && _block is not null && _small is not null) return false;
            batch.Begin();
            DrawLine(batch, $"着色器不可用：{_error}", 28f, 96f, new Color(255, 150, 150));
            batch.End();
            return true;
        }

        /// <summary>程序化生成一张带软边、带角落标记的彩色圆盘（标记用于看清旋转 / 翻转）。</summary>
        private Texture2D MakeChart(int size)
        {
            Texture2D tex = Device.CreateTexture(size, size);
            byte[] data = new byte[size * size * 4];
            float cx = size / 2f, cy = size / 2f;
            float radius = size * 0.42f;
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

        /// <summary>演示用的片元着色器：uTint / uPulse / uOrientation / uUvMatrix / uMask 都会被 MaterialPropertyBlock 覆盖。</summary>
        private const string FragmentSource = @"#version 300 es
precision highp float;
in vec2 vTexCoord;
in vec4 vColor;

uniform sampler2D uTexture;   // 精灵纹理（0 号单元）
uniform sampler2D uMask;      // 材质属性里的纹理（1 号单元）
uniform float uTime;          // 内置：动画时间
uniform vec4  uParams;        // 内置：x=W y=H z=色相偏移 w=是否启用遮罩
uniform vec4  uTint;          // 材质基线 + Block 覆盖
uniform float uPulse;         // 材质基线 + Block 覆盖
uniform int   uOrientation;   // 材质基线 + Block 覆盖
uniform mat4  uUvMatrix;      // 材质基线 + Block 覆盖

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
