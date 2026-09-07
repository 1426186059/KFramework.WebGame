namespace Coroutines
{
    /// <summary>
    /// 在条件为 true 期间一直等待，条件变 false 后继续（等价于 Unity 的 WaitWhile）。
    /// </summary>
    public sealed class WaitWhile : YieldInstruction
    {
        public System.Func<bool> Predicate { get; }
        public WaitWhile(System.Func<bool> predicate) => Predicate = predicate;
    }
}
