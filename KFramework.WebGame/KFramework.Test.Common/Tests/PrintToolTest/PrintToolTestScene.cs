using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.Common.Tests.PrintToolTest
{

    /// <summary>
    /// PrintTool 三种打印方式性能对比：
    /// <para>· ① Generic.Log(object…) —— 各种值类型（int/long/uint/ulong/short/ushort/byte/sbyte/float/double/decimal/bool/char）按 object 传递，会装箱、触发 GC。</para>
    /// <para>· ② PrintTool.A（StringBuilder 后端）—— 强类型 Append，无装箱，输出经 StringBuilder.GetChunks() 直写。</para>
    /// <para>· ③ PrintTool.B（Span&lt;char&gt; 后端）—— 强类型 Append（TryFormat 直写字符缓冲），无装箱、无字符串分配。</para>
    /// <para>· ④⑤ A-KV / B-KV —— 在 ②/③ 的基础上改用 AppendKV(string 键, 强类型值) 写法（key=value），对比 Append 写法本身的耗时/分配差异（数据完全相同）。</para>
    /// <para>各方式打印几千条，统计 用时（Stopwatch）与 GC0 次数（GC.CollectionCount）。</para>
    /// <para>注意：PrintTool.A/B 的方法带 [Conditional]，Release（无 DEBUG 且未定义 PrintLog）下调用点被整体剔除；请用 Debug 构建运行本页。</para>
    /// </summary>
    public sealed class PrintToolTestScene : TestSceneBase
    {
        private static readonly int[] CountOptions = [2000, 10000, 50000, 100000];
        private int _countIndex = 1; // 默认 10000

        private readonly List<string> _results = new();
        private string _lastError;

        private bool _buildOnly;                                   // 仅构建（不打印）模式：替换 Console.Out 为丢弃写入器
        private static readonly TextWriter Discard = new DiscardWriter();

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
            AddUiButton(_buildOnly ? "模式: 仅构建(不打印)" : "模式: 打印", () => _buildOnly = !_buildOnly);
            AddUiButton("测试 Generic.Log", () => RunBenchmark("① Generic.Log", RunGeneric));
            AddUiButton("测试 A(StringBuilder)", () => RunBenchmark("② StringBuilder", RunStringBuilder));
            AddUiButton("测试 B(Span)", () => RunBenchmark("③ Span<char>", RunSpan));
            AddUiButton("测试 A-KV(StringBuilder)", () => RunBenchmark("④ A-KV", RunStringBuilderKV));
            AddUiButton("测试 B-KV(Span)", () => RunBenchmark("⑤ B-KV", RunSpanKV));
            AddUiButton("全部测试", () =>
            {
                RunBenchmark("① Generic.Log", RunGeneric);
                RunBenchmark("② StringBuilder", RunStringBuilder);
                RunBenchmark("③ Span<char>", RunSpan);
                RunBenchmark("④ A-KV", RunStringBuilderKV);
                RunBenchmark("⑤ B-KV", RunSpanKV);
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
                if (_buildOnly)
                {
                    // 仅构建：把 Console.Out 换成丢弃写入器，去掉 console.log 这个 interop 耗时大头；
                    // 三种方式仍走真实 .EndLog() 代码路径，从而暴露纯构建成本（含 Generic 的装箱）。
                    TextWriter old = Console.Out;
                    Console.SetOut(Discard);
                    try { action(n); }
                    finally { Console.SetOut(old); }
                }
                else
                {
                    action(n);
                }
                sw.Stop();
                long gcAfter = GC.CollectionCount(0);

                string tag = _buildOnly ? "构建" : "打印";
                _results.Insert(0, $"{name,-16} [{tag}] n={n,-6:N0} 用时 {sw.Elapsed.TotalMilliseconds,9:F1} ms  GC0={gcAfter - gcBefore}");
                if (_results.Count > 12) _results.RemoveAt(_results.Count - 1);
            }
            catch (Exception ex)
            {
                _lastError = $"{name} 异常: {ex.Message}";
            }
        }

        // 三个方法使用完全相同的 9 个数据（int/long/uint/ulong/float/double/decimal/bool/string），保证对比绝对公平；
        // 每个值都被放大/加长（大整数、长小数、80 字符长字符串）以加重 ToString 与装箱代价，使差距更明显。
        // 唯一变量是传递方式：Generic.Log 按 object 逐项装箱并对每个值 ToString() 分配字符串；A/B 走强类型 Append（TryFormat 直写），零装箱、零字符串分配。
        private static void RunGeneric(int n)
        {
            for (int i = 0; i < n; i++)
            {
                int vInt = i * 7919 + 13;
                long vLong = i * 1000000000L + 123456789L;
                uint vUInt = (uint)(i * 104729u + 7u);
                ulong vULong = (ulong)i * 1000000000000UL + 999UL;
                float vFloat = i * 1.2345678f;
                double vDouble = i * 5.678901234;
                decimal vDecimal = i * 123456.7890123456m;
                bool vBool = (i & 1) == 0;
                string vStr = "item_" + i.ToString("D8") + "_" + new string('*', 80);
                // 9 个数据全部作为 object 传入 → 全部装箱，且每个值都会 ToString() 分配字符串
                PrintTool.Log(vInt, vLong, vUInt, vULong, vFloat, vDouble, vDecimal, vBool, vStr);
            }
        }

        private static void RunStringBuilder(int n)
        {
            for (int i = 0; i < n; i++)
            {
                int vInt = i * 7919 + 13;
                long vLong = i * 1000000000L + 123456789L;
                uint vUInt = (uint)(i * 104729u + 7u);
                ulong vULong = (ulong)i * 1000000000000UL + 999UL;
                float vFloat = i * 1.2345678f;
                double vDouble = i * 5.678901234;
                decimal vDecimal = i * 123456.7890123456m;
                bool vBool = (i & 1) == 0;
                string vStr = "item_" + i.ToString("D8") + "_" + new string('*', 80);
                PrintTool.A.BeginLog();
                PrintTool.A.Append("vInt");    PrintTool.A.Append(vInt);
                PrintTool.A.Append("vLong");   PrintTool.A.Append(vLong);
                PrintTool.A.Append("vUInt");   PrintTool.A.Append(vUInt);
                PrintTool.A.Append("vULong");  PrintTool.A.Append(vULong);
                PrintTool.A.Append("vFloat");  PrintTool.A.Append(vFloat);
                PrintTool.A.Append("vDouble"); PrintTool.A.Append(vDouble);
                PrintTool.A.Append("vDecimal");PrintTool.A.Append(vDecimal);
                PrintTool.A.Append("vBool");   PrintTool.A.Append(vBool);
                PrintTool.A.Append("vStr");    PrintTool.A.Append(vStr);
                PrintTool.A.EndLog();
            }
        }

        private static void RunSpan(int n)
        {
            for (int i = 0; i < n; i++)
            {
                int vInt = i * 7919 + 13;
                long vLong = i * 1000000000L + 123456789L;
                uint vUInt = (uint)(i * 104729u + 7u);
                ulong vULong = (ulong)i * 1000000000000UL + 999UL;
                float vFloat = i * 1.2345678f;
                double vDouble = i * 5.678901234;
                decimal vDecimal = i * 123456.7890123456m;
                bool vBool = (i & 1) == 0;
                string vStr = "item_" + i.ToString("D8") + "_" + new string('*', 80);
                PrintTool.B.BeginLog();
                PrintTool.B.Append("vInt");    PrintTool.B.Append(vInt);
                PrintTool.B.Append("vLong");   PrintTool.B.Append(vLong);
                PrintTool.B.Append("vUInt");   PrintTool.B.Append(vUInt);
                PrintTool.B.Append("vULong");  PrintTool.B.Append(vULong);
                PrintTool.B.Append("vFloat");  PrintTool.B.Append(vFloat);
                PrintTool.B.Append("vDouble"); PrintTool.B.Append(vDouble);
                PrintTool.B.Append("vDecimal");PrintTool.B.Append(vDecimal);
                PrintTool.B.Append("vBool");   PrintTool.B.Append(vBool);
                PrintTool.B.Append("vStr");    PrintTool.B.Append(vStr);
                PrintTool.B.EndLog();
            }
        }

        // 与 RunStringBuilder / RunSpan 使用完全相同的 9 个数据，仅写法换成 AppendKV（string 键 + 强类型值）；
        // 字符串值 vStr 因 string 非 ISpanFormattable，仍走 Append，确保输出与上面两组完全一致。
        private static void RunStringBuilderKV(int n)
        {
            for (int i = 0; i < n; i++)
            {
                int vInt = i * 7919 + 13;
                long vLong = i * 1000000000L + 123456789L;
                uint vUInt = (uint)(i * 104729u + 7u);
                ulong vULong = (ulong)i * 1000000000000UL + 999UL;
                float vFloat = i * 1.2345678f;
                double vDouble = i * 5.678901234;
                decimal vDecimal = i * 123456.7890123456m;
                bool vBool = (i & 1) == 0;
                string vStr = "item_" + i.ToString("D8") + "_" + new string('*', 80);
                PrintTool.A.BeginLog();
                PrintTool.A.AppendKV("vInt", vInt);
                PrintTool.A.AppendKV("vLong", vLong);
                PrintTool.A.AppendKV("vUInt", vUInt);
                PrintTool.A.AppendKV("vULong", vULong);
                PrintTool.A.AppendKV("vFloat", vFloat);
                PrintTool.A.AppendKV("vDouble", vDouble);
                PrintTool.A.AppendKV("vDecimal", vDecimal);
                PrintTool.A.AppendKV("vBool", vBool);
                PrintTool.A.Append("vStr"); PrintTool.A.Append(vStr);
                PrintTool.A.EndLog();
            }
        }

        private static void RunSpanKV(int n)
        {
            for (int i = 0; i < n; i++)
            {
                int vInt = i * 7919 + 13;
                long vLong = i * 1000000000L + 123456789L;
                uint vUInt = (uint)(i * 104729u + 7u);
                ulong vULong = (ulong)i * 1000000000000UL + 999UL;
                float vFloat = i * 1.2345678f;
                double vDouble = i * 5.678901234;
                decimal vDecimal = i * 123456.7890123456m;
                bool vBool = (i & 1) == 0;
                string vStr = "item_" + i.ToString("D8") + "_" + new string('*', 80);
                PrintTool.B.BeginLog();
                PrintTool.B.AppendKV("vInt", vInt);
                PrintTool.B.AppendKV("vLong", vLong);
                PrintTool.B.AppendKV("vUInt", vUInt);
                PrintTool.B.AppendKV("vULong", vULong);
                PrintTool.B.AppendKV("vFloat", vFloat);
                PrintTool.B.AppendKV("vDouble", vDouble);
                PrintTool.B.AppendKV("vDecimal", vDecimal);
                PrintTool.B.AppendKV("vBool", vBool);
                PrintTool.B.Append("vStr"); PrintTool.B.Append(vStr);
                PrintTool.B.EndLog();
            }
        }

        protected override void DrawBody(SpriteBatch batch, Vector2 origin)
        {
            // 按钮已在 Update 登记，这里（Begin/End 之间）绘制
            DrawButtons(batch);

            float y = ContentTop + 6f;
            y += DrawSection(batch, _buildOnly
                ? "结果【仅构建·不打印】：去掉 console.log 大头，看纯构建耗时；GC0 反映装箱代价"
                : "结果【打印】：用时含真实控制台打印；GC0 即 Gen0 回收次数，最能反映装箱代价", new Vector2(origin.X, y));

            if (_results.Count == 0)
                y += DrawLine(batch, Font,
                    "点上方按钮开始：Generic.Log 会对 int/long/uint/ulong/float/double/decimal/bool 等所有传入的值类型装箱并触发 GC，A/B 强类型 Append 不会。",
                    new Vector2(origin.X, y), Color.LightGray);
            for (int i = 0; i < _results.Count; i++)
            {
                y += DrawLine(batch, Font, _results[i], new Vector2(origin.X, y),
                    i == 0 ? new Color(126, 230, 160) : new Color(210, 220, 240));
            }

            if (_lastError != null)
                y += DrawLine(batch, Font, _lastError, new Vector2(origin.X, y), new Color(255, 150, 150));

            y += DrawLine(batch, Font,
                _buildOnly
                    ? "当前：仅构建模式（Console.Out 已替换为丢弃写入器，无控制台输出）"
                    : "当前：打印模式（真实 console.log；点「模式」按钮切到仅构建）",
                new Vector2(origin.X, y), new Color(150, 165, 190));
            y += DrawLine(batch, Font,
                $"FPS {Fps:F1}（真实帧率）    Esc 返回",
                new Vector2(origin.X, y), new Color(150, 165, 190));
        }

        /// <summary>丢弃一切写入的 TextWriter：仅构建模式下替换 Console.Out，去掉 console.log 这个耗时大头，暴露纯构建成本。</summary>
        private sealed class DiscardWriter : TextWriter
        {
            public override Encoding Encoding => Encoding.UTF8;
            public override void Write(char value) { }
            public override void Write(char[] buffer, int index, int count) { }
            public override void Write(string value) { }
            public override void Write(ReadOnlySpan<char> buffer) { }
            public override void WriteLine() { }
            public override void WriteLine(string value) { }
            public override void WriteLine(ReadOnlySpan<char> buffer) { }
            public override void Flush() { }
        }

    }

}
