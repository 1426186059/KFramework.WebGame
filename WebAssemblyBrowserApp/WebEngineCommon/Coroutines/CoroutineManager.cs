using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace Coroutines
{
    /// <summary>
    /// 协程调度器（等价于 Unity 的 MonoBehaviour 协程系统）。
    /// 由主循环每帧调用 Update()：把每个活跃协程推进一格（一个 yield 边界）。
    /// 因为每帧每协程只前进一格，长任务（如 DB 加载）会被自然分摊到多帧，
    /// 主线程（心跳/渲染）每帧都能运行 -> 不会冻结、不会触发服务端超时。
    /// </summary>
    public sealed class CoroutineManager
    {
        public static readonly CoroutineManager Instance = new CoroutineManager();

        private readonly List<Coroutine> _active = new List<Coroutine>();
        private readonly List<Coroutine> _pending = new List<Coroutine>();

        /// <summary>启动一个协程，返回其句柄（等价于 Unity 的 MonoBehaviour.StartCoroutine）。
        /// 句柄可用于 yield return（链式等待）或 StopCoroutine。
        /// 也可直接 yield return 一个返回 IEnumerator 的方法（如 Session.Initialize），自动作为嵌套协程运行。</summary>
        public Coroutine StartCoroutine(IEnumerator routine)
        {
            var c = new Coroutine(routine);
            _pending.Add(c);
            return c;
        }

        /// <summary>停止指定协程句柄（等价于 Unity 的 MonoBehaviour.StopCoroutine(Coroutine)）。</summary>
        public void StopCoroutine(Coroutine routine)
        {
            if (routine == null) return;
            routine.IsDone = true; // 标记为结束，Update 中会被移除；等待它的父协程也会随之继续
            _pending.Remove(routine);
            _active.Remove(routine);
        }

        /// <summary>停止由该 enumerator 实例启动的协程（等价于 Unity 的 StopCoroutine(IEnumerator) 重载）。</summary>
        public void StopCoroutine(IEnumerator routine)
        {
            if (routine == null) return;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_active[i].Routine, routine)) { _active[i].IsDone = true; _active.RemoveAt(i); }
            }
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_pending[i].Routine, routine)) _pending.RemoveAt(i);
            }
        }

        /// <summary>停止全部协程（等价于 Unity 的 MonoBehaviour.StopAllCoroutines）。</summary>
        public void StopAllCoroutines()
        {
            foreach (var c in _active) c.IsDone = true;
            foreach (var c in _pending) c.IsDone = true;
            _active.Clear();
            _pending.Clear();
        }

        /// <summary>每帧调用：推进所有协程一格。应在心跳/渲染之前调用（等价于 Unity 玩家循环里的协程阶段）。</summary>
        public void Update()
        {
            long now = Stopwatch.GetTimestamp();
            if (_pending.Count > 0) { _active.AddRange(_pending); _pending.Clear(); }

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Coroutine c = _active[i];
                if (c.IsDone) { _active.RemoveAt(i); continue; }
                if (c.IsReady(now))
                {
                    try
                    {
                        bool spawned;
                        Coroutine nested = c.Advance(now, out spawned);
                        if (spawned && nested != null)
                            _pending.Add(nested); // 下一帧提升为活跃并推进（与 Unity 嵌套协程下一帧起步一致）
                    }
                    catch (Exception ex)
                    {
                        // 对齐 Unity：协程内抛异常会被捕获并记录，不会中断整个玩家循环。
                        Console.WriteLine($"[Coroutine] 协程执行异常: {ex}");
                        c.IsDone = true;
                    }
                    if (c.IsDone) _active.RemoveAt(i);
                }
            }
        }
    }
}
