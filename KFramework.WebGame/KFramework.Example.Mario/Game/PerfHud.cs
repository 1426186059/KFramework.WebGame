using System;
using System.Diagnostics;
using System.Globalization;

using KFramework.MonoGame;

namespace KFramework.Example.Mario
{
    /// <summary>
    /// 帧耗时 HUD —— 马里奥是测试工程，这里放开手脚加统计。
    /// <para>
    /// 存在的直接原因：把 GPU 上传改成零拷贝（Bench_ZeroCopy 实测 4 MB 时省 98.7%）之后，
    /// 帧数<b>几乎没变</b>。要搞清楚钱到底花在哪，就得先量，不能继续猜。
    /// 本类把一帧拆成 Update / Draw 两段，并按 <see cref="GraphicsDevice.Metrics"/> 取
    /// draw call / sprite / 纹理切换数，再按跨界单价折算成"跨界占了多少毫秒"。
    /// </para>
    /// <para>
    /// 统计口径：以<b>一秒</b>为一个窗口，窗口结束才刷新一次显示 ——
    /// 每帧都刷新会让数字乱跳，反而看不清量级。
    /// </para>
    /// </summary>
    public sealed class PerfHud
    {
        /// <summary>
        /// 一次跨 JS 调用的单价（ns）。取自 Bench_ZeroCopy 的实测（空跨界 ≈ 3333 ns）。
        /// 用它把 draw call 数折算成"跨界耗时"，是个<b>估算</b>，故显示时会标注。
        /// </summary>
        private const double NsPerCrossCall = 3333.0;

        /// <summary>每次绘制大约跨两次界：一次顶点上传 + 一次 drawElements。</summary>
        private const int CrossCallsPerDraw = 2;

        /// <summary>HUD 行距（固定值，不依赖字体的 LineSpacing）。</summary>
        private const float LineHeight = 20f;

        private readonly Stopwatch _sw = new();

        private double _updateMs;
        private double _drawMs;

        // ---- 当前统计窗口的累加器 ----
        private double _winMs;
        private int _winFrames;
        private double _winUpdateMs;
        private double _winDrawMs;
        private long _winDrawCalls;
        private long _winSprites;
        private long _winTexSwitches;
        private int _winGcAtStart = GC.CollectionCount(0);

        private string[] _lines = Array.Empty<string>();

        // ---------------------------------------------------------------- 采样

        public void BeginUpdate() => _sw.Restart();

        public void EndUpdate() => _updateMs = _sw.Elapsed.TotalMilliseconds;

        public void BeginDraw() => _sw.Restart();

        /// <summary>
        /// 结束一帧的 Draw 采样并累加进窗口。
        /// <paramref name="metrics"/> 要在 Draw 末尾读：GraphicsMetrics 每帧由 Clear 重置，
        /// 此时读到的是本帧累计值。
        /// </summary>
        public void EndDraw(GraphicsMetrics metrics)
        {
            _drawMs = _sw.Elapsed.TotalMilliseconds;

            _winMs += _updateMs + _drawMs;
            _winFrames++;
            _winUpdateMs += _updateMs;
            _winDrawMs += _drawMs;
            _winDrawCalls += metrics.DrawCount;
            _winSprites += metrics.SpriteCount;
            _winTexSwitches += metrics.TextureCount;

            if (_winMs >= 1000.0) Flush();
        }

        private void Flush()
        {
            int frames = Math.Max(1, _winFrames);
            double avgFrame = _winMs / frames;
            double fps = frames / (_winMs / 1000.0);
            double upd = _winUpdateMs / frames;
            double drw = _winDrawMs / frames;
            double calls = (double)_winDrawCalls / frames;
            double sprites = (double)_winSprites / frames;
            double tex = (double)_winTexSwitches / frames;

            // 跨界折算：只算绘制那一串（顶点上传 + drawElements），状态设置未计入，故是下限
            double crossPerFrame = calls * CrossCallsPerDraw;
            double crossMs = crossPerFrame * NsPerCrossCall / 1_000_000.0;

            int gc = GC.CollectionCount(0) - _winGcAtStart;

            _lines = new[]
            {
                Fmt("FPS", fps, "F1") + "   帧 " + Fmt("", avgFrame, "F2") + " ms",
                "  Update " + Fmt("", upd, "F2") + " ms   Draw " + Fmt("", drw, "F2") + " ms",
                "  draw call " + Fmt("", calls, "F1") + "   sprite " + Fmt("", sprites, "F0") +
                    "   纹理切换 " + Fmt("", tex, "F1"),
                "  绘制跨界(估) " + Fmt("", crossPerFrame, "F0") + " 次/帧 ≈ " + Fmt("", crossMs, "F2") +
                    " ms（占帧 " + Fmt("", avgFrame > 0 ? crossMs / avgFrame * 100.0 : 0, "F1") + " %）",
                "  GC(0代) " + gc + " 次/秒",
            };

            // 开窗重置
            _winMs = 0;
            _winFrames = 0;
            _winUpdateMs = 0;
            _winDrawMs = 0;
            _winDrawCalls = 0;
            _winSprites = 0;
            _winTexSwitches = 0;
            _winGcAtStart = GC.CollectionCount(0);
        }

        // ---------------------------------------------------------------- 绘制

        /// <summary>把统计画到屏幕左上角。用独立的 SpriteBatch，不干扰场景那一个。</summary>
        public void Render(SpriteBatch batch, IFont font, bool visible)
        {
            if (!visible || _lines.Length == 0) return;

            batch.Begin();
            float y = 6f;
            foreach (string line in _lines)
            {
                batch.DrawString(font, line, new Vector2(6f, y), Color.Yellow);
                y += LineHeight;
            }
            batch.End();
        }

        private static string Fmt(string label, double v, string f)
            => label.Length > 0
                ? label + " " + v.ToString(f, CultureInfo.InvariantCulture)
                : v.ToString(f, CultureInfo.InvariantCulture);
    }
}
