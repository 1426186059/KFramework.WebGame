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

        /// <summary>
        /// 是否启用 GPU 实例化（照 Unity 的 <c>Material.enableInstancing</c>）：一次 <c>drawElementsInstanced</c>
        /// 画一批同纹理精灵，逐物体数据随实例缓冲走（见 <see cref="SpriteInstance"/>）。
        /// 需要后端支持：WebGPU 尚未接入，开启后绘制会抛 <see cref="NotSupportedException"/>。
        /// </summary>
        public bool EnableInstancing;

        /// <summary>恢复成一个「默认精灵材质」（渲染状态回默认 + 效果置空 + 关掉实例化）。</summary>
        public void Reset()
        {
            Effect = null;
            Blend = BlendState.NonPremultiplied;
            Sampler = SamplerState.Point;
            DepthStencil = DepthStencilState.None;
            Rasterizer = RasterizerState.CullNone;
            EnableInstancing = false;
        }
    }
}
