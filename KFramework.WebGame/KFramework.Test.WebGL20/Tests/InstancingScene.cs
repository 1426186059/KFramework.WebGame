using System;
using System.Diagnostics;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.WebGL20.Tests
{
    /// <summary>
    /// GPU 实例化（GPU Instancing）测试：<b>一次 DrawCall 画出成百上千个各有外观的精灵</b>。
    /// <para>
    /// 几何只有 4 个顶点的单位四边形（静态），位置/尺寸/旋转/颜色/UV 矩形按实例放进第二根缓冲
    /// （<c>vertexAttribDivisor = 1</c>，每实例 40 字节），一次 <c>drawElementsInstanced</c> 全画完。
    /// </para>
    /// <para>
    /// <b>同一份实例数据有两条提交 API，用页面上的按钮（或 I 键）切换</b>，DrawCall 增量都应该是 1：
    /// <list type="bullet">
    ///   <item><description><b>A) SpriteInstancer 直连</b>：显式 Begin → Add × N → End；
    ///   可以传自定义片元着色器（本页用它做了逐实例明暗脉冲）。</description></item>
    ///   <item><description><b>B) SpriteBatch + 材质的 EnableInstancing</b>：把实例化接进常规 SpriteBatch 流程，
    ///   逐实例数据取自 Draw 的位置/尺寸/旋转/颜色；用的是引擎内置实例化着色器（纹理 × 逐实例颜色），
    ///   所以 B 比 A 少一个脉冲动画，这是两条 API 目前唯一的差别。</description></item>
    /// </list>
    /// </para>
    /// <para>交互：<b>按钮 / I 键切换 API</b>；<b>空格</b>切换实例数量档位（256 / 1024 / 4096）。</para>
    /// </summary>
    public sealed class InstancingScene : DemoScene
    {
        public override string Title => "7) GPU 实例化：一次 DrawCall 画 N 个精灵";

        protected override string Description
            => "两条实例化 API 共用同一份逐实例数据（40 字节/实例）：A) SpriteInstancer，B) SpriteBatch 实例化模式";

        /// <summary>可切换的实例数量档位（空格键循环）。</summary>
        private static readonly int[] CountPresets = [256, 1024, 4096];

        /// <summary>实例化的两条提交 API。</summary>
        private enum SubmissionApi
        {
            /// <summary>直接 new SpriteInstancer，显式 Begin / Add / End。</summary>
            Instancer,
            /// <summary>常规 SpriteBatch，开实例化开关，逐物体数据取自 Draw 的参数。</summary>
            SpriteBatch,
        }

        private static readonly string[] ApiNames =
        {
            "A) SpriteInstancer 直连",
            "B) SpriteBatch（材质 EnableInstancing）",
        };

        /// <summary>实例方阵的左上角（正文起点：让开标题、描述与 API 按钮）。</summary>
        private const float GridLeft = 28f;
        private const float GridTop = 116f;

        private readonly Stopwatch _sw = Stopwatch.StartNew();

        private Texture2D? _chart;
        private SpriteFont? _small;
        private SpriteInstancer? _instancer;

        /// <summary>B 路的材质（逐实例数据来自 Draw 参数，材质只定渲染状态）。</summary>
        private Material? _batchMaterial;

        private string _error = string.Empty;

        private float _time;
        private int _presetIndex = 1;
        private SubmissionApi _api = SubmissionApi.Instancer;
        private long _lastDrawCalls = -1;
        private int _lastInstances;

        private Rectangle _button;

        public override void LoadContent()
        {
            _chart = MakeChecker(64, Color.White, new Color(36, 46, 66));
            _small = new SpriteFont(Device, 13f);

            // B 路：逐实例数据全部取自 Draw 的参数（位置/尺寸/旋转/颜色），材质定渲染状态 + 打开实例化。
            _batchMaterial = new Material
            {
                Blend = BlendState.NonPremultiplied,
                Sampler = SamplerState.Point,
                EnableInstancing = true,
            };

            try
            {
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
            LayoutButton();

            if (Input_KeyBoard.GetKeyDown(Keys.Space))
                _presetIndex = (_presetIndex + 1) % CountPresets.Length;

            // 按钮点击 / I 键：在两条实例化 API 之间切换。
            // （不用 Tab：Tab 会让画布失焦，随即被清空按键状态，实测抓不稳。）
            bool clicked = Input_Mouse.GetButtonDown(MouseButton.Left) && _button.Contains(Input_Mouse.Position);
            if (clicked || Input_KeyBoard.GetKeyDown(Keys.KeyI))
                _api = _api == SubmissionApi.Instancer ? SubmissionApi.SpriteBatch : SubmissionApi.Instancer;
        }

        public override void Draw()
        {
            SpriteBatch batch = Batch;

            batch.Begin();
            DrawHeader(batch);
            DrawApiButton(batch);
            DrawFooter(batch);
            batch.End();

            if (_chart is null || _small is null) return;

            if (_instancer is null || !_instancer.IsSupported)
            {
                batch.Begin();
                DrawLine(batch, $"实例化初始化失败：{_error}", 28f, GridTop, new Color(255, 150, 150));
                DrawLine(batch, $"当前后端（{Device.BackendName}）尚未接入 GPU 实例化。", 28f, GridTop + 24f, new Color(255, 206, 110));
                DrawLine(batch, "WebGL2 后端支持；WebGPU 需要另一套带实例属性的管线。", 28f, GridTop + 48f, new Color(150, 165, 195));
                batch.End();
                return;
            }
            else
            {
                int count = CountPresets[_presetIndex];

                // ---- 关键：N 个实例一次提交（两条 API 的读数应当一致）----
                long before = Device.Metrics.DrawCount;
                if (_api == SubmissionApi.Instancer)
                {
                    SubmitViaInstancer(count);
                }
                else
                {
                    SubmitViaSpriteBatch(count);
                }
                _lastDrawCalls = Device.Metrics.DrawCount - before;
                _lastInstances = count;

                // ---- 读数 ----
                batch.Begin();

                float readoutY = Device.Viewport.Height - 152f;
                DrawLine(batch, $"当前 API = {ApiNames[(int)_api]}    实例数 = {_lastInstances} → DrawCall 增量 = {_lastDrawCalls}（单次上限 4096，超出自动分批）",
                    28f, readoutY, new Color(120, 200, 160));
                DrawLine(batch, $"CPU 侧带宽/精灵：实例化 40 字节（本页 = {_lastInstances * 40 / 1024} KB）   对照 SpriteBatch 逐顶点 4×28 = 112 字节（= {_lastInstances * 112 / 1024} KB）",
                    28f, readoutY + 20f, new Color(255, 206, 110));
                DrawLine(batch, "两条 API 都是一次 drawElementsInstanced；差别只在着色器：A 用本页自定义片元着色器（逐实例明暗脉冲），B 用引擎内置实例化着色器（纹理 × 逐实例颜色）",
                    28f, readoutY + 40f, new Color(150, 165, 195));

                batch.End();
            }
        }

        /// <summary>A 路：直连 SpriteInstancer（显式 Begin / Add / End）。</summary>
        private void SubmitViaInstancer(int count)
        {
            ComputeLayout(count, out int cols, out float cellW, out float cellH, out float size);

            _instancer!.Begin();
            for (int i = 0; i < count; i++)
            {
                int c = i % cols;
                int r = i / cols;

                var center = new Vector2(GridLeft + (c + 0.5f) * cellW, GridTop + (r + 0.5f) * cellH);
                float rotation = _time * 1.2f + (r + c) * 0.06f;

                // 逐实例色调：直接作为实例数据里的 tint（着色器的 vColor）。
                _instancer.Add(center, new Vector2(size, size), rotation, Hue(i * 0.013f));
            }
            _instancer.End();
        }

        /// <summary>
        /// B 路：走常规 SpriteBatch 的实例化模式 —— Begin 时打开开关，逐实例数据由 Draw 的参数给。
        /// 一个 Begin / End 之间排队的全部实例，到 End 时按纹理分段、一次实例化 draw 提交。
        /// </summary>
        private void SubmitViaSpriteBatch(int count)
        {
            ComputeLayout(count, out int cols, out float cellW, out float cellH, out float size);

            Texture2D chart = _chart!;
            var origin = new Vector2(chart.Width / 2f, chart.Height / 2f);   // 以精灵中心为锚点，语义与 A 路的 center 对齐
            var scale = new Vector2(size / chart.Width, size / chart.Height);

            Batch.Begin(_batchMaterial!, SpriteSortMode.Deferred, null);
            for (int i = 0; i < count; i++)
            {
                int c = i % cols;
                int r = i / cols;

                var center = new Vector2(GridLeft + (c + 0.5f) * cellW, GridTop + (r + 0.5f) * cellH);
                float rotation = _time * 1.2f + (r + c) * 0.06f;

                Batch.Draw(chart, center, null, Hue(i * 0.013f), rotation, origin, scale,
                           SpriteEffects.None, 0f);
            }
            Batch.End();
        }

        /// <summary>方阵布局：两条 API 共用，保证画出来的位置、尺寸完全一致。</summary>
        private void ComputeLayout(int count, out int cols, out float cellW, out float cellH, out float size)
        {
            cols = (int)MathF.Ceiling(MathF.Sqrt(count));
            int rows = (int)MathF.Ceiling(count / (float)cols);

            float width = Math.Max(64f, Device.Viewport.Width - GridLeft * 2f);
            float height = Math.Max(64f, Device.Viewport.Height - GridTop - 168f);

            cellW = width / cols;
            cellH = height / rows;
            size = MathF.Max(3f, MathF.Min(cellW, cellH) * 0.92f);
        }

        // ================= 按钮 =================

        private void LayoutButton() => _button = new Rectangle(28, 74, 470, 30);

        private void DrawApiButton(SpriteBatch batch)
        {
            LayoutButton();

            bool hover = _button.Contains(Input_Mouse.Position);
            batch.Draw(KDefaultRes.DefaultTexture2D, _button, hover ? new Color(58, 84, 130) : new Color(34, 46, 72));
            batch.DrawString(Font, $"当前 API：{ApiNames[(int)_api]}    ← 点击按钮 / I 键切换",
                new Vector2(_button.X + 14f, _button.Y + 6f), Color.White);
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
        /// A 路（SpriteInstancer）的片元着色器：几何（四边形、UV）由单位四边形提供，色调来自逐实例颜色 vColor，
        /// 明暗脉冲用实例号 vInstanceID 做相位（实例化程序没有 uTime 这类 uniform，实例号是唯一可用的逐实例标量）。
        /// </summary>
        private const string FragmentSource = @"#version 300 es
precision highp float;
in vec2 vTexCoord;
in vec4 vColor;                 // 逐实例：色调
flat in int vInstanceID;        // 逐实例：实例号（内置 gl_InstanceID 经顶点着色器透传）
uniform sampler2D uTexture;
out vec4 fragColor;
void main()
{
    vec4 c = texture(uTexture, vTexCoord) * vColor;
    float pulse = 0.70 + 0.30 * sin(float(vInstanceID) * 0.7);
    fragColor = vec4(c.rgb * pulse, c.a);
}";
    }
}
