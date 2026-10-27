using System;
using System.Collections.Generic;
using System.Diagnostics;

using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.Common.Tests.PrintToolTest
{

    /// <summary>
    /// PrintTool 三种打印方式性能对比：
    /// <para>· ① Generic.Log(object…) —— 值类型（int/float/bool）按 object 传递，会装箱、触发 GC。</para>
    /// <para>· ② BeginStringBuilder() —— 强类型 Append，无装箱，输出经 StringBuilder.GetChunks() 直写。</para>
    /// <para>· ③ BeginSpan() —— Span&lt;char&gt; 强类型 Append（TryFormat 直写字符缓冲），无装箱、无字符串分配。</para>
    /// <para>各方式打印几千条，统计 用时（Stopwatch）与 GC0 次数（GC.CollectionCount）。</para>
    /// <para>注意：PrintTool.* 的输出在 #if DEBUG 内，请用 Debug 构建运行本页，否则不打印、计时近乎 0。</para>
    /// </summary>
    public sealed class PrintToolTestScene : TestSceneBase
    {
        private static readonly int[] CountOptions = [1000, 3000, 5000, 10000];
        private int _countIndex = 2; // 默认 5000

        private readonly List<string> _results = new();
        private string _lastError;

        public override string Title => "PrintTool 打印性能对比（装箱 / GC / 用时）";

        public override void Update()
        {
            // 基类处理 Esc / 返回；一旦切走就不再碰本场景状态
            base.Update();
            if (!ReferenceEquals(KSceneMgr.Main, this)) return;

            LayoutButtons();
            ClickButtons();
        }

        private void LayoutButtons()
        {
            BeginButtons(70);

            AddUiButton($"数量 {CountOptions[_countIndex]:N0}", () => _countIndex = (_countIndex + 1) % CountOptions.Length);
            AddUiButton("测试 Generic.Log", () => RunBenchmark("① Generic.Log", RunGeneric));
            AddUiButton("测试 BeginStringBuilder", () => RunBenchmark("② StringBuilder", RunStringBuilder));
            AddUiButton("测试 BeginSpan", () => RunBenchmark("③ Span<char>", RunSpan));
            AddUiButton("全部测试", () =>
            {
                RunBenchmark("① Generic.Log", RunGeneric);
                RunBenchmark("② StringBuilder", RunStringBuilder);
                RunBenchmark("③ Span<char>", RunSpan);
            });
            AddUiButton("清空结果", () => { _results.Clear(); _lastError = null; });
        }

        private void RunBenchmark(string name, Action<int> action)
        {
            int n = CountOptions[_countIndex];
            try
            {
                long gcBefore = GC.CollectionCount(0);
                var sw = Stopwatch.StartNew();
                action(n);
                sw.Stop();
                long gcAfter = GC.CollectionCount(0);

                _results.Insert(0, $"{name,-16} n={n,-6:N0} 用时 {sw.Elapsed.TotalMilliseconds,9:F1} ms  GC0={gcAfter - gcBefore}");
                if (_results.Count > 12) _results.RemoveAt(_results.Count - 1);
            }
            catch (Exception ex)
            {
                _lastError = $"{name} 异常: {ex.Message}";
            }
        }

        // 以下三个方法给每次循环塞同样的负载：一个 int、一个 float、一个 string、一个 bool，
        // 唯一变量是 int/float/bool 的传递方式（装箱 vs 不装箱）。
        private static void RunGeneric(int n)
        {
            for (int i = 0; i < n; i++)
            {
                float f = i * 1.234f;
                string name = "item" + i;
                PrintTool.Log("idx", i, "ratio", f, "name", name, "ok", true);
            }
        }

        private static void RunStringBuilder(int n)
        {
            for (int i = 0; i < n; i++)
            {
                float f = i * 1.234f;
                string name = "item" + i;
                PrintTool.BeginStringBuilder()
                    .Append("idx").Append(i)
                    .Append("ratio").Append(f)
                    .Append("name").Append(name)
                    .Append("ok").Append(true)
                    .Log();
            }
        }

        private static void RunSpan(int n)
        {
            for (int i = 0; i < n; i++)
            {
                float f = i * 1.234f;
                string name = "item" + i;
                PrintTool.BeginSpan()
                    .Append("idx").Append(i)
                    .Append("ratio").Append(f)
                    .Append("name").Append(name)
                    .Append("ok").Append(true)
                    .Log();
            }
        }

        protected override void DrawBody(SpriteBatch batch, Vector2 origin)
        {
            // 按钮已在 Update 登记，这里（Begin/End 之间）绘制
            DrawButtons(batch);

            float y = ContentTop + 6f;
            y += DrawSection(batch, "结果（用时含真实控制台打印；GC0 即 Gen0 回收次数，最能反映装箱代价）", new Vector2(origin.X, y));

            if (_results.Count == 0)
                y += DrawLine(batch, Font,
                    "点上方按钮开始：Generic.Log 会对 int/float/bool 装箱并触发 GC，另两者不会。",
                    new Vector2(origin.X, y), Color.LightGray);
            for (int i = 0; i < _results.Count; i++)
            {
                y += DrawLine(batch, Font, _results[i], new Vector2(origin.X, y),
                    i == 0 ? new Color(126, 230, 160) : new Color(210, 220, 240));
            }

            if (_lastError != null)
                y += DrawLine(batch, Font, _lastError, new Vector2(origin.X, y), new Color(255, 150, 150));

            y += DrawLine(batch, Font,
                $"FPS {Fps:F1}（真实帧率）    Esc 返回",
                new Vector2(origin.X, y), new Color(150, 165, 190));
        }
    }

}
