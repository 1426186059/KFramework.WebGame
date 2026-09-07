namespace Coroutines
{
    /// <summary>
    /// 等到下一次 FixedUpdate 后再继续（等价于 Unity 的 WaitForFixedUpdate）。
    /// 在当前单帧循环中，语义等同于“等待一帧”。
    /// </summary>
    public sealed class WaitForFixedUpdate : YieldInstruction
    {
    }
}
