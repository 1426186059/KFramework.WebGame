using System;
using System.Diagnostics;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.WebGL20.Tests
{
    /// <summary>
    /// 逐精灵参数（顶点通道）测试：<b>一个共享材质 + 一次 Begin/End</b> 画出 8 种不同外观，整批仍然只有 1 次 DrawCall。
    /// <para>
    /// 与第 7 页（MaterialPropertyBlock + <see cref="Material.SetSpriteChannels"/>）是<b>同一机制的两个入口</b>：
    /// 那边按<b>属性名</b>声明映射（块里写 SetColor / SetFloat …），这里直接给<b>数值</b>（<see cref="SpriteParams"/>）。
    /// 两者都是把逐物体的值放进顶点通道，所以<b>不参与分批键</b>（分批键只有纹理，以及"含未映射属性的 uniform 块"）。
    /// </para>
    /// <para>
    /// 参数语义由本页的片元着色器自定义（引擎不解释）：
    /// <c>vParams0.rgb</c> = 逐精灵色调、<c>vParams0.a</c> = 与材质基线的混合权重（1 = 完全覆盖材质）、<c>vParams1.x</c> = 脉冲相位。
    /// </para>
    /// </summary>
    public sealed class SpriteParamScene : DemoScene
    {
        public override string Title => "8) 逐精灵参数（顶点通道）：同材质、逐精灵不同、仍然合批";

        protected override string Description
            => "8 个格子共用 1 个材质、只 Begin/End 一次：参数随顶点走，不参与分批键 → 整批 1 次 DrawCall（对照第 7 页的 8 次）";

        private const int TileCount = 8;
        private const float TileSize = 165f;
        private const float TileGap = 16f;
        private const float RowPitch = TileSize + TileGap + 56f;

        /// <summary>8 格的逐精灵色调（写进顶点参数的 P0~P3）。</summary>
        private static readonly Color[] TileColors =
        [
            new(255, 255, 255),
            new(255, 138, 138),
            new(138, 255, 170),
            new(150, 190, 255),
            new(255, 220, 120),
            new(230, 150, 255),
            new(140, 240, 240),
            new(255, 170, 90),
        ];

        private static readonly string[] TileTitles =
        [
            "基本色（覆盖材质基线）",
            "逐精灵色调 1",
            "逐精灵色调 2",
            "逐精灵色调 3",
            "逐精灵色调 4",
            "逐精灵色调 5",
            "逐精灵色调 6",
            "逐精灵色调 7",
        ];

        private readonly Rectangle[] _tiles = new Rectangle[TileCount];
        private readonly Stopwatch _sw = Stopwatch.StartNew();

        private Texture2D? _chart;
        private SpriteFont? _small;
        private ShaderEffect? _fx;
        private Material? _material;
        private string _error = string.Empty;

        private float _time;
        private long _batchDrawCalls = -1;

        public override void LoadContent()
        {
            _chart = MakeChecker(256, Color.White, new Color(32, 42, 62));
            _small = new SpriteFont(Device, 13f);

            try
            {
                _fx = (ShaderEffect)Device.CreateShaderEffect(FragmentSource);
            }
            catch (Exception ex)
            {
                _error = ex.Message;
                Console.Error.WriteLine($"[SpriteParamScene] 着色器创建失败：{ex.Message}");
                return;
            }

            // 共享材质：只有一份 uTint 基线（整批共用）。逐精灵差异全部走顶点参数。
            _material = new Material { Effect = _fx };
            _material.SetColor("uTint", Color.White);
        }

        public override void Update() => _time = (float)_sw.Elapsed.TotalSeconds;

        public override void Draw()
        {
            SpriteBatch batch = Batch;

            batch.Begin();
            DrawHeader(batch);
            DrawFooter(batch);
            batch.End();

            LayoutTiles();

            if (_material is null || _chart is null || _small is null)
            {
                batch.Begin();
                DrawLine(batch, $"着色器不可用：{_error}", 28f, 96f, new Color(255, 150, 150));
                batch.End();
                return;
            }

            // ---- 关键：整批只 Begin/End 一次，参数逐精灵给（这就是第 7 页做不到的写法）----
            long before = Device.Metrics.DrawCount;
            batch.Begin(_material);
            for (int i = 0; i < TileCount; i++)
            {
                var parameters = SpriteParams.FromColor(TileColors[i],
                    new System.Numerics.Vector4(i / (float)TileCount, 0f, 0f, 0f));
                batch.Draw(_chart, _tiles[i], Color.White, parameters);
            }
            batch.End();
            _batchDrawCalls = Device.Metrics.DrawCount - before;

            // ---- 读数与说明 ----
            batch.Begin();

            float readoutY = _tiles[0].Y - 58f;
            DrawLine(batch, $"本批 {TileCount} 个精灵（各自色调 / 相位）→ DrawCall 增量 = {_batchDrawCalls}",
                28f, readoutY, new Color(120, 200, 160));
            DrawLine(batch, "第 7 页对照：属性块 + SetSpriteChannels 映射后同样只 1 次 DrawCall —— 两条路是同一机制的两个入口（块按属性名 / 这里按数值）",
                28f, readoutY + 20f, new Color(255, 206, 110));

            for (int i = 0; i < TileCount; i++)
            {
                Rectangle tile = _tiles[i];
                float textY = tile.Y + tile.Height + 4f;
                batch.DrawString(_small, TileTitles[i], new Vector2(tile.X, textY), new Color(200, 220, 255));
                batch.DrawString(_small, DescribeParams(i), new Vector2(tile.X, textY + 18f), new Color(140, 158, 190));
            }

            batch.DrawString(_small,
                "参数通道：顶点里 8 个字节（2 组 unorm8x4）→ 顶点着色器 aParams0/aParams1 → 片元着色器 vParams0/vParams1；引擎不做语义解释",
                new Vector2(28f, Device.Viewport.Height - 66f), new Color(120, 200, 160));

            batch.End();
        }

        /// <summary>把该格实际写进顶点参数的 8 个通道读出来展示（P0..P7）。</summary>
        private string DescribeParams(int index)
        {
            SpriteParams p = SpriteParams.FromColor(TileColors[index],
                new System.Numerics.Vector4(index / (float)TileCount, 0f, 0f, 0f));
            return $"P0..P3=#{p.P0:X2}{p.P1:X2}{p.P2:X2}{p.P3:X2} P4={p.P4}";
        }

        /// <summary>把 8 个格子排成 4 列 2 行。</summary>
        private void LayoutTiles()
        {
            const int cols = 4;
            float x0 = 28f;
            float y0 = 160f;   // 上方要给两行读数留位置（读数画在 _tiles[0].Y - 58 处）

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

        /// <summary>
        /// 本页的片元着色器：材质基线 uTint 一份（整批共用），逐精灵值全部来自顶点参数。
        /// 混合权重 a=1 时相当于"完全覆盖材质"，a=0 时完全用材质基线 —— 这就是 MaterialPropertyBlock 的覆盖语义在着色器里的等价写法。
        /// </summary>
        private const string FragmentSource = @"#version 300 es
precision highp float;
in vec2 vTexCoord;
in vec4 vColor;
in vec4 vParams0;   // 逐精灵：rgb = 色调, a = 与材质基线的混合权重
in vec4 vParams1;   // 逐精灵：x = 脉冲相位
uniform sampler2D uTexture;
uniform vec4 uTint;      // 材质基线（整批一份，Material.SetColor 甚至不会随精灵变）
uniform float uTime;
out vec4 fragColor;
void main()
{
    vec4 c = texture(uTexture, vTexCoord) * vColor;
    vec3 tint = mix(uTint.rgb, vParams0.rgb, vParams0.a);
    float pulse = 0.72 + 0.28 * sin(uTime * 3.0 + vParams1.x * 6.2831853);
    fragColor = vec4(c.rgb * tint * pulse, c.a);
}";
    }
}
