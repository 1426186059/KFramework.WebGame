using System;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 材质：把「着色器 + 一组渲染状态 + 纹理采样参数」打包成可复用的绘制配置，
    /// 对应 Unity 的 Material（引用 Shader，并携带 Blend / 采样 / 深度 / 剔除等渲染状态）。
    /// <para>
    /// SpriteBatch.Begin 直接吃一个 Material，不再把 BlendState / SamplerState 当散参传。
    /// 其中 SamplerState 即 Unity 片元着色器里 <c>sampler2D</c> 绑定的「采样方式」（filter/wrap）：
    /// 本引擎的 Texture2D 是图本身、SamplerState 是采样参数，两者经 BindTexture + SetSamplerState 绑定到同一纹理单元，
    /// 等价于 Unity 把一个 Texture2D 属性按 filterMode/wrapMode（或 SRP 的 SamplerState）绑定给 sampler2D。
    /// WebGL2 无独立 sampler 对象，采样参数经 texParameteri 写到当前绑定的纹理上。
    /// </para>
    /// </summary>
    public sealed class Material
    {
        /// <summary>着色器程序（片元里的 sampler 即对应本引擎的 Texture2D + SamplerState）。
        /// 为 null 表示用 GraphicsDevice 的默认精灵着色器。</summary>
        public ISpriteProgram? Effect;

        /// <summary>混合状态（照 MonoGame 的 BlendState，对应 Unity Shader 的 Blend 命令）。</summary>
        public BlendState Blend = BlendState.NonPremultiplied;

        /// <summary>采样参数（照 MonoGame 的 SamplerState，对应 Unity 纹理的 filterMode/wrapMode 或 SRP 的 SamplerState 对象）。</summary>
        public SamplerState Sampler = SamplerState.Point;

        /// <summary>深度模板状态（对应 Unity 的 ZTest / ZWrite）。</summary>
        public DepthStencilState DepthStencil = DepthStencilState.None;

        /// <summary>光栅化状态（对应 Unity 的 Cull 命令）。</summary>
        public RasterizerState Rasterizer = RasterizerState.CullNone;
    }
}
