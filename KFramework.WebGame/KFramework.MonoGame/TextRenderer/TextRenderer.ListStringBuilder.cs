using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace KFramework.MonoGame
{
    public static partial class TextRenderer
    {
        /// <summary>
        /// WrapLines 的返回容器：内部包含一个 <see cref="StringBuilder"/> 列表。
        /// _Count 标记当前有效行数；Reset() 只把它归零，StringBuilder 实例跨调用保留复用。
        /// 调用方必须立即消费，不得长期持有——下一次 WrapLines 会复用同一批实例。
        /// </summary>
        public sealed class ListStringBuilder : IEnumerable<StringBuilder>
        {
            private readonly List<StringBuilder> _lines = new List<StringBuilder>();
            private int _Count;

            public ListStringBuilder()
            {
            }

            public ListStringBuilder(int capacity)
            {
                _lines.Capacity = capacity;
            }

            /// <summary>当前有效行数。</summary>
            public int Count => _Count;

            /// <summary>第 index 行。</summary>
            public StringBuilder this[int index] => _lines[index];

            public StringBuilder Current
            {
                get
                {
                    if(_Count >= _lines.Count)
                    {
                       _lines.Add(new StringBuilder());
                    }
                    return _lines[_Count];
                }
            }

            /// <summary>把有效行数归零，StringBuilder 实例保留复用。</summary>
            public void Reset()
            {
                _Count = 0;
                Current.Clear();
            }

            /// <summary>新增一行：推进有效行数并返回清空后的 StringBuilder 供填充。</summary>
            public StringBuilder AddLine()
            {
                _Count++;
                return Current.Clear();
            }

            public IEnumerator<StringBuilder> GetEnumerator()
            {
                for (int i = 0; i < _Count; i++)
                {
                    yield return _lines[i];
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}
