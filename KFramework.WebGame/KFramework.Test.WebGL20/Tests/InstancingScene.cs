using System;
using System.Diagnostics;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.WebGL20.Tests
{
    /// <summary>
    /// GPU 实例化（GPU Instancing）测试：<b>一次 DrawCall 画出成百上千个各有外观的精灵</b>。
    /// <para>
    /// 几何只有 4 个顶点的单位四边形（静态），逐实例的「对象→世界矩阵 / 颜色 / UV 矩形」
    /// 放进第二根缓冲（<c>vertexAttribDivisor = 1</c>，每实例 84 字节），一次 <c>drawElementsInstanced</c> 全画完。
    /// 那个矩阵就是 Unity 的 <c>unity_ObjectToWorld</c>：位置 / 尺寸 / 旋转只是它的一种填法
    /// （<see cref="GpuInstance.CreateObjectToWorld"/>，等价于 Unity 的 <c>Matrix4x4.TRS</c> + 单位四边形居中）。
    /// </para>
    /// <para>
    /// 这条路与本引擎的 CPU 合批（<see cref="SpriteBatch"/> 的逐顶点路径）是<b>两条互不相干的路子</b>：
    /// SpriteBatch 按纹理/属性块分批、逐批一次 drawElements，几何在 CPU 侧展开；
    /// 本页走 <see cref="GpuInstanceBatch"/>（显式 Begin → Draw × N → End），几何只有一份单位四边形。
    /// 想省 DrawCall 与 CPU 带宽 ⇒ 用本页这条；需要逐批换材质状态或覆盖任意 uniform ⇒ 回到 SpriteBatch。
    /// </para>
    /// <para>
    /// <b>逐实例差异只能来自实例数据</b>：本路不接受 <see cref="ShaderPropertyBlock"/>（一次实例化 draw 只有一份
    /// uniform，装不下"逐实例不同"的任意属性）。本页的逐实例脉冲就靠实例数据里的字段实现 ——
    /// 片元着色器用顶点着色器透传的内置实例号 <c>vInstanceID</c> 取相位，"每个实例各不相同"而整批仍只有一次 DrawCall。
    /// 想按物体覆盖 uniform 请回 <see cref="SpriteBatch"/>（切批）或 <see cref="UrpBatch"/>（不切批但 DrawCall = 物体数）。
    /// </para>
    /// <para>交互：<b>空格</b>切换实例数量档位（256 / 1024 / 4096）。</para>
    /// </summary>
    public sealed class InstancingScene : DemoScene
    {
        public override string Title => "7) GPU 实例化（GpuInstanceBatch）：一次 DrawCall 画 N 个精灵";

        protected override string Description
            => "同纹理的 N 个精灵各自有位置/尺寸/旋转/颜色/UV：一次 drawElementsInstanced 全画完（独立于 SpriteBatch 的 CPU 合批）";

        /// <summary>可切换的实例数量档位（空格键循环）。</summary>
        private static readonly int[] CountPresets = [256, 1024, 4096];

        /// <summary>实例方阵的左上角（正文起点：让开标题与描述）。</summary>
        private const float GridLeft = 28f;
        private const float GridTop = 116f;

        private readonly Stopwatch _sw = Stopwatch.StartNew();

        private Texture2D? _chart;
        private SpriteFont? _small;
        private GpuInstanceBatch? _instances;

        /// <summary>本批材质：自定义片元着色器挂在它身上，<see cref="GpuInstanceBatch.Begin"/> 时传入。</summary>
        private Material? _material;
        private string _error = string.Empty;

        private float _time;
        private int _presetIndex = 1;
        private long _lastDrawCalls = -1;
        private int _lastInstances;

        /// <summary>CPU 侧提交耗时（毫秒，EMA 平滑）：C# 里组矩阵 + 写实例数据 + 上传那一段。</summary>
        private double _submitMs;

        /// <summary>提交里"纯 C# 组数据"那半段（Begin + N×Add）的耗时，EMA 平滑。</summary>
        private double _addMs;

        /// <summary>提交里"GL 提交"那半段（End：上传实例数据 + 一次 drawElementsInstanced）的耗时，EMA 平滑。</summary>
        private double _endMs;

        /// <summary>帧间隔（毫秒，EMA 平滑）≈ 整帧耗时，用来分辨掉帧到底在 CPU 还是 GPU。</summary>
        private double _frameMs;
        private long _lastFrameTicks;

        public override void LoadContent()
        {
            _chart = MakeChecker(64, Color.White, new Color(36, 46, 66));
            _small = new SpriteFont(Device, 13f);

            // 材质与 SpriteBatch 一样在 Begin 里给，不再进构造函数。
            _material = new Material();
            // 自定义着色器走 Material.Effect（与 SpriteBatch 同一条路子，不另开参数）：
            // 本页用它做"逐实例明暗脉冲"（读内置实例号 vInstanceID）。
            _material.Effect = Device.CreateShaderEffect(FragmentSource);

            try
            {
                _instances = new GpuInstanceBatch(Device);
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
            DrawLine(batch, "空格键切换实例数量档位", 28f, 80f, new Color(150, 165, 195));
            DrawFooter(batch);
            batch.End();

            if (_chart is null || _small is null) return;

            if (_instances is null || !_instances.IsSupported)
            {
                batch.Begin();
                DrawLine(batch, $"实例化初始化失败：{_error}", 28f, GridTop, new Color(255, 150, 150));
                DrawLine(batch, $"当前后端（{Device.BackendName}）尚未接入 GPU 实例化。", 28f, GridTop + 24f, new Color(255, 206, 110));
                DrawLine(batch, "WebGL2 后端支持；WebGPU 需要另一套带实例属性的管线。", 28f, GridTop + 48f, new Color(150, 165, 195));
                batch.End();
                return;
            }

            int count = CountPresets[_presetIndex];

            // 帧间隔：Draw 每帧调一次，两次之间的间隔 ≈ 整帧耗时（含 C# 逻辑、渲染与浏览器合成）。
            long now = Stopwatch.GetTimestamp();
            if (_lastFrameTicks != 0)
            {
                _frameMs = Ema(_frameMs, (now - _lastFrameTicks) * 1000.0 / Stopwatch.Frequency);
            }
            _lastFrameTicks = now;

            // ---- 关键：N 个实例一次提交 ----
            long before = Device.Metrics.DrawCount;
            long submitStart = Stopwatch.GetTimestamp();
            Submit(count, out double addMs, out double endMs);
            double submit = (Stopwatch.GetTimestamp() - submitStart) * 1000.0 / Stopwatch.Frequency;
            _addMs = Ema(_addMs, addMs);
            _endMs = Ema(_endMs, endMs);
            _submitMs = Ema(_submitMs, submit);
            _lastDrawCalls = Device.Metrics.DrawCount - before;
            _lastInstances = count;

            // ---- 读数 ----
            batch.Begin();

            float readoutY = Device.Viewport.Height - 152f;
            DrawLine(batch, $"实例数 = {_lastInstances} → DrawCall 增量 = {_lastDrawCalls}（单次上限 4096，超出自动分批）",
                28f, readoutY, new Color(120, 200, 160));
            DrawLine(batch, $"CPU 侧带宽/精灵：实例化 84 字节（本页 = {_lastInstances * 84 / 1024} KB）   对照 SpriteBatch 逐顶点 4×28 = 112 字节（= {_lastInstances * 112 / 1024} KB）",
                28f, readoutY + 20f, new Color(255, 206, 110));
            DrawLine(batch, "GpuInstanceBatch：Begin → Draw × N → End；逐实例矩阵即 Unity 的 unity_ObjectToWorld，逐实例差异只在实例数据里（矩阵 / 颜色 / UV），整批仍 1 次 DC",
                28f, readoutY + 40f, new Color(150, 165, 195));
            DrawLine(batch, $"提交拆分：组数据(Begin+Add×N) = {_addMs:F2} ms　GL 提交(End) = {_endMs:F2} ms　合计 {_submitMs:F2} ms　帧间隔 = {_frameMs:F1} ms（≈ {(_frameMs > 0 ? 1000.0 / _frameMs : 0):F0} FPS）",
                28f, readoutY + 60f, new Color(180, 220, 255));

            batch.End();
        }

        /// <summary>
        /// 显式 Begin / Draw × N / End：N 个实例一次 drawElementsInstanced。
        /// 顺手把两段耗时分别带出来 —— 这是分辨"到底哪一侧慢"的关键数据：
        /// <list type="bullet">
        ///   <item><description><paramref name="addMs"/>：纯 C# 组数据（组矩阵 + 写实例数据），不碰 GL。</description></item>
        ///   <item><description><paramref name="endMs"/>：GL 提交（上传实例缓冲 + 一次 drawElementsInstanced）。这一侧变慢通常意味着
        ///   GPU / 驱动队列积压（WebGL 调用会把 CPU 阻塞到队列有位置为止），而不是 C# 慢。</description></item>
        /// </list>
        /// </summary>
        private void Submit(int count, out double addMs, out double endMs)
        {
            ComputeLayout(count, out int cols, out float cellW, out float cellH, out float size);

            Texture2D chart = _chart!;
            // 锚点放在纹理中心；目标矩形给位置与尺寸，origin 会按 源尺寸 → 矩形尺寸 自动放大（照 SpriteBatch）。
            var origin = new Vector2(chart.Width / 2f, chart.Height / 2f);
            int side = (int)MathF.Round(size);

            long t0 = Stopwatch.GetTimestamp();

            _instances!.Begin(_material);
            for (int i = 0; i < count; i++)
            {
                int c = i % cols;
                int r = i / cols;

                var center = new Vector2(GridLeft + (c + 0.5f) * cellW, GridTop + (r + 0.5f) * cellH);
                float rotation = _time * 1.2f + (r + c) * 0.06f;

                // 目标矩形：X/Y 是"锚点落点"（origin 给了纹理中心，所以就是精灵中心落在 center），Width/Height 是尺寸
                // —— 语义与 SpriteBatch 的目标矩形重载一致。
                var target = new Rectangle((int)center.X, (int)center.Y, side, side);

                // Draw 的参数与 SpriteBatch 的「目标矩形」重载逐参数对应（纹理也在这里给）。
                _instances.Draw(chart, target, null, Hue(i * 0.013f), rotation, origin, Vector2.One, SpriteEffects.None, 0f);
            }

            long t1 = Stopwatch.GetTimestamp();
            _instances.End();
            long t2 = Stopwatch.GetTimestamp();

            addMs = (t1 - t0) * 1000.0 / Stopwatch.Frequency;
            endMs = (t2 - t1) * 1000.0 / Stopwatch.Frequency;
        }

        /// <summary>指数滑动平均（0.9/0.1），让读数不被单帧抖动带偏。</summary>
        private static double Ema(double previous, double value)
            => previous <= 0 ? value : previous * 0.9 + value * 0.1;

        /// <summary>方阵布局。</summary>
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
            _instances?.Dispose();
            _instances = null;
        }

        /// <summary>
        /// 片元着色器：几何（四边形、UV）由单位四边形提供，色调来自逐实例颜色 vColor，
        /// 明暗脉冲用 vInstanceID（内置实例号经顶点着色器透传）取相位 —— 这就是"逐实例不同"在本路的写法：
        /// 差异只能来自实例数据（矩阵 / 颜色 / UV）与内置实例号，没有可变 uniform 可用。
        /// </summary>
        private const string FragmentSource = @"#version 300 es
precision highp float;
in vec2 vTexCoord;
in vec4 vColor;                 // 逐实例：色调（实例数据里的 Tint）
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
