namespace KFramework.MonoGame
{
    /// <summary>
    /// 模板缓冲操作（照 Unity 的 <c>UnityEngine.Rendering.StencilOp</c>，成员名与数值均与之一致）。
    /// <para>
    /// 决定"测试结果"如何改写模板缓冲里的值。一个模板状态要为三种结果各指定一个操作：
    /// <list type="bullet">
    ///   <item><description><c>Fail</c>：模板测试未通过</description></item>
    ///   <item><description><c>ZFail</c>：模板测试通过、但深度测试未通过</description></item>
    ///   <item><description><c>Pass</c>：两者都通过</description></item>
    /// </list>
    /// </para>
    /// </summary>
    public enum StencilOp
    {
        /// <summary>保持原值不变（默认）。</summary>
        Keep = 0,

        /// <summary>写 0。</summary>
        Zero = 1,

        /// <summary>写入参考值（见 <see cref="StencilState.Reference"/>）。</summary>
        Replace = 2,

        /// <summary>加 1，饱和在 255 不再增加。</summary>
        IncrementSaturate = 3,

        /// <summary>减 1，饱和在 0 不再减少。</summary>
        DecrementSaturate = 4,

        /// <summary>按位取反。</summary>
        Invert = 5,

        /// <summary>加 1，到 255 后回绕为 0。</summary>
        IncrementWrap = 6,

        /// <summary>减 1，到 0 后回绕为 255。</summary>
        DecrementWrap = 7,
    }
}
