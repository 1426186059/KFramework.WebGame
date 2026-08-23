namespace KFramework.MonoGame
{
    /// <summary>
    /// 全局设置中心。KTime 等引擎类只读/写这里的值，运行时改立即生效。
    /// </summary>
    public static class KSetting
    {
        /// <summary>时间缩放（1 = 正常，0 = 暂停，2 = 两倍速）。对应 Unity Time.timeScale。</summary>
        public static float TimeScale { get; set; } = 1.0f;

        /// <summary>固定步进间隔（秒），Unity 默认 0.02。对应 Unity Time.fixedDeltaTime。</summary>
        public static float FixedDeltaTime { get; set; } = 0.02f;

        /// <summary>一帧允许的最大 deltaTime（秒），超过会被截断，Unity 默认 0.333。对应 Unity Time.maximumDeltaTime。</summary>
        public static float MaximumDeltaTime { get; set; } = 0.333f;

        /// <summary>强制固定帧率（每秒多少帧），0 = 不限制。对应 Unity Time.captureFramerate。</summary>
        public static int CaptureFramerate { get; set; } = 0;
    }
}
