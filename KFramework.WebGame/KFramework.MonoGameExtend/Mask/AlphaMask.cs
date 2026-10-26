using System.Numerics;
using KFramework.MonoGame;

namespace KFramework.MonoGameExtend.Mask
{
    /// <summary>
    /// Alpha 遮罩（照 Unity 风格的 Shader 资产）：主图按遮罩图的 Alpha 通道控制可见度（遮罩 Alpha=0 处完全透明）。
    /// <para>
    /// 遮罩图通过 <c>uMask_ST</c>（缩放 / 偏移，等价于 Unity 的 <c>_MainTex_ST</c>：xy = tiling，zw = offset）映射到主图 UV，
    /// 因此遮罩图本身可以是图集里的子矩形（任何外部传入的纹理都可能是图集图，故按 Unity 约定给每个 sampler 配一个 *_ST）。
    /// 主图 <c>uTexture</c> 的 UV 已由 SpriteBatch 映射到它自己的图集子矩形，故不再叠加 _ST。
    /// </para>
    /// <para>三后端通用：WebGL 真正编译生效；WebGPU / Canvas2D 自定义着色器回落默认精灵着色器（无此遮罩效果）。</para>
    /// <example>
    /// <code>
    /// // 简单：遮罩图 1:1 映射主图 UV
    /// var mat = AlphaMask.CreateMaterial(device, maskTex);
    /// batch.Begin(mat); batch.Draw(content, ...); batch.End();
    ///
    /// // 复杂：遮罩图是图集里的子矩形（像素坐标，相对遮罩图自身），统一换算成 uMask_ST
    /// var mat = AlphaMask.CreateMaterial(device, maskAtlas, new Rectangle(x, y, w, h));
    /// batch.Begin(mat); batch.Draw(content, ...); batch.End();
    ///
    /// // 已有 ShaderEffect 时，C# 侧单独赋值遮罩相关 uniform
    /// AlphaMask.ApplyMask(myEffect, maskAtlas, new Rectangle(x, y, w, h), invert: false);
    /// </code>
    /// </example>
    /// </summary>
    public static class AlphaMask
    {
        /// <summary>Shader 资产名（照 Unity 资源路径风格，可用 <see cref="Shader.Find"/> 按名取）。</summary>
        public const string ShaderName = "KFramework/Mask/AlphaMask";

        // 严格遵循引擎契约：声明 in vec2 vTexCoord / in vec4 vColor；#version 300 es；precision highp float；out vec4 fragColor。
        // 每个 sampler 都配一个 *_ST（Unity 约定）。主图 uTexture 的 UV 已由 SpriteBatch 映射到其图集子矩形，故不叠 _ST。
        private const string Fragment = 
@"#version 300 es
precision highp float;
in vec2 vTexCoord;
in vec4 vColor;
uniform sampler2D uTexture;   // 主图（0 号单元，SpriteBatch 自动绑）
uniform sampler2D uMask;      // 遮罩图（1 号单元，ApplyMask 里 SetTexture 绑）
uniform vec4  uMask_ST;       // 遮罩图缩放/偏移（照 Unity _MainTex_ST：xy=tiling, zw=offset）
uniform float uMaskEnabled;   // 1=启用遮罩 0=不遮罩（默认 0，避免未配置时采样空纹理）
uniform float uInvert;        // 1=反相（遮罩 Alpha 越低越可见）
out vec4 fragColor;
void main(){
  vec4 c = texture(uTexture, vTexCoord) * vColor;
  float m = 1.0;
  if (uMaskEnabled > 0.5) {
    // 照 Unity TRANSFORM_TEX：maskUV = vTexCoord * uMask_ST.xy + uMask_ST.zw
    vec2 maskUV = vTexCoord * uMask_ST.xy + uMask_ST.zw;
    float a = texture(uMask, maskUV).a;
    m = (uInvert > 0.5) ? (1.0 - a) : a;
  }
  // 非预乘混合：直接输出 (rgb, a*m)
  fragColor = vec4(c.rgb, c.a * m);
}";

        private static Shader? _shader;

        /// <summary>确保 Shader 资产已用给定设备编译一次（懒编译、共享 Program）。</summary>
        private static Shader Ensure(GraphicsDevice device)
        {
            if (_shader is null)
            {
                _shader = new Shader(ShaderName, null, Fragment, device);
                // 默认 uniform（每个 CreateEffectInstance 会完整克隆，再从"全默认"起步）。
                _shader.Defaults.SetVector("uMask_ST", new Vector4(1f, 1f, 0f, 0f));
                _shader.Defaults.SetFloat("uMaskEnabled", 0f);
                _shader.Defaults.SetFloat("uInvert", 0f);
            }
            return _shader;
        }

        /// <summary>创建 Alpha 遮罩材质（遮罩图 1:1 映射主图 UV，即 uMask_ST = identity）。</summary>
        public static Material CreateMaterial(GraphicsDevice device, Texture2D mask, bool invert = false)
            => CreateMaterial(device, mask, null, invert);

        /// <summary>
        /// 创建 Alpha 遮罩材质，并指定遮罩图在图集里的子矩形 <paramref name="maskAtlasRect"/>（像素，相对遮罩图自身左上原点）。
        /// 内部换算成 Unity 风格的 <c>uMask_ST</c>（xy = 缩放 = rect.w/h，zw = 偏移 = rect.x/y，均归一化到 0..1）。
        /// </summary>
        public static Material CreateMaterial(GraphicsDevice device, Texture2D mask, Rectangle? maskAtlasRect, bool invert = false)
        {
            ShaderEffect effect = Ensure(device).CreateEffectInstance();
            ApplyMask(effect, mask, maskAtlasRect, invert);
            return new Material { Effect = effect, Blend = BlendState.NonPremultiplied };
        }

        /// <summary>
        /// 上层给材质里的 <see cref="ShaderEffect"/> 赋值（照用户要求：C# 侧统一设置遮罩相关 uniform）。
        /// 设置 <c>uMask</c> 纹理、<c>uMask_ST</c>（图集子矩形 → Unity 风格缩放偏移）、<c>uInvert</c>、<c>uMaskEnabled</c>。
        /// </summary>
        /// <param name="effect">目标效果（通常由 <see cref="Shader.CreateEffectInstance"/> 产出）。</param>
        /// <param name="mask">遮罩纹理（按 Alpha 决定可见度）。</param>
        /// <param name="maskAtlasRect">遮罩图上的子矩形（像素，相对纹理自身左上原点）；null = 整张图 1:1。</param>
        /// <param name="invert">是否反相：true 时遮罩 Alpha 越低越可见。</param>
        public static void ApplyMask(ShaderEffect effect, Texture2D mask, Rectangle? maskAtlasRect = null, bool invert = false)
        {
            effect.SetTexture("uMask", mask);
            effect.SetVector("uMask_ST", AtlasRectToST(mask, maskAtlasRect));
            effect.SetFloat("uInvert", invert ? 1f : 0f);
            effect.SetFloat("uMaskEnabled", 1f);
        }

        /// <summary>
        /// 把图集子矩形（像素，相对纹理自身左上原点）换算成 Unity 风格的 <c>_ST</c>：
        /// xy = tiling = (rect.w / W, rect.h / H)，zw = offset = (rect.x / W, rect.y / H)，均归一化到 0..1。
        /// 注：若遮罩图在引擎里的 V 方向与约定相反，调用方可在传入前翻转 rect.Y 或结果 .W。
        /// </summary>
        private static Vector4 AtlasRectToST(Texture2D mask, Rectangle? rect)
        {
            if (mask == null || rect is null) return new Vector4(1f, 1f, 0f, 0f);
            float iw = 1f / mask.Width;
            float ih = 1f / mask.Height;
            return new Vector4(rect.Value.Width * iw, rect.Value.Height * ih, rect.Value.X * iw, rect.Value.Y * ih);
        }
    }
}
