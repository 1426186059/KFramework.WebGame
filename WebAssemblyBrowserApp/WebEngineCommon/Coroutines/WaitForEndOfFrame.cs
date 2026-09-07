namespace Coroutines
{
    /// <summary>
    /// 等到本帧渲染结束（LateUpdate 之后）再继续（等价于 Unity 的 WaitForEndOfFrame）。
    /// 在当前单帧循环中，语义等同于“等待一帧”。
    /// </summary>
    public sealed class WaitForEndOfFrame : YieldInstruction
    {
    }
}
