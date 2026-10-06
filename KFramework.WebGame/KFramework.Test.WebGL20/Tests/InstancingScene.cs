using System;
using System.Diagnostics;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.WebGL20.Tests
{
    /// <summary>
    /// GPU 实例化（GPU Instancing）测试：<b>一次 DrawCall 画出成百上千个各有外观的精灵</b>。
    /// <para>
    /// 做法：几何只有 4 个顶点的单位四边形（静态），位置/尺寸/旋转/颜色/参数按实例放进第二根缓冲
    /// （<c>vertexAttribDivisor = 1</c>，每实例 32 字节），一次 <c>drawElementsInstanced</c> 全画完。
    /// </para>
    /// <para>
    /// 与另外两条路的对照：
    /// <list type="bullet">
    ///   <item><description>MaterialPropertyBlock / SpriteParams：逐物体值进顶点通道（1 次 DrawCall），但 CPU 侧每精灵要写 4 顶点 × 28 字节 = 112 字节；</description></item>
    ///   <item><description>本页实例化：1 次 DrawCall，且每实例只要 32 字节。</description></item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class InstancingScene : DemoScene
    {
        public override string Title => "9) GPU 实例化：一次 DrawCall 画 N 个精灵";

        protected override string Description
            => "单位四边形 + 逐实例缓冲（divisor=1）+ drawElementsInstanced：每实例只写 32 字节，空格键切换数量";

        /// <summary>可切换的实例数量档位（空格键循环）。</summary>
        private static readonly int[] CountPresets = [256, 1024, 4096];

        private readonly Stopwatch _sw = Stopwatch.StartNew();

        private Texture2D? _chart;
        private SpriteFont? _small;
        private SpriteInstancer? _instancer;
        private string _error = string.Empty;

        private float _time;
        private int _presetIndex = 1;
        private long _lastDrawCalls = -1;
        private int _lastInstances;

        public override void LoadContent()
        {
            _chart = MakeChecker(64, Color.White, new Color(36, 46, 66));
            _small = new SpriteFont(Device, 13f);

            try
            {
                // 逐实例参数（vParams0 / vParams1）在本页着色器里参与计算，用来证明"每个实例真的不一样"。
                _instancer = new SpriteInstancer(Device, _chart, fragmentSource: FragmentSource, capacity: 4096);
            }
            catch (Exception ex)
            {
                _error = ex.Message;
                Console.Error.WriteLine($"[InstancingScene] 实例化初始化失败：{ex.Message}");
            }
        }

        public override void Update()
        {
            _time = (float)_sw.Elapsed.TotalSeconds;

            if (Input_KeyBoard.GetKeyDown(Keys.Space))
                _presetIndex = (_presetIndex + 1) % CountPresets.Length;
        }

        public override void Draw()
        {
            SpriteBatch batch = Batch;

            batch.Begin();
            DrawHeader(batch);
            DrawFooter(batch);
            batch.End();

            if (_instancer is null || _chart is null || _small is null)
            {
                batch.Begin();
                DrawLine(batch, $"实例化初始化失败：{_error}", 28f, 96f, new Color(255, 150, 150));
                batch.End();
                return;
            }

            if (!_instancer.IsSupported)
            {
                batch.Begin();
                DrawLine(batch, $"当前后端（{Device.BackendName}）尚未接入 GPU 实例化。", 28f, 96f, new Color(255, 206, 110));
                DrawLine(batch, "WebGL2 后端支持；WebGPU 需要另一套带实例属性的管线，见 SpriteInstancer 注释。",
                    28f, 122f, new Color(150, 165, 195));
                batch.End();
                return;
            }

            int count = CountPresets[_presetIndex];

            // ---- 关键：N 个实例一次提交 ----
            long before = Device.Metrics.DrawCount;
            SubmitInstances(count);
            _lastDrawCalls = Device.Metrics.DrawCount - before;
            _lastInstances = count;

            // ---- 读数 ----
            batch.Begin();

            float readoutY = Device.Viewport.Height - 132f;
            DrawLine(batch, $"实例数 = {_lastInstances} → DrawCall 增量 = {_lastDrawCalls}（缓冲容量 {_instancer.Capacity}，超出自动分批）",
                28f, readoutY, new Color(120, 200, 160));
            DrawLine(batch, $"CPU 侧带宽/精灵：实例化 32 字节（本页 = {_lastInstances * 32 / 1024} KB）   对照 SpriteBatch 逐顶点 4×28 = 112 字节（= {_lastInstances * 112 / 1024} KB）",
                28f, readoutY + 20f, new Color(255, 206, 110));
            DrawLine(batch, "对照 SpriteBatch + 属性块（第 7/8 页）：同为 1 次 DrawCall，但逐顶点路径每精灵要写 4 顶点 × 28 字节",
                28f, readoutY + 40f, new Color(150, 165, 195));

            batch.End();
        }

        /// <summary>把 count 个实例排成方阵提交：位置/旋转/色调/相位都各不相同。</summary>
        private void SubmitInstances(int count)
        {
            int cols = (int)MathF.Ceiling(MathF.Sqrt(count));
            int rows = (int)MathF.Ceiling(count / (float)cols);

            float left = 28f;
            float top = 88f;
            float width = Math.Max(64f, Device.Viewport.Width - 56f);
            float height = Math.Max(64f, Device.Viewport.Height - 88f - 156f);

            float cellW = width / cols;
            float cellH = height / rows;
            float size = MathF.Max(3f, MathF.Min(cellW, cellH) * 0.92f);

            _instancer!.Begin();
            for (int i = 0; i < count; i++)
            {
                int c = i % cols;
                int r = i / cols;

                var center = new Vector2(left + (c + 0.5f) * cellW, top + (r + 0.5f) * cellH);
                float rotation = _time * 1.2f + (r + c) * 0.06f;
                float phase = (i % 16) / 16f;

                // 逐实例色调（P0~P3）+ 逐实例相位（P4）——都在 vParams0 / vParams1 里被着色器使用。
                var parameters = SpriteParams.FromColor(Hue(i * 0.013f), new System.Numerics.Vector4(phase, 0f, 0f, 0f));
                _instancer.Add(center, new Vector2(size, size), rotation, Color.White, parameters);
            }
            _instancer.End();
        }

        /// <summary>HSV → RGB（h 取 0~1），用于给每个实例一个明显不同的色调。</summary>
        private static Color Hue(float h)
        {
            h -= MathF.Floor(h);
            float s = 0.75f, v = 1f;
            float i = MathF.Floor(h * 6f);
            float f = h * 6f - i;
            float p = v * (1f - s);
            float q = v * (1f - f * s);
            float t = v * (1f - (1f - f) * s);

            return ((int)i % 6) switch
            {
                0 => new Color(v, t, p),
                1 => new Color(q, v, p),
                2 => new Color(p, v, t),
                3 => new Color(p, q, v),
                4 => new Color(t, p, v),
                _ => new Color(v, p, q),
            };
        }

        public override void Dispose()
        {
            _instancer?.Dispose();
            _instancer = null;
        }

        /// <summary>
        /// 本页片元着色器：几何（四边形、UV）由单位四边形提供，外观全部来自逐实例数据。
        /// vColor 是逐实例颜色，vParams0/vParams1 是逐实例的 8 个参数（这里 P0..P3 = 色调、P4 = 脉冲相位）。
        /// </summary>
        private const string FragmentSource = @"#version 300 es
precision highp float;
in vec2 vTexCoord;
in vec4 vColor;
in vec4 vParams0;   // 逐实例：rgb = 色调
in vec4 vParams1;   // 逐实例：x = 脉冲相位
uniform sampler2D uTexture;
uniform float uTime;
out vec4 fragColor;
void main()
{
    vec4 c = texture(uTexture, vTexCoord) * vColor;
    float pulse = 0.70 + 0.30 * sin(uTime * 3.0 + vParams1.x * 6.2831853);
    fragColor = vec4(c.rgb * vParams0.rgb * pulse, c.a);
}";
    }
}
