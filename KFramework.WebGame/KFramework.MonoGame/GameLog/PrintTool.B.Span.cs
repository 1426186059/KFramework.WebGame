using System.Diagnostics;

namespace KFramework.MonoGame
{
    public static partial class PrintTool
    {
        /// <summary>
        /// 基于 Span&lt;char&gt;（char[] 缓冲）的零装箱、零字符串分配日志后端（B）。
        /// 所有方法均为 void + 双 Conditional，Release（无 DEBUG 且未定义 PrintLog）下调用点被整体剔除，零开销。
        /// 用法：
        ///   PrintTool.B.BeginLog();
        ///   PrintTool.B.Append("HP="); PrintTool.B.Append(hp);
        ///   PrintTool.B.EndLog();
        /// 缓冲为静态复用（单线程日志场景，避免每帧分配），溢出静默截断。
        /// </summary>
        public static class B
        {
            // 静态可复用字符缓冲（单线程日志场景，避免每帧分配）
            private static readonly char[] _buffer = new char[16384];
            private static int _length;

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void BeginLog()
            {
                _length = 0;
                var now = DateTime.Now;
                if (now.TryFormat(_buffer.AsSpan(1), out int w, "HH:mm:ss.fff".AsSpan(), null))
                {
                    _buffer[0] = '[';
                    _buffer[w + 1] = ']';
                    _length = w + 2;
                }
            }

            private static void AppendRaw(ReadOnlySpan<char> text)
            {
                if (text.IsEmpty) return;
                int room = _buffer.Length - _length;
                if (room <= 0) return;
                if (text.Length > room) text = text.Slice(0, room);   // 超出容量静默截断
                text.CopyTo(_buffer.AsSpan(_length));
                _length += text.Length;
            }

            private static void AppendSep() => AppendRaw(ConcatStr.AsSpan());

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void Append(string value)
            {
                if (value != null) AppendRaw(value.AsSpan());
                AppendSep();
            }

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void Append(int value)
            {
                if (value.TryFormat(_buffer.AsSpan(_length), out int w)) _length += w;
                AppendSep();
            }

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void Append(long value)
            {
                if (value.TryFormat(_buffer.AsSpan(_length), out int w)) _length += w;
                AppendSep();
            }

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void Append(uint value)
            {
                if (value.TryFormat(_buffer.AsSpan(_length), out int w)) _length += w;
                AppendSep();
            }

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void Append(ulong value)
            {
                if (value.TryFormat(_buffer.AsSpan(_length), out int w)) _length += w;
                AppendSep();
            }

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void Append(short value)
            {
                if (value.TryFormat(_buffer.AsSpan(_length), out int w)) _length += w;
                AppendSep();
            }

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void Append(ushort value)
            {
                if (value.TryFormat(_buffer.AsSpan(_length), out int w)) _length += w;
                AppendSep();
            }

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void Append(byte value)
            {
                if (value.TryFormat(_buffer.AsSpan(_length), out int w)) _length += w;
                AppendSep();
            }

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void Append(sbyte value)
            {
                if (value.TryFormat(_buffer.AsSpan(_length), out int w)) _length += w;
                AppendSep();
            }

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void Append(float value)
            {
                if (value.TryFormat(_buffer.AsSpan(_length), out int w)) _length += w;
                AppendSep();
            }

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void Append(double value)
            {
                if (value.TryFormat(_buffer.AsSpan(_length), out int w)) _length += w;
                AppendSep();
            }

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void Append(decimal value)
            {
                if (value.TryFormat(_buffer.AsSpan(_length), out int w)) _length += w;
                AppendSep();
            }

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void Append(bool value)
            {
                if (value.TryFormat(_buffer.AsSpan(_length), out int w)) _length += w;
                AppendSep();
            }

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void Append(char value)
            {
                if (_length < _buffer.Length) _buffer[_length++] = value;
                AppendSep();
            }

            // 兜底：引用类型或没有对应重载的值类型（会装箱 + 分配字符串，仅必要时使用）
            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void Append(object value)
            {
                if (value != null) AppendRaw(value.ToString().AsSpan());
                AppendSep();
            }

            // KV 形式（key=value）：key/value 须为 ISpanFormattable（所有数值/char/bool/DateTime/Guid 等均满足，string 不满足请用 Append）。
            // 走 TryFormat 直写 stackalloc 缓冲再拷入主缓冲，常见情况零分配；仅超长才回退 ToString()。
            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void AppendKV<K, V>(K key, V value)
                where K : ISpanFormattable
                where V : ISpanFormattable
            {
                AppendFormatted(key);
                AppendRaw(KVConcatStr.AsSpan());
                AppendFormatted(value);
                AppendSep();
            }

            // 字符串键重载：key 为 string（真实日志里键几乎都是字符串），value 仍须 ISpanFormattable。
            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void AppendKV<V>(string key, V value)
                where V : ISpanFormattable
            {
                AppendRaw(key.AsSpan());
                AppendRaw(KVConcatStr.AsSpan());
                AppendFormatted(value);
                AppendSep();
            }

            private static void AppendFormatted<T>(T value)
                where T : ISpanFormattable
            {
                Span<char> buffer = stackalloc char[128];
                if (value.TryFormat(buffer, out int written, default, null))
                    AppendRaw(buffer[..written]);
                else
                    AppendRaw(value.ToString().AsSpan());   // 极端超长才分配，极少走到
            }

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void EndLog()
            {
                if (_length > 0)
                {
                    Console.Out.Write(_buffer.AsSpan(0, _length));
                    Console.Out.Write('\n');
                }
                _length = 0;
            }

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void EndLogError()
            {
                if (_length > 0)
                {
                    Console.Error.Write(_buffer.AsSpan(0, _length));
                    Console.Error.Write('\n');
                }
                _length = 0;
            }

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void EndLogWarning()
            {
                if (_length > 0)
                {
                    Console.Out.Write("<color=orange>".AsSpan());
                    Console.Out.Write(_buffer.AsSpan(0, _length));
                    Console.Out.Write("</color>\n".AsSpan());
                }
                _length = 0;
            }

            [Conditional("DEBUG")]
            [Conditional(MacroConst.PrintLog)]
            public static void EndLogWithColor()
            {
                if (_length > 0)
                {
                    Console.Out.Write("<color=yellow>".AsSpan());
                    Console.Out.Write(_buffer.AsSpan(0, _length));
                    Console.Out.Write("</color>\n".AsSpan());
                }
                _length = 0;
            }
        }
    }
}
