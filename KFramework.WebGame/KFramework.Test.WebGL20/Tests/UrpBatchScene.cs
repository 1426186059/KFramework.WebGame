using System;
using System.Diagnostics;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;
// 引擎自带 Vector2（KFramework.MonoGame.Vector2），与 System.Numerics 同名：
// 这里只给 Vector4（材质常量）起个别名，避免同文件两个 using 造成 Vector2 二义。
using Vector4 = System.Numerics.Vector4;

namespace KFramework.Test.WebGL20.Tests
{
    /// <summary>
    /// SRP Batcher 式批处理（照 Unity 的 URP / SRP Batcher）测试：<b>DrawCall 不减少，但换物体几乎不花钱</b>。
    /// <para>
    /// 机制（三条，对应 Unity 的三条硬性要求）：
    /// <list type="number">
    ///   <item><description><b>逐材质常量进一份常驻 UBO</b>（<c>UnityPerMaterial</c>）：材质不变就一次都不重传。
    ///   本页让材质色每帧轻微脉动，于是"材质变了 → 本帧只传 1 次"（而不是每个物体 1 次）。</description></item>
    ///   <item><description><b>逐物体常量进另一份 UBO</b>（<c>UnityPerDraw</c> / <c>unity_ObjectToWorld</c>）：
    ///   整段一次性上传 <see cref="UrpDrawData"/>（矩阵 + UV 矩形 + 颜色）。</description></item>
    ///   <item><description><b>同一 shader 连续绘制不切程序、不设 uniform</b>：每个物体只是
    ///   <c>bindBufferRange</c> 换一段偏移 + <c>drawElements</c> —— 这就是 SRP Batcher 省下来的东西。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 三条路一眼对照（都能"同材质同纹理画 N 个精灵"）：
    /// <list type="table">
    ///   <item><term>第 7 页 <see cref="GpuInstanceBatch"/></term><description>GPU 实例化：DrawCall = 1 / 缓冲容量；逐物体数据走顶点属性（受槽位与格式限制）。</description></item>
    ///   <item><term>第 8 页 <see cref="ShaderPropertyBlock"/></term><description>CPU 合批 + 可变 uniform：块一变就切批，N 个不同块 = N 次 DrawCall。</description></item>
    ///   <item><term>本页 <see cref="UrpBatch"/></term><description>DrawCall = N（不降），但每个物体只重绑一次 UBO 范围；数据走 uniform buffer，矩阵 / 整型 / 任意分量都装得下。</description></item>
    /// </list>
    /// </para>
    /// <para>交互：<b>空格</b>切换物体数量档位（256 / 1024 / 4096）。</para>
    /// </summary>
    public sealed class UrpBatchScene : DemoScene
    {
        public override string Title => "9) SRP Batcher 式批处理（UrpBatch）：换物体只剩一次绑定";

        protected override string Description
            => "同纹理 N 个物体：逐物体常量整段一次上传，之后每个物体只 bindBufferRange 换偏移 + drawElements —— DrawCall 不降，换物体的 CPU 开销 ≈ 一次绑定";

        /// <summary>可切换的物体数量档位（空格键循环）。</summary>
        private static readonly int[] CountPresets = [256, 1024, 4096];

        private const float GridLeft = 28f;
        private const float GridTop = 116f;

        private readonly Stopwatch _sw = Stopwatch.StartNew();

        private Texture2D? _chart;
        private SpriteFont? _small;
        private UrpBatch? _urp;
        private string _error = string.Empty;

        private float _time;
        private int _presetIndex = 1;
        private int _lastObjects;
        private long _lastDrawCalls = -1;
        private long _lastMaterialUploads = -1;

        /// <summary>组数据（Begin + Add×N）与 GL 提交（End）两段耗时，EMA 平滑。</summary>
        private double _addMs;
        private double _endMs;

        /// <summary>帧间隔（毫秒，EMA）≈ 整帧耗时。</summary>
        private double _frameMs;
        private long _lastFrameTicks;

        public override void LoadContent()
        {
            _chart = MakeChecker(64, Color.White, new Color(36, 46, 66));
            _small = new SpriteFont(Device, 13f);

            var material = new Material
            {
                Blend = BlendState.NonPremultiplied,
                Sampler = SamplerState.Point,
            };

            try
            {
                _urp = new UrpBatch(Device, _chart, material, capacity: 4096);
            }
            catch (Exception ex)
            {
                _error = ex.Message;
                Console.Error.WriteLine($"[UrpBatchScene] URP 批处理初始化失败：{ex.Message}");
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
            DrawLine(batch, "空格键切换物体数量档位", 28f, 80f, new Color(150, 165, 195));
            DrawFooter(batch);
            batch.End();

            if (_chart is null || _small is null) return;

            if (_urp is null || !_urp.IsSupported)
            {
                batch.Begin();
                DrawLine(batch, $"URP 批处理初始化失败：{_error}", 28f, GridTop, new Color(255, 150, 150));
                DrawLine(batch, $"当前后端（{Device.BackendName}）尚未接入 SRP-Batcher 式的 UBO 绘制。", 28f, GridTop + 24f, new Color(255, 206, 110));
                DrawLine(batch, "WebGL2 后端支持（uniform buffer + bindBufferRange）；WebGPU 需要另一套动态偏移的绑定方式。", 28f, GridTop + 48f, new Color(150, 165, 195));
                batch.End();
                return;
            }

            int count = CountPresets[_presetIndex];

            // 帧间隔：Draw 每帧一次，两次之间 ≈ 整帧耗时。
            long now = Stopwatch.GetTimestamp();
            if (_lastFrameTicks != 0)
                _frameMs = Ema(_frameMs, (now - _lastFrameTicks) * 1000.0 / Stopwatch.Frequency);
            _lastFrameTicks = now;

            // 材质常量每帧脉动：既演示"材质常量变了才传一次"，也让画面有变化。
            _urp.MaterialColor = new Vector4(1f - 0.10f * MathF.Sin(_time * 2.4f),
                                             1f - 0.06f * MathF.Sin(_time * 1.7f),
                                             1f, 1f);

            long beforeDraws = Device.Metrics.DrawCount;
            long beforeUploads = _urp.MaterialUploads;

            long t0 = Stopwatch.GetTimestamp();
            BeginAdds(count, out float cellW, out float cellH, out float size);
            long t1 = Stopwatch.GetTimestamp();
            int draws = _urp.End();
            long t2 = Stopwatch.GetTimestamp();

            _addMs = Ema(_addMs, (t1 - t0) * 1000.0 / Stopwatch.Frequency);
            _endMs = Ema(_endMs, (t2 - t1) * 1000.0 / Stopwatch.Frequency);
            _lastDrawCalls = Device.Metrics.DrawCount - beforeDraws;
            _lastMaterialUploads = _urp.MaterialUploads - beforeUploads;
            _lastObjects = count;
            _ = draws;

            // ---- 读数 ----
            batch.Begin();

            float readoutY = Device.Viewport.Height - 172f;
            DrawLine(batch, $"物体数 = {_lastObjects} → DrawCall 增量 = {_lastDrawCalls}（SRP Batcher 不降 DrawCall；对照第 7 页实例化 = 1 次）",
                28f, readoutY, new Color(120, 200, 160));
            DrawLine(batch, $"材质常量上传 = {_lastMaterialUploads} 次/帧（同一份常驻 UBO，{_lastObjects} 个物体共享）   逐物体常量 = 整段 1 次上传（{_lastObjects * UrpDrawData.SizeInBytes / 1024} KB）",
                28f, readoutY + 20f, new Color(255, 206, 110));
            DrawLine(batch, "每个物体只做：bindBufferRange（换偏移）+ drawElements —— 不切程序、不设 uniform、不传数据",
                28f, readoutY + 40f, new Color(180, 220, 255));
            DrawLine(batch, $"提交拆分：组数据(Add×N) = {_addMs:F2} ms　GL 提交(End) = {_endMs:F2} ms　帧间隔 = {_frameMs:F1} ms（≈ {(_frameMs > 0 ? 1000.0 / _frameMs : 0):F0} FPS）",
                28f, readoutY + 60f, new Color(180, 220, 255));
            DrawLine(batch, "注：逐物体常量按 UNIFORM_BUFFER_OFFSET_ALIGNMENT（常见 256 字节）对齐占位，所以每条记录实际占位比 96 字节大",
                28f, readoutY + 80f, new Color(150, 165, 195));
            DrawLine(batch, $"画布上的格子尺寸 {size:F0} px（每格一个物体，旋转动画）　—　这条路的强项是数据不受顶点格式限制，弱项是 DrawCall 不降",
                28f, readoutY + 100f, new Color(150, 165, 195));

            batch.End();
        }

        /// <summary>排好 N 个物体（每个一条逐物体常量）。</summary>
        private void BeginAdds(int count, out float cellW, out float cellH, out float size)
        {
            ComputeLayout(count, out int cols, out cellW, out cellH, out size);

            _urp!.Begin();
            for (int i = 0; i < count; i++)
            {
                int c = i % cols;
                int r = i / cols;

                var center = new Vector2(GridLeft + (c + 0.5f) * cellW, GridTop + (r + 0.5f) * cellH);
                float rotation = _time * 1.2f + (r + c) * 0.06f;

                _urp.Add(center, new Vector2(size, size), rotation, Hue(i * 0.013f));
            }
        }

        /// <summary>方阵布局。</summary>
        private void ComputeLayout(int count, out int cols, out float cellW, out float cellH, out float size)
        {
            cols = (int)MathF.Ceiling(MathF.Sqrt(count));
            int rows = (int)MathF.Ceiling(count / (float)cols);

            float width = Math.Max(64f, Device.Viewport.Width - GridLeft * 2f);
            float height = Math.Max(64f, Device.Viewport.Height - GridTop - 188f);

            cellW = width / cols;
            cellH = height / rows;
            size = MathF.Max(3f, MathF.Min(cellW, cellH) * 0.92f);
        }

        /// <summary>指数滑动平均（0.9/0.1）。</summary>
        private static double Ema(double previous, double value)
            => previous <= 0 ? value : previous * 0.9 + value * 0.1;

        /// <summary>HSV → RGB（h 取 0~1）：给每个物体一个明显不同的色调（逐物体颜色走逐物体常量）。</summary>
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
            _urp?.Dispose();
            _urp = null;
        }
    }
}
