namespace Coroutines
{
    /// <summary>
    /// 在条件为 true 期间一直等待，条件变 false 后继续（等价于 Unity 的 WaitUntil）。
    /// </summary>
    public sealed class WaitUntil : YieldInstruction
    {
        public System.Func<bool> Predicate { get; }
        public WaitUntil(System.Func<bool> predicate) => Predicate = predicate;
    }
}
