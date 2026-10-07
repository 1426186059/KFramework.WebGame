using System;
using System.Numerics;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 自定义着色器效果：用一段片元着色器源码（GLSL ES 3.00）包装出一个可被 <see cref="SpriteBatch.Begin"/> 使用的 <see cref="Effect"/>。
    /// <para>
    /// 配合 <see cref="GraphicsDevice.CreateShaderEffect"/> 创建；每帧由场景写入 <see cref="Time"/> / <see cref="Params"/>，
    /// 绘制时随材质下发给 GPU（由后端的 <see cref="ICustomSpriteProgram"/> 读取并灌入 uTime / uParams）。
    /// </para>
    /// <para>注：当前仅 WebGL 后端真正编译自定义着色器；WebGPU 后端回落到默认精灵着色器（效果不生效，但页面照常运行）。</para>
    /// </summary>
    public sealed class ShaderEffect : Effect
    {
        /// <summary>标准精灵顶点着色器源码（aPosition / aColor / aTexCoord + uProjection）。自定义程序复用它以保证顶点属性位置与默认一致。</summary>
        public const string DefaultVertexSource = @"
#version 300 es
// 顶点输入对齐 Unity 精灵着色器的 appdata_t：aPosition ↔ float4 vertex : POSITION，
// aColor ↔ float4 color : COLOR，aTexCoord ↔ float2 texcoord : TEXCOORD0。
in vec4 aPosition;
in vec4 aColor;
in vec2 aTexCoord;
uniform mat4 uProjection;
out vec2 vTexCoord;
out vec4 vColor;
// UNITY_VERTEX_INPUT_INSTANCE_ID 在 GLSL 里的等价物：实例号是内置输入，不占顶点布局。
flat out int vInstanceID;
void main()
{
    gl_Position = uProjection * aPosition;
    vTexCoord = aTexCoord;
    vColor = aColor;
    vInstanceID = gl_InstanceID;
}";

        /// <summary>动画时间（秒），作用于片元着色器的 uTime。</summary>
        public float Time;

        /// <summary>
        /// 额外参数（vec4）：x/y 常为纹理分辨率（用于求 texel 尺寸），z/w 为自定义参数。
        /// 作用于片元着色器的 uParams。
        /// </summary>
        public Vector4 Params;

        public ShaderEffect(ISpriteProgram program) : base(program) { }
    }

    /// <summary>自定义精灵程序需要回指其 <see cref="ShaderEffect"/>，以读取每帧的 <see cref="ShaderEffect.Time"/> / <see cref="ShaderEffect.Params"/>。</summary>
    internal interface ICustomSpriteProgram
    {
        void SetOwner(ShaderEffect owner);
    }
}
