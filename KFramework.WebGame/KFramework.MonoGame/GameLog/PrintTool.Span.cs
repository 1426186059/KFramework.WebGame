using System;

namespace KFramework.MonoGame
{
    public static partial class PrintTool
    {
        // 静态可复用字符缓冲（单线程日志场景，与 StringBuilder 版同样的取舍，避免每帧分配）。
        // 若需多线程 / 零驻留，可改为 ArrayPool<char>.Shared.Rent(...)，并在 Log 时归还。
        private static readonly char[] SpanBuffer = new char[16384];

        /// <summary>
        /// 开启一次基于 Span&lt;char&gt; 的零装箱、零字符串分配日志会话：把值类型用 TryFormat 直接格式化进字符缓冲，
        /// 最后用 Console.Out.Write(ReadOnlySpan&lt;char&gt;) 直接写出（不经 ToString，避免字符串分配）。
        /// 例：PrintTool.BeginSpan().Append("HP=").Append(hp).Append(" MP=").Append(mp).Log();
        /// 非 DEBUG 下返回空会话，Append/Log 均为 no-op。
        /// </summary>
        public static PrintSpan BeginSpan()
        {
#if DEBUG
            var span = new PrintSpan(SpanBuffer);
            // 与 GetStr / StringBuilder 版保持一致：前缀 [HH:mm:ss.fff]
            span.WriteTimestamp();
            return span;
#else
            return default;
#endif
        }


        /// <summary>
        /// 基于 Span&lt;char&gt; 的零装箱日志拼接器：强类型 Append 用 TryFormat 直接写入字符缓冲，避免装箱与字符串分配。
        /// 注意：这是 ref struct，只能同步链式使用（不能存字段 / 进 async / 被 lambda 捕获）。缓冲溢出会静默截断。
        /// </summary>
        public ref struct PrintSpan
        {
            private readonly Span<char> _buffer;
            private int _length;

            internal PrintSpan(char[] buffer) { _buffer = buffer; _length = 0; }

            // 时间前缀 [HH:mm:ss.fff]：直接 TryFormat 进缓冲，避免 ToString 字符串分配（仅一次 DateTime 装箱）
            internal void WriteTimestamp()
            {
                var now = DateTime.Now;
                if (now.TryFormat(_buffer.Slice(1), out int w, "HH:mm:ss.fff".AsSpan(), null))
                {
                    _buffer[0] = '[';
                    _buffer[w + 1] = ']';
                    _length = w + 2;
                }
            }

            internal PrintSpan AppendRaw(ReadOnlySpan<char> text)
            {
                if (_buffer.IsEmpty) return this;
                int room = _buffer.Length - _length;
                if (room <= 0) return this;
                if (text.Length > room) text = text.Slice(0, room);   // 超出容量静默截断
                text.CopyTo(_buffer.Slice(_length));
                _length += text.Length;
                return this;
            }

            private PrintSpan AppendSep() => AppendRaw(PrintTool.ConcatStr.AsSpan());

            public PrintSpan Append(string value)
            {
                AppendRaw(value.AsSpan());
                return AppendSep();
            }

            public PrintSpan Append(int value)
            {
                if (!_buffer.IsEmpty && value.TryFormat(_buffer.Slice(_length), out int w)) _length += w;
                return AppendSep();
            }
            public PrintSpan Append(long value)
            {
                if (!_buffer.IsEmpty && value.TryFormat(_buffer.Slice(_length), out int w)) _length += w;
                return AppendSep();
            }
            public PrintSpan Append(uint value)
            {
                if (!_buffer.IsEmpty && value.TryFormat(_buffer.Slice(_length), out int w)) _length += w;
                return AppendSep();
            }
            public PrintSpan Append(ulong value)
            {
                if (!_buffer.IsEmpty && value.TryFormat(_buffer.Slice(_length), out int w)) _length += w;
                return AppendSep();
            }
            public PrintSpan Append(short value)
            {
                if (!_buffer.IsEmpty && value.TryFormat(_buffer.Slice(_length), out int w)) _length += w;
                return AppendSep();
            }
            public PrintSpan Append(ushort value)
            {
                if (!_buffer.IsEmpty && value.TryFormat(_buffer.Slice(_length), out int w)) _length += w;
                return AppendSep();
            }
            public PrintSpan Append(byte value)
            {
                if (!_buffer.IsEmpty && value.TryFormat(_buffer.Slice(_length), out int w)) _length += w;
                return AppendSep();
            }
            public PrintSpan Append(sbyte value)
            {
                if (!_buffer.IsEmpty && value.TryFormat(_buffer.Slice(_length), out int w)) _length += w;
                return AppendSep();
            }
            public PrintSpan Append(float value)
            {
                if (!_buffer.IsEmpty && value.TryFormat(_buffer.Slice(_length), out int w)) _length += w;
                return AppendSep();
            }
            public PrintSpan Append(double value)
            {
                if (!_buffer.IsEmpty && value.TryFormat(_buffer.Slice(_length), out int w)) _length += w;
                return AppendSep();
            }
            public PrintSpan Append(decimal value)
            {
                if (!_buffer.IsEmpty && value.TryFormat(_buffer.Slice(_length), out int w)) _length += w;
                return AppendSep();
            }
            public PrintSpan Append(bool value)
            {
                if (!_buffer.IsEmpty && value.TryFormat(_buffer.Slice(_length), out int w)) _length += w;
                return AppendSep();
            }
            public PrintSpan Append(char value)
            {
                if (!_buffer.IsEmpty && _length < _buffer.Length) _buffer[_length++] = value;
                return AppendSep();
            }

            // 兜底：引用类型或没有对应重载的值类型（会装箱 + 分配字符串，仅必要时使用）
            public PrintSpan Append(object value)
            {
                if (!_buffer.IsEmpty && value != null) AppendRaw(value.ToString().AsSpan());
                return AppendSep();
            }

            public void Log()
            {
#if DEBUG
                if (!_buffer.IsEmpty && _length > 0)
                {
                    Console.Out.Write(_buffer.Slice(0, _length));
                    Console.Out.Write('\n');
                }
#endif
            }

            public void LogError()
            {
#if DEBUG
                if (!_buffer.IsEmpty && _length > 0)
                {
                    Console.Error.Write(_buffer.Slice(0, _length));
                    Console.Error.Write('\n');
                }
#endif
            }

            public void LogWarning()
            {
#if DEBUG
                if (!_buffer.IsEmpty && _length > 0)
                {
                    Console.Out.Write("<color=orange>".AsSpan());
                    Console.Out.Write(_buffer.Slice(0, _length));
                    Console.Out.Write("</color>\n".AsSpan());
                }
#endif
            }

            public void LogWithColor()
            {
#if DEBUG
                if (!_buffer.IsEmpty && _length > 0)
                {
                    Console.Out.Write("<color=yellow>".AsSpan());
                    Console.Out.Write(_buffer.Slice(0, _length));
                    Console.Out.Write("</color>\n".AsSpan());
                }
#endif
            }

            public override string ToString() => !_buffer.IsEmpty ? new string(_buffer.Slice(0, _length)) : string.Empty;
        }
    }
}
