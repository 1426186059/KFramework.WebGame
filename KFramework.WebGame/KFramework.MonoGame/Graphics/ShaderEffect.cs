using System;
using System.Numerics;

namespace KFramework.MonoGame
{
    public sealed class ShaderEffect : Effect
    {
        /// <summary>动画时间（秒），作用于片元着色器的 uTime。</summary>
        public float Time;

        /// <summary>
        /// 额外参数（vec4）：x/y 常为纹理分辨率（用于求 texel 尺寸），z/w 为自定义参数。
        /// 作用于片元着色器的 uParams。
        /// </summary>
        public Vector4 Params;

        public ShaderEffect(IShaderProgram program) : base(program) { }
    }

    /// <summary>自定义精灵程序需要回指其 <see cref="ShaderEffect"/>，以读取每帧的 <see cref="ShaderEffect.Time"/> / <see cref="ShaderEffect.Params"/>。</summary>
    internal interface ICustomShaderProgram
    {
        void SetOwner(ShaderEffect owner);
    }
}
