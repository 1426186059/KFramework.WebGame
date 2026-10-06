using System;
using System.Diagnostics;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.WebGL20.Tests
{
    /// <summary>
    /// MaterialPropertyBlock 测试：<b>同一个材质 + 每个物体不同属性 + DrawCall 合并</b>。
    /// <para>
    /// 属性块挂在"这一次绘制"上（<see cref="SpriteBatch.Draw(Texture2D, Rectangle, Color, MaterialPropertyBlock?)"/>），
    /// 材质上用 <see cref="Material.SetSpriteChannels"/> 声明「块的哪些属性进顶点通道」：
    /// 被映射的属性值随顶点走 → 不参与分批键 → 整批合并成 1 次 DrawCall。
    /// 这就是 MaterialPropertyBlock 存在的意义（同材质 + 每物体不同 + 仍然合批）。
    /// </para>
    /// <para>
    /// 边界对照：第 8 格故意往块里放了 <c>uUvMatrix</c>（矩阵）—— 通道只有 8 个 8 位数值，装不下矩阵，
    /// 所以那一次绘制退回 uniform 覆盖路径：语义正确，但会切批（多出 1 次 DrawCall）。页面底部会显示实测值。
    /// </para>
    /// <para>
    /// 8 个格子逐个演示：第 1 格不打块（吃材质基线），第 2~7 格各改一类<b>可映射</b>属性
    /// （SetColor / SetFloat ×5），第 8 格改<b>不可映射</b>的矩阵。底部读数是块上的 GetXXX + 块状态。
    /// </para>
    /// <para>交互：空格暂停动画；鼠标悬停某格时给那一格再盖一条白色 tint —— 只影响这一格。</para>
    /// </summary>
    public sealed class MaterialPropertyBlockScene : DemoScene
    {
        public override string Title => "7) MaterialPropertyBlock（同材质 + 每物体不同 + DrawCall 合并）";

        protected override string Description =>
            "8 格共用同一材质：块里被 SetSpriteChannels 映射的属性编码进顶点 → 整批合并；只有矩阵这种通道装不下的退回 uniform。";

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
        private string _error = string.Empty;

        private float _time;
        private bool _paused;
        private long _tileDrawCalls = -1;

        /// <summary>格子标题：前 7 格改的都是「能进通道」的属性，最后一格改的是「进不了通道」的矩阵。</summary>
        private static readonly string[] TileTitles =
        [
            "不打 Block（基线）",
            "SetColor(uTint)",
            "SetFloat(uPulse)",
            "SetFloat(uHue)",
            "SetFloat(uMaskAmount)",
            "SetFloat(uFlip)",
            "SetFloat(uAngle)",
            "SetMatrix(uUvMatrix) ← 切批",
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

            _material = new Material { Effect = _fx };

            // 关键的一步：声明「块的哪些属性按顺序进 8 个顶点通道」——3+1+1+1+1+1 = 8 个槽位。
            // 声明之后，块里这些属性的值就随顶点走，不再需要 uniform，于是整批能合并成一次 DrawCall。
            _material.SetSpriteChannels("uTint.rgb", "uPulse", "uHue", "uFlip", "uMaskAmount", "uAngle");

            // 材质基线：块里没设的属性由这里兜底（通道编码取不到块里的值就用基线）。
            _material.SetColor("uTint", Color.White);
            _material.SetFloat("uPulse", 1f);
            _material.SetFloat("uHue", 0f);
            _material.SetFloat("uFlip", 0f);
            _material.SetFloat("uMaskAmount", 0f);
            _material.SetFloat("uAngle", 0f);

            // 通道装不下的属性：只能走 uniform（矩阵、纹理、枚举都算这类）。
            _material.SetMatrix("uUvMatrix", Matrix4x4.Identity);
            _material.SetInt("uOrientation", 0);
            _material.SetTexture("uMask", _mask);

            // 重复使用的那个块：每次绘制前 Clear → SetXXX。
            _block = new MaterialPropertyBlock();
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

            // ---- 块挂在 Draw 上，整批只 Begin/End 一次 ----
            // 块里被映射的属性进顶点（合批）；含未映射属性（第 8 格的矩阵）的那一次才切批。
            long before = Device.Metrics.DrawCount;
            batch.Begin(_material);
            for (int i = 0; i < TileCount; i++)
            {
                bool useBlock = ConfigureBlock(i, t, _tiles[i].Contains(mouse));
                _values[i] = DescribeBlock(i);
                batch.Draw(_chart!, _tiles[i], Color.White, useBlock ? _block : null);
            }
            batch.End();
            _tileDrawCalls = Device.Metrics.DrawCount - before;

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

        /// <summary>按格子把「这一绘制要盖的属性」写进共享块；返回 false 表示这一格不打块（吃材质基线）。</summary>
        private bool ConfigureBlock(int index, float t, bool hover)
        {
            _block!.Clear();

            if (index == 0) return false;   // 不打块：通道值全部来自材质基线

            switch (index)
            {
                case 1:
                    _block.SetColor("uTint", new Color(255, 140, 140));
                    break;
                case 2:
                    _block.SetFloat("uPulse", 0.45f);
                    break;
                case 3:
                    _block.SetFloat("uHue", 0.33f);
                    break;
                case 4:
                    _block.SetFloat("uMaskAmount", 1f);   // 用材质基线上绑的遮罩纹理，逐物体控制强度
                    break;
                case 5:
                    _block.SetFloat("uFlip", 1f);
                    break;
                case 6:
                    _block.SetFloat("uAngle", 0.125f);    // 0~1 通道值 → 着色器里 ×2π ≈ 45°
                    break;
                default:
                    // 第 8 格：矩阵进不了通道 → 这一次绘制只能挂 uniform，于是切批。
                    _block.SetColor("uTint", new Color(255, 205, 120));
                    _block.SetMatrix("uUvMatrix", RotationAboutCenter(t * 0.8f));
                    _block.SetInt("uOrientation", 1);
                    break;
            }

            // 悬停：再往这个块上盖一条 tint —— 因为块只作用于本次 Draw，所以只影响这一格。
            if (hover) _block.SetColor("uTint", Color.White);

            return true;
        }

        /// <summary>直接从块上读回值（证明值确实存在块里，而材质本身没变）。</summary>
        private string DescribeBlock(int index)
        {
            MaterialPropertyBlock b = _block!;
            switch (index)
            {
                case 0:
                    Color baseTint = _material!.GetColor("uTint");
                    return $"材质基线 uTint=#{baseTint.R:X2}{baseTint.G:X2}{baseTint.B:X2}";
                case 1:
                {
                    Color c = b.GetColor("uTint");
                    return $"Get uTint = #{c.R:X2}{c.G:X2}{c.B:X2}";
                }
                case 2:
                    return $"Get uPulse = {b.GetFloat("uPulse"):F2}";
                case 3:
                    return $"Get uHue = {b.GetFloat("uHue"):F2}";
                case 4:
                    return $"Get uMaskAmount = {b.GetFloat("uMaskAmount"):F2}";
                case 5:
                    return $"Get uFlip = {b.GetFloat("uFlip"):F2}";
                case 6:
                    return $"Get uAngle = {b.GetFloat("uAngle"):F3}";
                default:
                    return $"uUvMatrix = 旋转 / uOrientation = {b.GetInt("uOrientation")}";
            }
        }

        /// <summary>底部说明与实测 DrawCall：这是本页最有说服力的两行数字。</summary>
        private void DrawReadBack(SpriteBatch batch)
        {
            MaterialPropertyBlock b = _block!;
            float y = Device.Viewport.Height - 116f;

            string line0 = $"8 格共用 1 个材质 + 1 次 Begin/End → DrawCall 增量 = {_tileDrawCalls}" +
                           "（前 7 格：块属性全部映射进顶点 → 合并成 1 次；第 8 格含 uUvMatrix → 另 1 次）";

            string line1 = "材质上声明的通道：uTint.rgb | uPulse | uHue | uFlip | uMaskAmount | uAngle（共 8 个，" +
                           "顶点里 aParams0/aParams1）";

            string line2 = $"块状态：IsEmpty={b.IsEmpty}  属性条数={b.Properties.Count}  块版本={b.PropertiesVersion}  " +
                           $"材质属性条数={_material!.Properties.Count}";

            batch.DrawString(_small!, line0, new Vector2(28f, y), new Color(255, 206, 110));
            batch.DrawString(_small!, line1, new Vector2(28f, y + 22f), new Color(120, 200, 160));
            batch.DrawString(_small!, line2, new Vector2(28f, y + 44f), new Color(120, 200, 160));
        }

        /// <summary>把 8 个格子排成 4 列 2 行。</summary>
        private void LayoutTiles()
        {
            const int cols = 4;
            float x0 = 28f;
            float y0 = 128f;

            for (int i = 0; i < TileCount; i++)
            {
                int c = i % cols;
                int r = i / cols;
                _tiles[i] = new Rectangle(
                    (int)(x0 + c * (TileSize + TileGap)),
                    (int)(y0 + r * RowPitch),
                    (int)TileSize,
                    (int)TileSize);
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

        /// <summary>
        /// 本页片元着色器：逐物体差异全部来自顶点通道
        /// （vParams0 = 色调.rgb + 亮度乘子.a，vParams1 = 色相 / 翻转 / 遮罩量 / UV 旋转），
        /// 通道装不下的（矩阵、朝向、纹理）仍从 uniform 取。
        /// </summary>
        private const string FragmentSource = @"#version 300 es
precision highp float;
in vec2 vTexCoord;
in vec4 vColor;
in vec4 vParams0;   // 通道 0~3：rgb = 色调, a = 亮度乘子
in vec4 vParams1;   // 通道 4~7：x = 色相, y = 翻转(0/0.5/1), z = 遮罩量, w = UV 旋转(0~1 → 0~2π)
uniform sampler2D uTexture;
uniform sampler2D uMask;      // 材质基线（批级）
uniform mat4  uUvMatrix;      // uniform 路径：通道装不下的矩阵
uniform int   uOrientation;   // uniform 路径：通道装不下的枚举
out vec4 fragColor;

vec3 hueShift(vec3 c, float h){
  const vec3 k = vec3(0.57735);
  float a = h * 6.2831853;
  return c * cos(a) + cross(k, c) * sin(a) + k * dot(k, c) * (1.0 - cos(a));
}

void main()
{
    vec2 uv = vTexCoord;

    // ---- 顶点通道来的逐物体差异（这部分能合批）----
    float flip = vParams1.y;
    if (flip > 0.7) uv.x = 1.0 - uv.x;
    else if (flip > 0.2) uv.y = 1.0 - uv.y;

    float angle = vParams1.w * 6.2831853;
    if (angle != 0.0)
    {
        float cs = cos(angle), sn = sin(angle);
        vec2 p = uv - vec2(0.5);
        uv = vec2(p.x * cs - p.y * sn, p.x * sn + p.y * cs) + vec2(0.5);
    }

    // ---- uniform 路径来的（通道装不下的属性）----
    uv = (uUvMatrix * vec4(uv, 0.0, 1.0)).xy;
    if (uOrientation == 1) uv.y = 1.0 - uv.y;
    else if (uOrientation == 2) uv.x = 1.0 - uv.x;

    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0) { fragColor = vec4(0.0); return; }

    vec4 c = texture(uTexture, uv) * vColor;
    c.rgb = hueShift(c.rgb, vParams1.x);
    c.rgb *= vParams0.rgb * vParams0.a;

    float m = mix(1.0, texture(uMask, vTexCoord).r, vParams1.z);
    fragColor = vec4(c.rgb * m, c.a);
}";
    }
}
