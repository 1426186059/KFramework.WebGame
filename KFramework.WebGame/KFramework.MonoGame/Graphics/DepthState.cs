namespace KFramework.MonoGame
{
    /// <summary>
    /// 深度状态（照 Unity 的 <c>UnityEngine.Rendering.DepthState</c>：<c>depthWrite</c> + <c>depthCompare</c>）。
    /// 成员名改用本仓库的 PascalCase 风格，语义与 Unity 一一对应。
    /// <para>
    /// "关闭深度测试"用 <see cref="CompareFunction.Disabled"/> 表达（Unity 语义），
    /// 不再另设 bool 开关 —— 否则"两个字段都能表示关闭"会自相矛盾。
    /// </para>
    /// </summary>
    public sealed class DepthState
    {
        /// <summary>是否写入深度缓冲（Unity: <c>depthWrite</c>）。</summary>
        public readonly bool DepthWrite;

        /// <summary>
        /// 深度比较函数（Unity: <c>depthCompare</c>）。
        /// <see cref="CompareFunction.Disabled"/> 表示关闭深度测试。
        /// </summary>
        public readonly CompareFunction DepthCompare;

        /// <param name="depthWrite">是否写深度。</param>
        /// <param name="depthCompare">比较函数；<c>Disabled</c> 即关闭深度测试。</param>
        public DepthState(bool depthWrite = true, CompareFunction depthCompare = CompareFunction.LessEqual)
        {
            DepthWrite = depthWrite;
            DepthCompare = depthCompare;
        }

        /// <summary>Unity 的 <c>DepthState.Default</c>：写深度 + LessEqual 比较。</summary>
        public static readonly DepthState Default = new(true, CompareFunction.LessEqual);

        /// <summary>关闭深度测试与写入（2D 主链路用它）。</summary>
        public static readonly DepthState Disabled = new(false, CompareFunction.Disabled);

        /// <summary>只测试不写入：参与遮挡判断但不写深度（半透明物体的常见设置）。</summary>
        public static readonly DepthState ReadOnly = new(false, CompareFunction.LessEqual);
    }
}
