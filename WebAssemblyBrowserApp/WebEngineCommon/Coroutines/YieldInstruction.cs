namespace Coroutines
{
    /// <summary>
    /// 协程暂停指令的基类（等价于 Unity 的 YieldInstruction）。
    /// 具体子类：WaitForSeconds / WaitForFixedUpdate / WaitForEndOfFrame / WaitWhile / WaitUntil。
    /// 此外 yield return null 表示“下一帧继续”，yield return 另一个 Coroutine 表示“等待该协程完成后继续”。
    /// </summary>
    public abstract class YieldInstruction
    {
    }
}
