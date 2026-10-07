namespace KFramework.MonoGame
{
    /// <summary>
    /// 材质：把「着色器 + 一组渲染状态 + 纹理采样参数 + 一组着色器属性」打包成可复用的绘制配置，
    /// 对应 Unity 的 Material（引用 Shader，携带 Blend / 采样 / 深度 / 剔除等渲染状态，以及往着色器里设的变量）。
    /// <para>
    /// SpriteBatch.Begin 直接吃一个 Material，不再把 BlendState / SamplerState 当散参传。
    /// 其中 SamplerState 即 Unity 片元着色器里 <c>sampler2D</c> 绑定的「采样方式」（filter/wrap）：
    /// 本引擎的 Texture2D 是图本身、SamplerState 是采样参数，两者经 BindTexture + SetSamplerState 绑定到同一纹理单元，
    /// 等价于 Unity 把一个 Texture2D 属性按 filterMode/wrapMode（或 SRP 的 SamplerState）绑定给 sampler2D。
    /// WebGL2 无独立 sampler 对象，采样参数经 texParameteri 写到当前绑定的纹理上。
    /// </para>
    /// <para>
    /// 属性（uniform）用法与 Unity 一致：<c>material.SetFloat("uTime", t)</c> / <c>SetVector("uParams", v)</c> /
    /// <c>SetColor("_Tint", Color.White)</c> / <c>SetMatrix("_M", m)</c> / <c>SetTexture("_Mask", tex)</c>，
    /// 绘制时由后端的精灵程序按属性名找到对应 uniform 灌入（名字在着色器里不存在就忽略，同 Unity）。
    /// 这里设的是「该材质的基线值」，会被 <see cref="ShaderEffect"/> 自带值盖住、又盖住效果自带值；
    /// 传入的 <see cref="MaterialPropertyBlock"/> 再覆盖这两者（照 Unity：越靠"这一次绘制"越优先）。
    /// 改属性会推进 <see cref="ShaderProperties.PropertiesVersion"/>，让材质去重短路失效、下一批绘制重新下发。
    /// </para>
    /// </summary>
    public sealed class Material : ShaderProperties
    {
        /// <summary>着色器效果（= 后端着色器程序 + 效果自带属性；片元里的 sampler 即对应本引擎的 Texture2D + SamplerState）。
        /// 为 null 表示用 GraphicsDevice 的默认效果。</summary>
        public ShaderEffect? Effect;

        /// <summary>混合状态（照 MonoGame 的 BlendState，对应 Unity Shader 的 Blend 命令）。</summary>
        public BlendState Blend = BlendState.NonPremultiplied;

        /// <summary>采样参数（照 MonoGame 的 SamplerState，对应 Unity 纹理的 filterMode/wrapMode 或 SRP 的 SamplerState 对象）。</summary>
        public SamplerState Sampler = SamplerState.Point;

        /// <summary>深度模板状态（对应 Unity 的 ZTest / ZWrite）。</summary>
        public DepthStencilState DepthStencil = DepthStencilState.None;

        /// <summary>光栅化状态（对应 Unity 的 Cull 命令）。</summary>
        public RasterizerState Rasterizer = RasterizerState.CullNone;

        /// <summary>恢复成一个「默认精灵材质」（渲染状态回默认 + 清空着色器属性）。</summary>
        public void Reset()
        {
            Effect = null;
            Blend = BlendState.NonPremultiplied;
            Sampler = SamplerState.Point;
            DepthStencil = DepthStencilState.None;
            Rasterizer = RasterizerState.CullNone;
            ClearProperties();
        }
    }
}
