using System.Numerics;
using KFramework.MonoGame;

namespace KFramework.MonoGameExtend.Mask
{
    /// <summary>
    /// 矩形遮罩（Shader 版，照 Unity UGUI RectMask2D 同思路：在片元里 clip 像素，而不是用 GPU scissor）。
    /// 按"本精灵自身四边形局部 UV 矩形"丢弃矩形外的像素（矩形外输出全透明，混合下等同被裁掉）。
    /// <para>
    /// 与 <see cref="RectMaskScissor"/>（GPU scissor，屏幕级）的区别：本版是逐元素、在 Shader 里裁，不依赖后端 scissor，
    /// 因此可对每个被画元素裁出子矩形（例如 <c>(0,0,0.5,1)</c> 只显示左半）。三后端通用（WebGL 真正生效）。
    /// </para>
    /// <example>
    /// <code>
    /// // 只显示被画元素的左半（局部 UV 矩形 (x,y,w,h)，0..1）
    /// var mat = RectMaskShader.CreateMaterial(device, new Vector4(0f, 0f, 0.5f, 1f));
    /// batch.Begin(mat); batch.Draw(image, ...); batch.End();
    ///
    /// // 已有 ShaderEffect 时，C# 侧单独赋值矩形与反相
    /// RectMaskShader.ApplyRect(myEffect, new Vector4(0f, 0f, 1f, 0.5f), invert: false);
    /// </code>
    /// </example>
    /// </summary>
    public static class RectMaskShader
    {
        /// <summary>Shader 资产名（照 Unity 资源路径风格，可用 <see cref="Shader.Find"/> 按名取）。</summary>
        public const string ShaderName = "KFramework/Mask/RectMaskShader";

        // 严格遵循引擎契约：声明 in vec2 vTexCoord / in vec4 vColor；#version 300 es；precision highp float；out vec4 fragColor。
        private const string Fragment = @"#version 300 es
precision highp float;
in vec2 vTexCoord;
in vec4 vColor;
uniform sampler2D uTexture;   // 主图（0 号单元，SpriteBatch 自动绑）
uniform vec4  uRect;          // 本精灵四边形局部 UV 矩形 (x, y, w, h)，范围 0..1
uniform float uInvert;        // 1=反相（矩形外可见、矩形内被裁）
out vec4 fragColor;
void main(){
  vec4 c = texture(uTexture, vTexCoord) * vColor;
  bool inside = vTexCoord.x >= uRect.x && vTexCoord.x <= uRect.x + uRect.z
             && vTexCoord.y >= uRect.y && vTexCoord.y <= uRect.y + uRect.w;
  if (uInvert > 0.5) inside = !inside;
  if (!inside) { fragColor = vec4(0.0); return; }   // 矩形外：输出全透明（alpha=0，混合下等同被裁掉）
  fragColor = vec4(c.rgb, c.a);
}";

        private static Shader? _shader;

        /// <summary>确保 Shader 资产已用给定设备编译一次（懒编译、共享 Program）。</summary>
        private static Shader Ensure(GraphicsDevice device)
        {
            if (_shader is null)
            {
                _shader = new Shader(ShaderName, null, Fragment, device);
                _shader.Defaults.SetFloat("uInvert", 0f);
            }
            return _shader;
        }

        /// <summary>
        /// 创建矩形遮罩材质（Shader 版）。<paramref name="rect"/> 为被画元素自身四边形的局部 UV 矩形
        /// <c>(x, y, w, h)</c>，范围 0..1，例如 <c>(0,0,0.5,1)</c> 只显示左半。
        /// </summary>
        public static Material CreateMaterial(GraphicsDevice device, Vector4 rect, bool invert = false)
        {
            ShaderEffect effect = Ensure(device).CreateEffectInstance();
            ApplyRect(effect, rect, invert);
            return new Material { Effect = effect, Blend = BlendState.NonPremultiplied };
        }

        /// <summary>
        /// 上层给材质里的 <see cref="ShaderEffect"/> 赋值（照用户要求：C# 侧统一设置矩形遮罩的 uniform）。
        /// 设置 <c>uRect</c>（局部 UV 矩形）与 <c>uInvert</c>。
        /// </summary>
        public static void ApplyRect(ShaderEffect effect, Vector4 rect, bool invert = false)
        {
            effect.SetVector("uRect", rect);
            effect.SetFloat("uInvert", invert ? 1f : 0f);
        }
    }
}
