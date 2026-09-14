using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 全局时间工具 — 对齐 Unity 的 UnityEngine.Time。
    /// 每帧调用一次 From(GameTime)（KSceneMgr 已自动调用），字段语义与 Unity 一致。
    /// </summary>
    public static class KTime
    {
        // ===== 运行时状态（只读，由 From/StepFixedUpdate 维护） =====

        /// <summary>游戏开始以来经过的时间（受 timeScale 影响，秒）。</summary>
        public static float time;

        /// <summary>完成上一帧所用的时间（受 timeScale 影响，且被 maximumDeltaTime 截断）。</summary>
        public static float deltaTime;

        /// <summary>不受 timeScale 影响的上一帧时间（仍被 maximumDeltaTime 截断）。</summary>
        public static float unscaledDeltaTime;

        /// <summary>不受 timeScale 影响的时间（秒）。</summary>
        public static float unscaledTime;

        /// <summary>自应用启动以来的真实秒数（暂停/缩放均不影响）。</summary>
        public static float realtimeSinceStartup;

        /// <summary>自当前关卡加载以来经过的时间（受 timeScale 影响），场景切换时调用 ResetTimeSinceLevelLoad。</summary>
        public static float timeSinceLevelLoad;

        /// <summary>固定步进累计时间（受 timeScale 影响），在 StepFixedUpdate 中累加。</summary>
        public static float fixedTime;

        /// <summary>平滑后的 deltaTime（对最近若干帧做加权平均，避免单帧抖动）。</summary>
        public static float smoothDeltaTime;

        /// <summary>已渲染的帧数（Update 每帧 +1）。</summary>
        public static int frameCount;

        /// <summary>已渲染的帧数（Draw 每帧 +1，同 frameCount）。</summary>
        public static int renderedFrameCount;

        /// <summary>当前是否处于固定步进阶段（在 StepFixedUpdate 期间为 true）。</summary>
        public static bool inFixedTimeStep;

        // ===== 可配置（对齐 Unity 默认值，实际值统一存 KSetting） =====

        /// <summary>固定步进间隔（秒），Unity 默认 0.02。映射 KSetting.FixedDeltaTime。</summary>
        public static float fixedDeltaTime
        {
            get => KSetting.FixedDeltaTime;
            set => KSetting.FixedDeltaTime = value;
        }

        /// <summary>一帧允许的最大 deltaTime（秒），超过会被截断，Unity 默认 0.333。映射 KSetting.MaximumDeltaTime。</summary>
        public static float maximumDeltaTime
        {
            get => KSetting.MaximumDeltaTime;
            set => KSetting.MaximumDeltaTime = value;
        }

        /// <summary>时间缩放（1 = 正常，0 = 暂停，2 = 两倍速）。映射 KSetting.TimeScale，改动立即生效。</summary>
        public static float timeScale
        {
            get => KSetting.TimeScale;
            set => KSetting.TimeScale = value;
        }

        /// <summary>设置为 &gt;0 时强制固定帧率（每秒多少帧），用于录屏/性能测试；0 = 不限制。映射 KSetting.CaptureFramerate。</summary>
        public static int captureFramerate
        {
            get => KSetting.CaptureFramerate;
            set => KSetting.CaptureFramerate = value;
        }

        // ===== 内部 =====

        private static readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private static int _smoothFrameCount;

        /// <summary>
        /// 每帧 Update 调用一次（KSceneMgr 已自动调用）。
        /// 依次推进 time/unscaledTime/timeSinceLevelLoad/smoothDeltaTime 等字段。
        /// </summary>
        public static void From(GameTime mGameTime)
        {
            float raw = (float)mGameTime.ElapsedGameTime.TotalSeconds;

            frameCount++;
            renderedFrameCount++;
            realtimeSinceStartup = (float)_stopwatch.Elapsed.TotalSeconds;

            // 与 Unity 一致：先截断到 maximumDeltaTime，再乘 timeScale
            unscaledDeltaTime = Math.Min(raw, maximumDeltaTime);
            deltaTime = unscaledDeltaTime * timeScale;

            time += deltaTime;
            unscaledTime += unscaledDeltaTime;
            timeSinceLevelLoad += deltaTime;

            UpdateSmoothDeltaTime();
        }

        /// <summary>将 timeSinceLevelLoad 重置为 0（关卡/场景切换时调用）。</summary>
        public static void ResetTimeSinceLevelLoad()
        {
            timeSinceLevelLoad = 0f;
        }

        /// <summary>
        /// 执行一次固定步进（对齐 Unity 的 FixedUpdate）：
        /// fixedTime += fixedDeltaTime * timeScale，期间 inFixedTimeStep = true。
        /// 由你自己的固定步进循环调用，例如：
        ///   float acc = 0; acc += KTime.deltaTime; while (acc >= KTime.fixedDeltaTime) { acc -= KTime.fixedDeltaTime; KTime.StepFixedUpdate(); }
        /// </summary>
        public static void StepFixedUpdate()
        {
            inFixedTimeStep = true;
            fixedTime += fixedDeltaTime * timeScale;
            inFixedTimeStep = false;
        }

        /// <summary>按 captureFramerate 应用固定帧率到 Game（仅 captureFramerate &gt; 0 时生效）。</summary>
        public static void ApplyCaptureFramerate(Game game)
        {
            if (captureFramerate > 0)
            {
                game.IsFixedTimeStep = true;
                game.TargetElapsedTime = TimeSpan.FromSeconds(1.0 / captureFramerate);
            }
        }

        private static void UpdateSmoothDeltaTime()
        {
            // 累积平均：前 20 帧逐渐收敛，之后等价于约 20 帧的移动平均（Unity smoothDeltaTime 近似）
            _smoothFrameCount = Math.Min(_smoothFrameCount + 1, 20);
            smoothDeltaTime += (deltaTime - smoothDeltaTime) / _smoothFrameCount;
        }
    }
}
