namespace KFramework.MonoGame
{
    /// <summary>
    /// 材质：把「着色器效果 + 一组渲染状态 + 纹理采样参数」打包成可复用的绘制配置，
    /// 对应 Unity 的 Material（引用 Shader，携带 Blend / 采样 / 深度 / 剔除等渲染状态）。
    /// <para>
    /// SpriteBatch.Begin 直接吃一个 Material，不再把 BlendState / SamplerState 当散参传。
    /// 其中 SamplerState 即 Unity 片元着色器里 <c>sampler2D</c> 绑定的「采样方式」（filter/wrap）：
    /// 本引擎的 Texture2D 是图本身、SamplerState 是采样参数，两者经 BindTexture + SetSamplerState 绑定到同一纹理单元，
    /// 等价于 Unity 把一个 Texture2D 属性按 filterMode/wrapMode（或 SRP 的 SamplerState）绑定给 sampler2D。
    /// WebGL2 无独立 sampler 对象，采样参数经 texParameteri 写到当前绑定的纹理上。
    /// </para>
    /// <para>
    /// <b>着色器属性不在这里设</b>：本类只描述"怎么画"（状态），"往着色器里灌什么值"由
    /// <see cref="ShaderEffect"/>（效果自带值，可被多个材质共用）与 <see cref="ShaderPropertyBlock"/>
    /// （这一次绘制临时覆盖的值）承担，见 <see cref="ShaderProperties"/>。
    /// </para>
    /// </summary>
    public sealed class Material
    {
        /// <summary>着色器效果（= 后端着色器程序 + 默认材质属性）。
        /// 为 null 表示用 GraphicsDevice 的默认效果 —— 绘制时会被填成设备默认效果（程序统一从本字段取效果）。</summary>
        public ShaderEffect? Effect;

        /// <summary>混合状态（照 MonoGame 的 BlendState，对应 Unity Shader 的 Blend 命令）。</summary>
        public BlendState Blend = BlendState.NonPremultiplied;

        /// <summary>采样参数（照 MonoGame 的 SamplerState，对应 Unity 纹理的 filterMode/wrapMode 或 SRP 的 SamplerState 对象）。</summary>
        public SamplerState Sampler = SamplerState.Point;

        /// <summary>深度模板状态（对应 Unity 的 ZTest / ZWrite）。</summary>
        public DepthStencilState DepthStencil = DepthStencilState.None;

        /// <summary>光栅化状态（对应 Unity 的 Cull 命令）。</summary>
        public RasterizerState Rasterizer = RasterizerState.CullNone;

        /// <summary>「着色器属性 → 实例通道」的映射表（null / 空 = 没声明，实例化路径不能下发块值）。</summary>
        internal GpuInstanceChannelMap? GpuInstanceChannels;

        /// <summary>是否声明了实例通道（声明后，Add 传入的块值随实例数据走）。</summary>
        public bool HasGpuInstanceChannels { get { return GpuInstanceChannels is { IsEmpty: false }; } }

        /// <summary>
        /// 声明「哪些着色器属性按顺序进实例通道」——本引擎的实例化属性声明（Unity 写在 shader 的
        /// <c>UNITY_INSTANCING_BUFFER</c> 里，本引擎的实例化着色器固定，故声明在材质上）。
        /// 每项形如 <c>"uTint.rgb"</c>（3 个分量占 3 个槽）或 <c>"uPhase"</c>（1 个槽，取 x）；
        /// 共 8 个 float 槽，按声明顺序填 <c>aInst0.xyzw</c> → <c>aInst1.xyzw</c>。
        /// <para>
        /// 只对 GPU 实例化路径（<see cref="GpuInstanceBatch"/>）有意义：声明过的属性可以用
        /// <see cref="ShaderPropertyBlock"/> 逐精灵给不同值，而值随实例数据走、<b>整批仍然只一次 DrawCall</b>。
        /// 传给 <see cref="GpuInstanceBatch.Add(in Matrix4x4, Color, ShaderPropertyBlock?)"/> 的块里
        /// 出现<b>没被声明</b>的属性会直接抛异常 —— 实例化路径没有 uniform 覆盖层，
        /// 未声明的属性（矩阵 / 纹理等）无处可去。
        /// </para>
        /// <para>
        /// 本声明也随材质一起被 <see cref="GpuInstanceBatch"/> 复制（照 Unity：声明属于材质）。
        /// CPU 合批路径（<see cref="SpriteBatch"/>）不使用它，那里的属性块只能是可变 uniform（块变即切批）。
        /// </para>
        /// </summary>
        public void SetGpuInstanceChannels(params string[] paths)
        {
            GpuInstanceChannels ??= new GpuInstanceChannelMap();
            GpuInstanceChannels.Set(paths);
        }

        /// <summary>恢复成一个「默认精灵材质」（渲染状态回默认 + 效果置空 + 清掉实例通道声明）。</summary>
        public void Reset()
        {
            Effect = null;
            Blend = BlendState.NonPremultiplied;
            Sampler = SamplerState.Point;
            DepthStencil = DepthStencilState.None;
            Rasterizer = RasterizerState.CullNone;
            GpuInstanceChannels = null;
        }
    }
}
