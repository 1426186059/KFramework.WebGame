using System;
using System.Numerics;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 着色器效果：把一个后端着色器程序（<see cref="IShaderProgram"/>：WebGL = GLSL Program，WebGPU = WGSL 模块 + 渲染管线）
    /// 与它自带的一组属性值打包在一起，对应原版 MonoGame 的 <c>Effect</c>（程序 + EffectParameterCollection）。
    /// <para>
    /// 它同时是一种 <see cref="ShaderProperties"/>：效果自带的是这组 uniform 的<b>默认材质属性</b>
    /// （<c>effect.SetFloat("uCustom", v)</c>）。绘制时先发这份默认值，再由
    /// <see cref="ShaderPropertyBlock"/> 覆盖（同名以块为准，照 Unity 的 SetPropertyBlock）。
    /// </para>
    /// <para>
    /// 效果是<b>共享</b>的：多个 <see cref="Material"/> 可以引用同一个效果（也就共用同一份自带值），
    /// 需要"每个物体不同"时用 <see cref="ShaderPropertyBlock"/> 覆盖，或各自创建效果实例。
    /// </para>
    /// <para>
    /// 传 null（或不设置 <see cref="Material.Effect"/>）时回落到 <see cref="GraphicsDevice.Effect"/> 的默认效果。
    /// 自定义效果由 <see cref="GraphicsDevice.CreateShaderEffect"/> 创建。
    /// </para>
    /// </summary>
    public sealed class ShaderEffect : ShaderProperties, IDisposable
    {
        /// <summary>标准精灵顶点着色器源码（aPosition / aColor / aTexCoord + uProjection）。自定义片元着色器复用它以保证顶点属性位置与默认一致。</summary>
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

        /// <summary>底层的后端着色器程序（WebGL = GLSL Program，WebGPU = WGSL 模块 + 渲染管线）。</summary>
        public IShaderProgram Program { get; }

        public ShaderEffect(IShaderProgram program)
        {
            Program = program ?? throw new ArgumentNullException(nameof(program));
        }

        /// <summary>动画时间（秒）：即片元着色器的 <c>uTime</c> 属性（没设过时为 0）。</summary>
        public float Time
        {
            get { return TryGetProperty("uTime", out ShaderProperty p) ? p.Float : 0f; }
            set { SetFloat("uTime", value); }
        }

        /// <summary>
        /// 额外参数（vec4）：即片元着色器的 <c>uParams</c> 属性（没设过时为零向量）。
        /// x/y 常为纹理分辨率（用于求 texel 尺寸），z/w 为自定义参数。
        /// </summary>
        public Vector4 Params
        {
            get { return TryGetProperty("uParams", out ShaderProperty p) ? p.Vector : Vector4.Zero; }
            set { SetVector("uParams", value); }
        }

        /// <summary>是否为"逐帧动画"效果：为 true 时 GraphicsDevice 不做材质去重短路，每帧都重灌 uniform。</summary>
        public bool IsAnimated
        {
            get { return Program.IsAnimated; }
        }

        public void Dispose()
        {
            Program.Dispose();
        }
    }
}
