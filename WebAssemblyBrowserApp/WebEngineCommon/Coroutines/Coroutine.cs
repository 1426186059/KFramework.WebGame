using System;
using System.Collections;
using System.Diagnostics;

namespace Coroutines
{
    /// <summary>
    /// 一个正在运行的协程句柄（等价于 Unity 的 Coroutine）。
    /// 由 CoroutineManager 每帧推进一格（一次 MoveNext），与 Unity 的协程调度一致：
    /// 每帧每个协程最多前进一个 yield 边界，下一帧才会继续，因此长任务不会冻结主线程。
    /// 可对以下 yield 值作出响应：
    ///   - null / WaitForEndOfFrame / WaitForFixedUpdate : 下一帧继续
    ///   - WaitForSeconds(s)                              : 约 s 秒后继续
    ///   - WaitWhile(pred) / WaitUntil(pred)             : 条件满足前每帧轮询
    ///   - 另一个 IEnumerator                             : 作为嵌套协程运行，直至其完成再继续（Unity 经典写法，如 yield return Session.Initialize(...)）
    ///   - 另一个 Coroutine 句柄                          : 等待该协程完成后继续（yield return StartCoroutine(...)）
    /// </summary>
    public sealed class Coroutine
    {
        private readonly IEnumerator _enumerator;
        private object _pending;     // 当前挂起的 yield 值（null / YieldInstruction / Coroutine / IEnumerator）
        private long _resumeTicks;   // WaitForSeconds 的恢复时间戳

        public bool IsDone { get; internal set; }

        internal IEnumerator Routine => _enumerator;

        internal Coroutine(IEnumerator enumerator)
        {
            _enumerator = enumerator;
            _pending = null;
            IsDone = false;
        }

        /// <summary>当前挂起的指令是否已就绪，可以前进一格。</summary>
        internal bool IsReady(long nowTicks)
        {
            object p = _pending;
            if (p == null) return true;                              // yield return null -> 下一帧
            if (p is WaitForEndOfFrame) return true;                // 下一帧
            if (p is WaitForFixedUpdate) return true;               // 下一帧
            if (p is WaitForSeconds) return nowTicks >= _resumeTicks;
            if (p is WaitWhile ww) return !ww.Predicate();
            if (p is WaitUntil wu) return wu.Predicate();
            if (p is Coroutine child) return child.IsDone;          // 等待句柄协程
            return true;                                            // 未知 yield（含任意普通对象）-> 当作 null（下一帧）
        }

        /// <summary>
        /// 前进一格（一次 MoveNext）。若 yield 出原始 IEnumerator，则创建嵌套协程并返回给管理器注册。
        /// 返回 null 表示未产生嵌套协程（或已结束）。
        /// </summary>
        internal Coroutine Advance(long nowTicks, out bool spawnedNested)
        {
            spawnedNested = false;
            if (IsDone) return null;
            if (!_enumerator.MoveNext()) { IsDone = true; return null; }
            object cur = _enumerator.Current;
            if (cur is WaitForSeconds wfs)
            {
                _resumeTicks = nowTicks + (long)(wfs.Seconds * Stopwatch.Frequency);
                _pending = cur;
                return null;
            }
            if (cur is YieldInstruction) { _pending = cur; return null; }
            if (cur is Coroutine child) { _pending = child; return null; }   // 等待已注册的句柄协程
            if (cur is IEnumerator childEnum)                                   // Unity 风格：yield return 另一个迭代器 -> 自动作为嵌套协程运行并等待完成
            {
                var nested = new Coroutine(childEnum);
                _pending = nested;
                spawnedNested = true;
                return nested;
            }
            _pending = null; // yield return null 或任意非指令对象 -> 下一帧
            return null;
        }
    }
}
