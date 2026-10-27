using System.Diagnostics;

namespace KFramework.MonoGame
{
    public static partial class PrintTool
    {
        /// <summary>
        /// 基于复用 StringBuilder 的零装箱日志后端（A）：值类型 Append 直接调用 StringBuilder 重载，避免装箱与 GC。
        /// 所有方法均为 void + 双 Conditional，Release（无 DEBUG 且未定义 PrintLog）下调用点被整体剔除，零开销。
        /// 用法：
        ///   PrintTool.A.BeginLog();
        ///   PrintTool.A.Append("HP="); PrintTool.A.Append(hp);
        ///   PrintTool.A.EndLog();
        /// 输出时通过 StringBuilder.GetChunks() + Console.Out.Write(ReadOnlySpan&lt;char&gt;) 直接写出，避免 ToString() 字符串分配。
        /// </summary>
        public static class A
        {
            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void BeginLog()
            {
                mStringBuilder.Clear();
                mStringBuilder.Append('[');
                Span<char> ts = stackalloc char[32];
                if (DateTime.Now.TryFormat(ts, out int w, "HH:mm:ss.fff".AsSpan(), null))
                    mStringBuilder.Append(ts.Slice(0, w));
                mStringBuilder.Append(']');
            }

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void Append(string value)
            {
                if (value != null) mStringBuilder.Append(value);
                mStringBuilder.Append(ConcatStr.AsSpan());
            }

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void Append(int value)
            {
                mStringBuilder.Append(value);
                mStringBuilder.Append(ConcatStr.AsSpan());
            }

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void Append(long value)
            {
                mStringBuilder.Append(value);
                mStringBuilder.Append(ConcatStr.AsSpan());
            }

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void Append(uint value)
            {
                mStringBuilder.Append(value);
                mStringBuilder.Append(ConcatStr.AsSpan());
            }

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void Append(ulong value)
            {
                mStringBuilder.Append(value);
                mStringBuilder.Append(ConcatStr.AsSpan());
            }

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void Append(short value)
            {
                mStringBuilder.Append(value);
                mStringBuilder.Append(ConcatStr.AsSpan());
            }

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void Append(ushort value)
            {
                mStringBuilder.Append(value);
                mStringBuilder.Append(ConcatStr.AsSpan());
            }

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void Append(byte value)
            {
                mStringBuilder.Append(value);
                mStringBuilder.Append(ConcatStr.AsSpan());
            }

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void Append(sbyte value)
            {
                mStringBuilder.Append(value);
                mStringBuilder.Append(ConcatStr.AsSpan());
            }

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void Append(float value)
            {
                mStringBuilder.Append(value);
                mStringBuilder.Append(ConcatStr.AsSpan());
            }

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void Append(double value)
            {
                mStringBuilder.Append(value);
                mStringBuilder.Append(ConcatStr.AsSpan());
            }

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void Append(decimal value)
            {
                mStringBuilder.Append(value);
                mStringBuilder.Append(ConcatStr.AsSpan());
            }

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void Append(bool value)
            {
                mStringBuilder.Append(value);
                mStringBuilder.Append(ConcatStr.AsSpan());
            }

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void Append(char value)
            {
                mStringBuilder.Append(value);
                mStringBuilder.Append(ConcatStr.AsSpan());
            }

            // 兜底：引用类型或没有对应重载的值类型（会装箱，仅必要时使用）
            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void Append(object value)
            {
                if (value != null) mStringBuilder.Append(value);
                mStringBuilder.Append(ConcatStr.AsSpan());
            }

            // KV 形式（key=value）：key/value 须为 ISpanFormattable（所有数值/char/bool/DateTime/Guid 等均满足，string 不满足请用 Append）。
            // 走 TryFormat 直写 stackalloc 缓冲，常见情况零分配；仅超长（如极长 decimal）才回退 ToString()。
            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void AppendKV<K, V>(K key, V value)
                where K : ISpanFormattable
                where V : ISpanFormattable
            {
                AppendFormatted(key);
                mStringBuilder.Append(KVConcatStr.AsSpan());
                AppendFormatted(value);
                mStringBuilder.Append(ConcatStr.AsSpan());
            }

            // 字符串键重载：key 为 string（真实日志里键几乎都是字符串），value 仍须 ISpanFormattable。
            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void AppendKV<V>(string key, V value)
                where V : ISpanFormattable
            {
                mStringBuilder.Append(key);
                mStringBuilder.Append(KVConcatStr.AsSpan());
                AppendFormatted(value);
                mStringBuilder.Append(ConcatStr.AsSpan());
            }

            private static void AppendFormatted<T>(T value)
                where T : ISpanFormattable
            {
                Span<char> buffer = stackalloc char[128];
                if (value.TryFormat(buffer, out int written, default, null))
                    mStringBuilder.Append(buffer[..written]);
                else
                    mStringBuilder.Append(value.ToString());   // 极端超长才分配，极少走到
            }

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void EndLog()
            {
                foreach (var chunk in mStringBuilder.GetChunks()) Console.Out.Write(chunk.Span);
                Console.Out.Write('\n');
                mStringBuilder.Clear();
            }

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void EndLogError()
            {
                foreach (var chunk in mStringBuilder.GetChunks()) Console.Error.Write(chunk.Span);
                Console.Error.Write('\n');
                mStringBuilder.Clear();
            }

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void EndLogWarning()
            {
                Console.Out.Write("<color=orange>".AsSpan());
                foreach (var chunk in mStringBuilder.GetChunks()) Console.Out.Write(chunk.Span);
                Console.Out.Write("</color>\n".AsSpan());
                mStringBuilder.Clear();
            }

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void EndLogWithColor()
            {
                Console.Out.Write("<color=yellow>".AsSpan());
                foreach (var chunk in mStringBuilder.GetChunks()) Console.Out.Write(chunk.Span);
                Console.Out.Write("</color>\n".AsSpan());
                mStringBuilder.Clear();
            }
        }
    }
}
