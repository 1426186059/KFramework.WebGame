namespace Coroutines
{
    /// <summary>等待约指定秒数后继续（等价于 Unity 的 WaitForSeconds）。</summary>
    public sealed class WaitForSeconds : YieldInstruction
    {
        public float Seconds { get; }
        public WaitForSeconds(float seconds) => Seconds = seconds < 0f ? 0f : seconds;
    }
}
