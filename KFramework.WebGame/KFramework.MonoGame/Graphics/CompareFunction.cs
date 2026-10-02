namespace KFramework.MonoGame
{
    /// <summary>
    /// 深度/模板比较函数（照 Unity 的 <c>UnityEngine.Rendering.CompareFunction</c>，成员名与数值均与之一致）。
    /// <para>
    /// 比 MonoGame 版多一个 <see cref="Disabled"/>（Unity 语义：关闭该比较测试）。
    /// 后端处理：<see cref="Disabled"/> 即不开启深度测试
    /// （见 <c>WebGl20Backend.ApplyDepth</c>）。
    /// </para>
    /// </summary>
    public enum CompareFunction
    {
        /// <summary>关闭测试（Unity 语义）。用于"不比较、直接通过"的场景。</summary>
        Disabled = 0,

        /// <summary>从不通过。</summary>
        Never = 1,
        /// <summary>小于通过。</summary>
        Less = 2,
        /// <summary>等于通过。</summary>
        Equal = 3,
        /// <summary>小于等于通过。</summary>
        LessEqual = 4,
        /// <summary>大于通过。</summary>
        Greater = 5,
        /// <summary>不等于通过。</summary>
        NotEqual = 6,
        /// <summary>大于等于通过。</summary>
        GreaterEqual = 7,
        /// <summary>始终通过。</summary>
        Always = 8,
    }
}
