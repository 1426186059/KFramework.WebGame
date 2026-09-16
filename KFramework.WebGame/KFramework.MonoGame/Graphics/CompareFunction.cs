namespace KFramework.MonoGame
{
    /// <summary>深度/模板比较函数（照 MonoGame 的 CompareFunction）。</summary>
    public enum CompareFunction
    {
        /// <summary>始终通过。</summary>
        Always,
        /// <summary>从不通过。</summary>
        Never,
        /// <summary>小于通过。</summary>
        Less,
        /// <summary>等于通过。</summary>
        Equal,
        /// <summary>小于等于通过。</summary>
        LessEqual,
        /// <summary>大于通过。</summary>
        Greater,
        /// <summary>不等于通过。</summary>
        NotEqual,
        /// <summary>大于等于通过。</summary>
        GreaterEqual,
    }
}
