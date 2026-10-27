using System;
using System.Text;

namespace KFramework.MonoGame
{
    public static partial class PrintTool
    {
        /// <summary>
        /// 开启一次"零装箱"的日志拼接会话（基于复用的 StringBuilder）：按原格式前缀时间戳。
        /// 之后链式调用强类型 Append（int/float/... 不会装箱），末尾用 Log()/LogError()/LogWarning()/LogWithColor() 输出。
        /// 例：PrintTool.BeginStringBuilder().Append("HP=").Append(hp).Append(" MP=").Append(mp).Log();
        /// 非 DEBUG 下返回空会话，Append/Log 均为 no-op（不分配、不输出）。
        /// 输出时通过 StringBuilder.GetChunks() + Console.Out.Write(ReadOnlySpan&lt;char&gt;) 直接写出，避免 ToString() 字符串分配；
        /// 仅时间戳（一次 DateTime 装箱）与兜底 object Append 仍有极小分配。若需完全零分配请用 BeginSpan()（Span&lt;char&gt; 版）。
        /// </summary>
        public static PrintStringBuilder BeginStringBuilder()
        {
#if DEBUG
            mStringBuilder.Clear();
            mStringBuilder.Append('[');
            Span<char> ts = stackalloc char[32];
            if (DateTime.Now.TryFormat(ts, out int w, "HH:mm:ss.fff".AsSpan(), null))
                mStringBuilder.Append(ts.Slice(0, w));
            mStringBuilder.Append(']');
            return new PrintStringBuilder(mStringBuilder);
#else
            return default;
#endif
        }


        /// <summary>
        /// 基于 StringBuilder 的零装箱日志拼接器。强类型 Append 直接调用 StringBuilder 对应重载，避免值类型装箱与 GC。
        /// 字符串/对象等引用类型本就不装箱；未提供重载的值类型会走 object 兜底（仍会装箱，仅在必要时使用）。
        /// </summary>
        public ref struct PrintStringBuilder
        {
            private readonly StringBuilder _sb;

            internal PrintStringBuilder(StringBuilder sb) => _sb = sb;

            public PrintStringBuilder Append(string value)
            {
                if (_sb != null)
                {
                    _sb.Append(value);
                    _sb.Append(PrintTool.ConcatStr.AsSpan());
                }
                return this;
            }

            public PrintStringBuilder Append(int value) { if (_sb != null) { _sb.Append(value); _sb.Append(PrintTool.ConcatStr.AsSpan()); } return this; }
            public PrintStringBuilder Append(long value) { if (_sb != null) { _sb.Append(value); _sb.Append(PrintTool.ConcatStr.AsSpan()); } return this; }
            public PrintStringBuilder Append(uint value) { if (_sb != null) { _sb.Append(value); _sb.Append(PrintTool.ConcatStr.AsSpan()); } return this; }
            public PrintStringBuilder Append(ulong value) { if (_sb != null) { _sb.Append(value); _sb.Append(PrintTool.ConcatStr.AsSpan()); } return this; }
            public PrintStringBuilder Append(short value) { if (_sb != null) { _sb.Append(value); _sb.Append(PrintTool.ConcatStr.AsSpan()); } return this; }
            public PrintStringBuilder Append(ushort value) { if (_sb != null) { _sb.Append(value); _sb.Append(PrintTool.ConcatStr.AsSpan()); } return this; }
            public PrintStringBuilder Append(byte value) { if (_sb != null) { _sb.Append(value); _sb.Append(PrintTool.ConcatStr.AsSpan()); } return this; }
            public PrintStringBuilder Append(sbyte value) { if (_sb != null) { _sb.Append(value); _sb.Append(PrintTool.ConcatStr.AsSpan()); } return this; }
            public PrintStringBuilder Append(float value) { if (_sb != null) { _sb.Append(value); _sb.Append(PrintTool.ConcatStr.AsSpan()); } return this; }
            public PrintStringBuilder Append(double value) { if (_sb != null) { _sb.Append(value); _sb.Append(PrintTool.ConcatStr.AsSpan()); } return this; }
            public PrintStringBuilder Append(decimal value) { if (_sb != null) { _sb.Append(value); _sb.Append(PrintTool.ConcatStr.AsSpan()); } return this; }
            public PrintStringBuilder Append(bool value) { if (_sb != null) { _sb.Append(value); _sb.Append(PrintTool.ConcatStr.AsSpan()); } return this; }
            public PrintStringBuilder Append(char value) { if (_sb != null) { _sb.Append(value); _sb.Append(PrintTool.ConcatStr.AsSpan()); } return this; }

            // 兜底：引用类型或没有对应重载的值类型（会装箱，仅必要时使用）
            public PrintStringBuilder Append(object value)
            {
                if (_sb != null && value != null)
                {
                    _sb.Append(value);
                    _sb.Append(PrintTool.ConcatStr.AsSpan());
                }
                return this;
            }

            public void Log()
            {
#if DEBUG
                if (_sb != null)
                {
                    // 直接按块写出，避免 _sb.ToString() 的字符串分配
                    foreach (var chunk in _sb.GetChunks()) Console.Out.Write(chunk.Span);
                    Console.Out.Write('\n');
                }
#endif
            }

            public void LogError()
            {
#if DEBUG
                if (_sb != null)
                {
                    foreach (var chunk in _sb.GetChunks()) Console.Error.Write(chunk.Span);
                    Console.Error.Write('\n');
                }
#endif
            }

            public void LogWarning()
            {
#if DEBUG
                if (_sb != null)
                {
                    Console.Out.Write("<color=orange>".AsSpan());
                    foreach (var chunk in _sb.GetChunks()) Console.Out.Write(chunk.Span);
                    Console.Out.Write("</color>\n".AsSpan());
                }
#endif
            }

            public void LogWithColor()
            {
#if DEBUG
                if (_sb != null)
                {
                    Console.Out.Write("<color=yellow>".AsSpan());
                    foreach (var chunk in _sb.GetChunks()) Console.Out.Write(chunk.Span);
                    Console.Out.Write("</color>\n".AsSpan());
                }
#endif
            }

            public override string ToString() => _sb != null ? _sb.ToString() : string.Empty;
        }
    }
}
