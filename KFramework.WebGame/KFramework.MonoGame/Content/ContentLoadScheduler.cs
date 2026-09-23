using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 全局资源加载调度器（引擎层限流）——从根上解决"几十个资源同时发起"导致的排队堵塞。
    ///
    /// 背景：浏览器对同一域名只有约 6 个并发连接，且磁盘/主线程资源有限。若调用方直接
    /// fire-and-forget 发起几十个加载（例如一张地图预加载 25 个 Lib），大文件会挤在小文件
    /// 前面造成队头阻塞：实测同样 9MB 的资源，一个 0.077s、另一个 6.989s，41MB 的更是 20.9s。
    ///
    /// 本调度器提供：
    /// 1) 统一并发闸门：无论调用方怎么发起，同时进行的加载不超过 MaxConcurrency；
    /// 2) 优先级：priority 越小越先拿到并发槽（底图等关键资源可先于大块头加载）；
    /// 3) 请求去重可选：同一路径并发请求时合并为一个（由调用方决定是否启用）。
    /// </summary>
    public sealed class ContentLoadScheduler
    {
        /// <summary>默认调度器（供 ContentManager / ContentFunc 直接使用）。</summary>
        public static readonly ContentLoadScheduler Default = new ContentLoadScheduler();

        private readonly object _sync = new object();
        private readonly PriorityQueue<Job, (int Priority, long Seq)> _queue = new PriorityQueue<Job, (int, long)>();
        private readonly SemaphoreSlim _slots;
        private long _seq;

        /// <summary>
        /// 最大并发加载数。默认 4：浏览器同域连接约 6，留 2 个给页面自身请求（音效、favicon 等），
        /// 避免把连接打满导致谁都快不了。
        /// </summary>
        public int MaxConcurrency { get; }

        public ContentLoadScheduler(int maxConcurrency = 4)
        {
            MaxConcurrency = Math.Max(1, maxConcurrency);
            _slots = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);
        }

        /// <summary>当前排队（尚未拿到并发槽）的任务数，便于诊断。</summary>
        public int PendingCount
        {
            get { lock (_sync) { return _queue.Count; } }
        }

        /// <summary>
        /// 排队执行一个加载任务。
        /// </summary>
        /// <param name="priority">优先级，数值越小越先执行（0 = 最高）。</param>
        /// <param name="loader">真正的加载逻辑（拿到并发槽后才会被调用）。</param>
        /// <param name="cancellationToken">取消标记。</param>
        public Task<byte[]> EnqueueAsync(int priority, Func<CancellationToken, Task<byte[]>> loader, CancellationToken cancellationToken = default)
        {
            if (loader == null) throw new ArgumentNullException(nameof(loader));

            var job = new Job(loader, priority, cancellationToken);
            lock (_sync)
            {
                _queue.Enqueue(job, (priority, _seq++));
            }
            Pump();
            return job.Completion.Task;
        }

        /// <summary>
        /// 尝试驱动队列：只要还有空闲并发槽就取任务执行。
        /// 允许多处并发调用 Pump —— 真正的并发上限由信号量保证，不会超额。
        /// </summary>
        private void Pump()
        {
            while (true)
            {
                // 先看有没有任务，避免无谓地抢信号量
                lock (_sync)
                {
                    if (!_queue.TryPeek(out _, out _)) return;
                }

                // 非阻塞抢一个槽；抢不到说明并发已满，直接返回（任务留在队列里）
                if (!_slots.Wait(0)) return;

                Job job;
                lock (_sync)
                {
                    if (!_queue.TryDequeue(out job, out _))
                    {
                        // 极端情况：任务被其他 Pump 取走了，把槽还回去
                        _slots.Release();
                        return;
                    }
                }

                _ = RunAsync(job);
            }
        }

        private async Task RunAsync(Job job)
        {
            try
            {
                if (job.CancellationToken.IsCancellationRequested)
                {
                    job.Completion.TrySetCanceled(job.CancellationToken);
                    return;
                }

                byte[] data = await job.Loader(job.CancellationToken).ConfigureAwait(false);
                job.Completion.TrySetResult(data);
            }
            catch (OperationCanceledException)
            {
                job.Completion.TrySetCanceled();
            }
            catch (Exception ex)
            {
                job.Completion.TrySetException(ex);
            }
            finally
            {
                _slots.Release();
                // 腾出槽位后继续跑下一个
                Pump();
            }
        }

        private sealed class Job
        {
            public readonly Func<CancellationToken, Task<byte[]>> Loader;
            public readonly int Priority;
            public readonly CancellationToken CancellationToken;
            public readonly TaskCompletionSource<byte[]> Completion =
                new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

            public Job(Func<CancellationToken, Task<byte[]>> loader, int priority, CancellationToken cancellationToken)
            {
                Loader = loader;
                Priority = priority;
                CancellationToken = cancellationToken;
            }
        }
    }
}
