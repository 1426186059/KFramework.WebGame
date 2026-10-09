using System;
using System.Diagnostics;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.WebGL20.Tests
{
    /// <summary>
    /// <see cref="SpriteBatch"/>（CPU 合批）的规模测试：一次 Begin → N 次 Draw → End，
    /// N 由空格键在 <b>1000 / 5000 / 9000</b> 之间切换（同一张纹理、逐精灵旋转与色调）。
    /// <para>
    /// 这里看的是 CPU 合批的代价曲线：每个精灵的 4 个顶点 × 28 字节都要在 C# 侧算好、再整段上传；
    /// 单次 draw 上限 <see cref="GraphicsDevice.MaxBatchSize"/> = 4096 个四边形（索引是 short），
    /// 所以<b>同一张纹理也绕不过这个上限</b>：1000 → 1 次 DC、5000 → 2 次、9000 → 3 次。
    /// </para>
    /// <para>
    /// 与第 7 页（GPU 实例化）对照着看：同样的精灵数，那条路每精灵只要 84 字节、顶点只有一份单位四边形，
    /// 但逐精灵的 uniform / 任意材质状态就用不了了；这条路（CPU 合批）的优势正在后者，且没有后端能力门槛。
    /// </para>
    /// <para>交互：<b>空格</b>切换精灵数（1000 / 5000 / 9000）。</para>
    /// </summary>
    public sealed class SpriteBatchPerfScene : DemoScene
    {
        public override string Title => "10) SpriteBatch 大批量：1000 / 5000 / 9000 精灵";

        protected override string Description
            => "一次 Begin → N 次 Draw → End：同纹理全部合批；单次 draw 上限 4096 个四边形，超出自动分批";

        /// <summary>可切换的精灵数档位（空格键循环）。</summary>
        private static readonly int[] CountPresets = [1000, 5000, 9000];

        /// <summary>精灵网格的左上角（正文起点：让开标题与描述）。</summary>
        private const float GridLeft = 28f;
        private const float GridTop = 116f;

        private readonly Stopwatch _sw = Stopwatch.StartNew();

        private Texture2D? _tex;
        private float _time;
        private int _presetIndex;

        private long _lastDrawCalls = -1;
        private int _lastSprites;

        /// <summary>一次 Begin → N×Draw → End 的总耗时（毫秒，EMA 平滑）。</summary>
        private double _submitMs;

        /// <summary>提交里"纯 C# 组数据"那半段（Begin + N×Draw：算 4 个顶点 + 排队）的耗时，EMA 平滑。</summary>
        private double _drawMs;

        /// <summary>提交里"GL 提交"那半段（End：上传顶点缓冲 + 分批 drawElements）的耗时，EMA 平滑。</summary>
        private double _endMs;

        /// <summary>帧间隔（毫秒，EMA 平滑）≈ 整帧耗时，用来分辨掉帧到底在 CPU 还是 GPU。</summary>
        private double _frameMs;
        private long _lastFrameTicks;

        public override void Update()
        {
            _time = (float)_sw.Elapsed.TotalSeconds;

            if (Input_KeyBoard.GetKeyDown(Keys.Space))
                _presetIndex = (_presetIndex + 1) % CountPresets.Length;
        }

        public override void Draw()
        {
            SpriteBatch batch = Batch;

            // 文字层：标题 / 提示 / 页脚（读数在提交之后另起一批画，否则会被算进计时）。
            batch.Begin();
            DrawHeader(batch);
            DrawLine(batch, "空格键切换精灵数（1000 / 5000 / 9000）", 28f, 80f, new Color(150, 165, 195));
            DrawFooter(batch);
            batch.End();

            _tex ??= MakeChecker(32, Color.White, new Color(36, 46, 66));

            int count = CountPresets[_presetIndex];

            // 帧间隔：Draw 每帧调一次，两次之间的间隔 ≈ 整帧耗时（含 C# 逻辑、渲染与浏览器合成）。
            long now = Stopwatch.GetTimestamp();
            if (_lastFrameTicks != 0)
                _frameMs = Ema(_frameMs, (now - _lastFrameTicks) * 1000.0 / Stopwatch.Frequency);
            _lastFrameTicks = now;

            // ---- 关键：N 个精灵一批提交 ----
            long before = Device.Metrics.DrawCount;
            long t0 = Stopwatch.GetTimestamp();
            Submit(count, out double drawMs);
            double submit = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;

            _lastDrawCalls = Device.Metrics.DrawCount - before;
            _lastSprites = count;
            _drawMs = Ema(_drawMs, drawMs);
            _endMs = Ema(_endMs, submit - drawMs);
            _submitMs = Ema(_submitMs, submit);

            // ---- 读数 ----
            batch.Begin();

            float readoutY = Device.Viewport.Height - 152f;
            DrawLine(batch, $"精灵数 = {_lastSprites} → DrawCall 增量 = {_lastDrawCalls}（单次上限 {GraphicsDevice.MaxBatchSize}，超出自动分批）",
                28f, readoutY, new Color(120, 200, 160));
            DrawLine(batch, $"CPU 侧带宽：逐顶点 4×28 = 112 字节/精灵（本页 = {_lastSprites * 112 / 1024} KB）   对照第 7 页 GPU 实例化 84 字节/精灵（= {_lastSprites * 84 / 1024} KB）",
                28f, readoutY + 20f, new Color(255, 206, 110));
            DrawLine(batch, "SpriteBatch：Begin → Draw × N → End；旋转 / 缩放 / 逐精灵色调都在 C# 侧算进 4 个顶点，逐批一次 drawElements",
                28f, readoutY + 40f, new Color(150, 165, 195));
            DrawLine(batch, $"提交拆分：组数据(Begin+Draw×N) = {_drawMs:F2} ms　GL 提交(End) = {_endMs:F2} ms　合计 {_submitMs:F2} ms　帧间隔 = {_frameMs:F1} ms（≈ {(_frameMs > 0 ? 1000.0 / _frameMs : 0):F0} FPS）",
                28f, readoutY + 60f, new Color(180, 220, 255));

            batch.End();
        }

        /// <summary>
        /// 显式 Begin / Draw × N / End，顺手把两段耗时带出来 —— 分辨"哪一侧慢"的关键数据：
        /// <list type="bullet">
        ///   <item><description><paramref name="drawMs"/>：纯 C# 组数据（算四边形顶点 + 排队），不碰 GL。</description></item>
        ///   <item><description>总耗时减去它就是 GL 提交（上传顶点缓冲 + 分批 <c>drawElements</c>）。这一侧变慢通常意味着
        ///   GPU / 驱动队列积压（WebGL 调用会把 CPU 阻塞到队列有位置为止），而不是 C# 慢。</description></item>
        /// </list>
        /// </summary>
        private void Submit(int count, out double drawMs)
        {
            ComputeLayout(count, out int cols, out float cellW, out float cellH, out float size);

            SpriteBatch batch = Batch;
            Texture2D tex = _tex!;

            long t0 = Stopwatch.GetTimestamp();

            batch.Begin();
            for (int i = 0; i < count; i++)
            {
                int c = i % cols;
                int r = i / cols;

                var center = new Vector2(GridLeft + (c + 0.5f) * cellW, GridTop + (r + 0.5f) * cellH);
                float rotation = _time * 0.6f + (r + c) * 0.05f;

                // 逐精灵色调 / 旋转都随顶点数据走（不参与分批键），所以整批只按 4096 上限分批。
                batch.DrawCentered(tex, center, Hue(i * 0.013f), rotation, size / tex.Width);
            }

            long t1 = Stopwatch.GetTimestamp();
            batch.End();     // End 里做：上传顶点缓冲 + 分批 drawElements

            drawMs = (t1 - t0) * 1000.0 / Stopwatch.Frequency;
        }

        /// <summary>指数滑动平均（0.9/0.1），让读数不被单帧抖动带偏。</summary>
        private static double Ema(double previous, double value)
            => previous <= 0 ? value : previous * 0.9 + value * 0.1;

        /// <summary>方阵布局（底部留 168px 给读数）。</summary>
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

        /// <summary>HSV → RGB（h 取 0~1），给每个精灵一个明显不同的色调。</summary>
        private static Color Hue(float h)
        {
            h -= MathF.Floor(h);
            float i = MathF.Floor(h * 6f);
            float f = h * 6f - i;

            const float s = 0.75f;
            float p = 1f - s;
            float q = 1f - f * s;
            float t = 1f - (1f - f) * s;

            return ((int)i % 6) switch
            {
                0 => new Color(1f, t, p),
                1 => new Color(q, 1f, p),
                2 => new Color(p, 1f, t),
                3 => new Color(p, q, 1f),
                4 => new Color(t, p, 1f),
                _ => new Color(1f, p, q),
            };
        }
    }
}
