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
    /// <para>· ① Generic.Log(object…) —— 值类型（int/float/bool）按 object 传递，会装箱、触发 GC。</para>
    /// <para>· ② PrintTool.A（StringBuilder 后端）—— 强类型 Append，无装箱，输出经 StringBuilder.GetChunks() 直写。</para>
    /// <para>· ③ PrintTool.B（Span&lt;char&gt; 后端）—— 强类型 Append（TryFormat 直写字符缓冲），无装箱、无字符串分配。</para>
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

        // 以下三个方法每次循环塞同样的重负载：覆盖各种值类型（int/long/uint/ulong/short/ushort/byte/sbyte/float/double/decimal/bool/char/string）。
        // 唯一变量是它们的传递方式：Generic.Log 按 object 传递会逐项装箱并触发 GC；A/B 走强类型 Append 不装箱。
        // 注：PrintTool.Log 固定最多 9 个 object 参数，故 Generic 基线最多喂 9 个值类型（全部装箱）；A/B 的 Append 无上限，可打出全部类型。
        private static void RunGeneric(int n)
        {
            for (int i = 0; i < n; i++)
            {
                float f = i * 1.234f;
                double d = i * 5.678;
                decimal dec = i * 9.99m;
                // 9 个值类型全部作为 object 传入 → 全部装箱
                PrintTool.Log(
                    (int)i, (long)i, (uint)i, (ulong)i, (short)i,
                    f, d, dec, true);
            }
        }

        private static void RunStringBuilder(int n)
        {
            for (int i = 0; i < n; i++)
            {
                float f = i * 1.234f;
                double d = i * 5.678;
                decimal dec = i * 9.99m;
                string name = "item" + i;
                char c = (char)('A' + i % 26);
                byte b = (byte)(i & 0xFF);
                sbyte sb = (sbyte)(i & 0x7F);
                ushort us = (ushort)(i & 0xFFFF);
                PrintTool.A.BeginLog();
                PrintTool.A.Append("i");   PrintTool.A.Append((int)i);
                PrintTool.A.Append("l");   PrintTool.A.Append((long)i);
                PrintTool.A.Append("u");   PrintTool.A.Append((uint)i);
                PrintTool.A.Append("ul");  PrintTool.A.Append((ulong)i);
                PrintTool.A.Append("s");   PrintTool.A.Append((short)i);
                PrintTool.A.Append("us");  PrintTool.A.Append(us);
                PrintTool.A.Append("b");   PrintTool.A.Append(b);
                PrintTool.A.Append("sb");  PrintTool.A.Append(sb);
                PrintTool.A.Append("f");   PrintTool.A.Append(f);
                PrintTool.A.Append("d");   PrintTool.A.Append(d);
                PrintTool.A.Append("dec"); PrintTool.A.Append(dec);
                PrintTool.A.Append("bo");  PrintTool.A.Append(true);
                PrintTool.A.Append("c");   PrintTool.A.Append(c);
                PrintTool.A.Append("name");PrintTool.A.Append(name);
                PrintTool.A.EndLog();
            }
        }

        private static void RunSpan(int n)
        {
            for (int i = 0; i < n; i++)
            {
                float f = i * 1.234f;
                double d = i * 5.678;
                decimal dec = i * 9.99m;
                string name = "item" + i;
                char c = (char)('A' + i % 26);
                byte b = (byte)(i & 0xFF);
                sbyte sb = (sbyte)(i & 0x7F);
                ushort us = (ushort)(i & 0xFFFF);
                PrintTool.B.BeginLog();
                PrintTool.B.Append("i");   PrintTool.B.Append((int)i);
                PrintTool.B.Append("l");   PrintTool.B.Append((long)i);
                PrintTool.B.Append("u");   PrintTool.B.Append((uint)i);
                PrintTool.B.Append("ul");  PrintTool.B.Append((ulong)i);
                PrintTool.B.Append("s");   PrintTool.B.Append((short)i);
                PrintTool.B.Append("us");  PrintTool.B.Append(us);
                PrintTool.B.Append("b");   PrintTool.B.Append(b);
                PrintTool.B.Append("sb");  PrintTool.B.Append(sb);
                PrintTool.B.Append("f");   PrintTool.B.Append(f);
                PrintTool.B.Append("d");   PrintTool.B.Append(d);
                PrintTool.B.Append("dec"); PrintTool.B.Append(dec);
                PrintTool.B.Append("bo");  PrintTool.B.Append(true);
                PrintTool.B.Append("c");   PrintTool.B.Append(c);
                PrintTool.B.Append("name");PrintTool.B.Append(name);
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
