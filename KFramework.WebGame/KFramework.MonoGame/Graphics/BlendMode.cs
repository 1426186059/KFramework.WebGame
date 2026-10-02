namespace KFramework.MonoGame
{
    /// <summary>
    /// 混合因子（照 Unity 的 <c>UnityEngine.Rendering.BlendMode</c>，成员名与数值均与之一致）。
    /// <para>
    /// <b>与具体图形 API 无关</b>：由各渲染后端翻译成本 API 的常量 ——
    /// WebGL 见 <c>WebGl20Backend.ToGLBlendMode</c>，WebGPU 见 <c>WebGpuBackend.BlendModeName</c>。
    /// 这样同一套状态类能被两个后端共用。
    /// </para>
    /// <para>
    /// 不收录需要"混合常数色"的因子（Unity 里另有 ConstantColor 一类）：本状态类不携带常数色，
    /// 收录了也无法正确下发，宁缺勿留。
    /// </para>
    /// </summary>
    public enum BlendMode
    {
        Zero = 0,
        One = 1,
        DstColor = 2,
        SrcColor = 3,
        OneMinusDstColor = 4,
        SrcAlpha = 5,
        OneMinusSrcColor = 6,
        DstAlpha = 7,
        OneMinusDstAlpha = 8,
        SrcAlphaSaturate = 9,
        OneMinusSrcAlpha = 10,
    }
}
