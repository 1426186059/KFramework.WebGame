namespace KFramework.MonoGame
{
    /// <summary>
    /// 混合运算（照 Unity 的 <c>UnityEngine.Rendering.BlendOp</c>，成员名与数值均与之一致）。
    /// 作用于"因子相乘之后"的结果：<c>结果 = 运算(Src × SrcFactor, Dst × DstFactor)</c>。
    /// <list type="bullet">
    ///   <item><description><see cref="Add"/>：Src + Dst（默认，绝大多数情况用它）</description></item>
    ///   <item><description><see cref="Sub"/>：Src − Dst</description></item>
    ///   <item><description><see cref="RevSub"/>：Dst − Src</description></item>
    ///   <item><description><see cref="Min"/>：min(Src, Dst)</description></item>
    ///   <item><description><see cref="Max"/>：max(Src, Dst)</description></item>
    /// </list>
    /// 颜色与 Alpha 可各自指定（见 <see cref="BlendState"/>）。
    /// </summary>
    public enum BlendOp
    {
        Add = 0,
        Sub = 1,
        RevSub = 2,
        Min = 3,
        Max = 4,
    }
}
