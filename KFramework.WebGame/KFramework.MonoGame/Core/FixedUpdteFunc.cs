using System;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 固定步长累加器 —— 用来替代 MonoGame 已废弃的 <see cref="Game.IsFixedTimeStep"/> 固定步进逻辑。
    /// 引擎主循环现在走可变 dt（由 requestAnimationFrame 锁帧），需要稳定的定步长（物理、网络同步等）
    /// 时，由各个模块自行持有一个 <see cref="FixedUpdteFunc"/> 实例，在 Update 里每帧调用 <see cref="Update"/>。
    /// 它把可变帧率的渲染节奏解耦成恒定间隔的逻辑步进：回调收到的 dt 永远是 <see cref="FixedDeltaTime"/>，
    /// 不受帧率抖动影响，因此是物理积分的理想 dt（避免可变 dt 导致的穿模 / 数值不稳定）。
    /// </summary>
    /// <example>
    /// 在场景 / 角色里：
    /// <code>
    /// private readonly FixedUpdteFunc _physics = new FixedUpdteFunc { FixedDeltaTime = 1f / 60f };
    ///
    /// protected override void Update(GameTime gameTime)
    /// {
    ///     float dt = (float)gameTime.ElapsedGameTime.TotalSeconds; // 或 KTime.deltaTime
    ///     _physics.Update(dt, step =&gt;
    ///     {
    ///         // step 恒等于 FixedDeltaTime，用它做物理积分最稳
    ///         body.Velocity += gravity * step;
    ///         body.Position += body.Velocity * step;
    ///     });
    /// }
    /// </code>
    /// 想让渲染平滑（消除定步长带来的视觉抖动），用 <see cref="InterpolationAlpha"/> 在上一物理态与
    /// 当前物理态之间插值即可。
    /// </example>
    public sealed class FixedUpdteFunc
    {
        /// <summary>固定步进间隔（秒），默认 1/60。可随时修改，下一个 Update 起生效。</summary>
        public float FixedDeltaTime { get; set; } = 1f / 60f;

        /// <summary>单帧最多模拟的步数，防止卡顿后“螺旋死亡”（一次性补太多步拖死主线程）。默认 5。</summary>
        public int MaxStepsPerFrame { get; set; } = 5;

        /// <summary>已累计的固定步进时间（秒）。这是“固定时间”，与渲染 time 解耦。</summary>
        public float FixedTime { get; private set; }

        /// <summary>
        /// 渲染插值因子 [0, 1)：当前帧已走过的、不足一个固定步的比例。
        /// 把上一物理态按此比例插值到当前物理态，可消除定步长带来的视觉抖动。
        /// </summary>
        public float InterpolationAlpha
        {
            get
            {
                float d = FixedDeltaTime;
                return d > 0f ? Math.Min(1f, _accumulator / d) : 0f;
            }
        }

        /// <summary>未消费完的剩余时间，留到下一帧继续（正常运行时应 &lt; <see cref="FixedDeltaTime"/>）。</summary>
        public float Remaining => _accumulator;

        private float _accumulator;

        /// <summary>
        /// 每渲染帧调用一次：按 <paramref name="deltaSeconds"/> 累加，每达到一个 <see cref="FixedDeltaTime"/>
        /// 就执行一次 <paramref name="step"/>。未达到时本帧不执行任何步（返回 0），剩余时间自动留到下一帧。
        /// </summary>
        /// <param name="deltaSeconds">本帧逻辑 dt（秒）。通常传 <c>KTime.deltaTime</c> 或 <see cref="GameTime.ElapsedGameTime"/>。</param>
        /// <param name="step">固定步长回调，参数恒为 <see cref="FixedDeltaTime"/>。</param>
        /// <returns>本帧实际执行的步数（0 表示本帧没走到任何固定步，渲染应沿用上一物理态做插值）。</returns>
        public int Update(float deltaSeconds, Action<float> step)
        {
            if (step == null) return 0;

            _accumulator += deltaSeconds;

            // 螺旋死亡保护：超出单帧预算的时间直接丢弃（对齐 MonoGame 对 _maxElapsedTime 的 clamp）。
            float budget = FixedDeltaTime * MaxStepsPerFrame;
            if (_accumulator > budget) _accumulator = budget;

            int steps = 0;
            while (_accumulator >= FixedDeltaTime && steps < MaxStepsPerFrame)
            {
                step(FixedDeltaTime);
                FixedTime += FixedDeltaTime;
                _accumulator -= FixedDeltaTime;
                steps++;
            }
            return steps;
        }

        /// <summary>清空累加器与计时（场景切换 / 暂停恢复时调用，避免恢复瞬间“爆步”补模拟）。</summary>
        public void Reset()
        {
            _accumulator = 0f;
        }
    }
}
