namespace KFramework.MonoGame
{
    /// <summary>
    /// 模板状态（照 Unity 的 <c>UnityEngine.Rendering.StencilState</c>）。
    /// 成员名改用本仓库的 PascalCase 风格，语义与 Unity 一一对应。
    /// <para>
    /// 正面 / 背面各有一组「比较函数 + 三种结果各自的操作」，与硬件一致
    /// （GL 的 <c>stencilFuncSeparate</c> / <c>stencilOpSeparate</c>，WebGPU 的 stencilFront / stencilBack）。
    /// </para>
    /// <para>
    /// 与 Unity 的一处差异：<b>参考值 <see cref="Reference"/> 折进了本类</b>。
    /// Unity 把 Ref 放在材质里（<c>Stencil { Ref 2 }</c>），不在 StencilState 中；
    /// 本引擎是即时状态模型、没有 per-material 着色器文本可写，故一并收纳
    /// （与 <see cref="BlendState"/> 同时携带颜色/Alpha 两套因子是同一个理由）。
    /// </para>
    /// </summary>
    public sealed class StencilState
    {
        /// <summary>是否开启模板测试（Unity: <c>enabled</c>）。</summary>
        public readonly bool Enabled;

        /// <summary>比较时先与参考值相与的掩码（Unity: <c>readMask</c>）。</summary>
        public readonly byte ReadMask;

        /// <summary>写入模板缓冲时的位掩码（Unity: <c>writeMask</c>）。</summary>
        public readonly byte WriteMask;

        /// <summary>参考值：与模板缓冲里的值做比较，也是 <see cref="StencilOp.Replace"/> 写入的值。</summary>
        public readonly byte Reference;

        public readonly CompareFunction CompareFunctionFront;
        public readonly StencilOp PassOperationFront;
        public readonly StencilOp FailOperationFront;
        public readonly StencilOp ZFailOperationFront;

        public readonly CompareFunction CompareFunctionBack;
        public readonly StencilOp PassOperationBack;
        public readonly StencilOp FailOperationBack;
        public readonly StencilOp ZFailOperationBack;

        /// <summary>正反面共用同一套配置（绝大多数情况）。</summary>
        public StencilState(bool enabled, byte reference = 0, byte readMask = 0xFF, byte writeMask = 0xFF,
                            CompareFunction compare = CompareFunction.Always,
                            StencilOp pass = StencilOp.Keep, StencilOp fail = StencilOp.Keep,
                            StencilOp zFail = StencilOp.Keep)
            : this(enabled, reference, readMask, writeMask,
                   compare, pass, fail, zFail,
                   compare, pass, fail, zFail)
        {
        }

        /// <summary>正反面各自指定。</summary>
        public StencilState(bool enabled, byte reference, byte readMask, byte writeMask,
                            CompareFunction compareFront, StencilOp passFront, StencilOp failFront, StencilOp zFailFront,
                            CompareFunction compareBack, StencilOp passBack, StencilOp failBack, StencilOp zFailBack)
        {
            Enabled = enabled;
            Reference = reference;
            ReadMask = readMask;
            WriteMask = writeMask;

            CompareFunctionFront = compareFront;
            PassOperationFront = passFront;
            FailOperationFront = failFront;
            ZFailOperationFront = zFailFront;

            CompareFunctionBack = compareBack;
            PassOperationBack = passBack;
            FailOperationBack = failBack;
            ZFailOperationBack = zFailBack;
        }

        /// <summary>
        /// Unity 的 <c>StencilState.Default</c>：<b>关闭</b>模板测试，掩码 255，比较 Always，操作 Keep。
        /// 注意 Unity 的默认就是关闭的，与深度默认开启不同。
        /// </summary>
        public static readonly StencilState Default = new(false);
    }
}
