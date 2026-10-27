using System;
using System.Collections.Generic;
using System.Numerics;
using KFramework.MonoGame;

namespace KFramework.MonoGameExtend
{
    /// <summary>
    /// 统一遮罩静态类：把"剪刀 / 模板 / Alpha / 矩形"四类遮罩的<b>材质配置方法</b>都归拢到这一个类里，
    /// 同一个材质可叠加多种遮罩能力（例如：底着合并 Shader 做 alpha+rect，再开 scissor、再叠模板）。
    /// <para>
    /// 两类"设置"分属不同层，但都在本类里：<br/>
    /// ① <b>Shader 层（作用于材质的 <see cref="ShaderEffect"/>）</b>：Alpha 遮罩贴图 / Mask ST / 反相 / 矩形 UV（用宏变体 + uniform，见 <see cref="MaskFeature"/>、<see cref="CreateMaterial"/>、SetMask* / SetRect*）。<br/>
    /// ② <b>渲染状态层（作用于材质本身）</b>：剪刀测试（<see cref="SetScissorTest"/> + <see cref="ApplyScissor"/>）、模板（<see cref="SetStencil"/>，两遍 Pass）。
    /// </para>
    /// <para>三后端通用说明：WebGL 真正编译各 Shader 变体并下发模板/剪刀；WebGPU 的 SetScissor/ApplyDepthStencil 当前为 no-op、自定义着色器回落默认；
    /// Canvas2D 无深度/模板。故"Alpha+矩形"最通用，"剪刀/模板"在 WebGL 下最稳。</para>
    /// <example>
    /// <code>
    /// // 1) 建一个"全功能"材质（按需开启宏变体，运行时再用 setter 开关）
    /// var mat = Mask.CreateMaterial(device, MaskFeature.Alpha | MaskFeature.Rect);
    /// // 2) 设置 Alpha 遮罩（贴图 + 图集子矩形换算成 uMask_ST）
    /// Mask.SetMaskTexture(mat.Effect, maskAtlas);
    /// Mask.SetMaskAtlasRect(mat.Effect, maskAtlas, new Rectangle(x, y, w, h));
    /// Mask.SetMaskInvert(mat.Effect, false);
    /// Mask.SetMaskEnabled(mat.Effect, true);
    /// // 3) 设置矩形遮罩（局部 UV 矩形，只显示左半）
    /// Mask.SetRect(mat.Effect, new Vector4(0f, 0f, 0.5f, 1f));
    /// Mask.SetRectEnabled(mat.Effect, true);
    /// // 4) 叠加剪刀测试（屏幕级）
    /// Mask.SetScissorTest(mat, true);
    /// Mask.ApplyScissor(device, clipRect);   // 绘制前设矩形；绘制后 Mask.ClearScissor(device)
    /// // 5) 或叠加模板（两遍：先 Write 画遮罩形状，再 Test 画内容）
    /// Mask.SetStencil(mat, StencilMode.Write);
    /// </code>
    /// </example>
    /// </summary>
    public static class Mask
    {
        /// <summary>Shader 遮罩特性组合（编译期宏变体，可按位叠加）。</summary>
        [Flags]
        public enum MaskFeature : byte
        {
            /// <summary>无 Shader 遮罩（直通：正常画内容）。</summary>
            None = 0,
            /// <summary>Alpha 遮罩：主图按遮罩图 Alpha 控可见度（需配 uMask + uMask_ST）。</summary>
            Alpha = 1,
            /// <summary>矩形遮罩（Shader 版）：按元素局部 UV 矩形 clip 像素（需配 uRect）。</summary>
            Rect = 2,
        }

        /// <summary>模板遮罩模式（材质 DepthStencilState 两遍 Pass 的取值）。</summary>
        public enum StencilMode : byte
        {
            /// <summary>关闭模板。</summary>
            None = 0,
            /// <summary>写模板：深度关 + Always/Replace，把参考值 1 写入遮罩形状覆盖到的像素。</summary>
            Write = 1,
            /// <summary>测模板：深度关 + Equal/Keep，只在模板 == 1 处绘制（即只显示遮罩形状内）。</summary>
            Test = 2,
        }

        /// <summary>Shader 资产名前缀（照 Unity 资源路径风格，各变体名 = 前缀 + "_" + 特性组合，可用 <see cref="Shader.Find"/> 按名取）。</summary>
        public const string ShaderName = "KFramework/Mask/Combined";

        // 严格遵循引擎契约：声明 in vec2 vTexCoord / in vec4 vColor；#version 300 es 必须第一行；precision highp float；out vec4 fragColor。
        // 每个 sampler 都配一个 *_ST（Unity 约定，纹理可能是图集图）。主图 uTexture 的 UV 已由 SpriteBatch 映射，不叠 _ST。
        // 用 #ifdef ALPHA_MASK / RECT_MASK 切换不同遮罩分支（编译期宏，等价于 Unity 的 shader_feature 变体）。
        private const string VersionLine = "#version 300 es\n";

        private const string Body = @"precision highp float;
in vec2 vTexCoord;
in vec4 vColor;
uniform sampler2D uTexture;   // 主图（0 号单元，SpriteBatch 自动绑）
#ifdef ALPHA_MASK
uniform sampler2D uMask;      // 遮罩图（1 号单元，SetMaskTexture 绑）
uniform vec4  uMask_ST;       // 遮罩图缩放/偏移（照 Unity _MainTex_ST：xy=tiling, zw=offset）
uniform float uInvert;        // 1=反相（遮罩 Alpha 越低越可见）
uniform float uMaskEnabled;   // 1=启用 Alpha 遮罩（运行时开关）
#endif
#ifdef RECT_MASK
uniform vec4  uRect;          // 本精灵四边形局部 UV 矩形 (x, y, w, h)，0..1
uniform float uRectInvert;    // 1=反相（矩形外可见、矩形内被裁）
uniform float uRectEnabled;   // 1=启用矩形裁切（运行时开关）
#endif
out vec4 fragColor;
void main(){
  vec4 c = texture(uTexture, vTexCoord) * vColor;
#ifdef RECT_MASK
  if (uRectEnabled > 0.5) {
    bool inside = vTexCoord.x >= uRect.x && vTexCoord.x <= uRect.x + uRect.z
               && vTexCoord.y >= uRect.y && vTexCoord.y <= uRect.y + uRect.w;
    if (uRectInvert > 0.5) inside = !inside;
    if (!inside) { fragColor = vec4(0.0); return; }   // 矩形外：输出全透明（混合下等同被裁掉）
  }
#endif
#ifdef ALPHA_MASK
  if (uMaskEnabled > 0.5) {
    // 照 Unity TRANSFORM_TEX：maskUV = vTexCoord * uMask_ST.xy + uMask_ST.zw（遮罩图可是图集子矩形）
    vec2 maskUV = vTexCoord * uMask_ST.xy + uMask_ST.zw;
    float a = texture(uMask, maskUV).a;
    float m = (uInvert > 0.5) ? (1.0 - a) : a;
    c.a *= m;
  }
#endif
  fragColor = vec4(c.rgb, c.a);
}";

        // 按"特性组合"缓存已编译的共享 Shader 变体（每种组合只编译一次）。
        private static readonly Dictionary<MaskFeature, Shader> Variants = new();

        /// <summary>取得（必要时编译）对应 <paramref name="features"/> 的 Shader 变体。</summary>
        private static Shader GetVariant(GraphicsDevice device, MaskFeature features)
        {
            if (Variants.TryGetValue(features, out Shader? s) && s != null) return s;
            string defines = string.Empty;
            if ((features & MaskFeature.Alpha) != 0) defines += "#define ALPHA_MASK\n";
            if ((features & MaskFeature.Rect) != 0) defines += "#define RECT_MASK\n";
            string frag = VersionLine + defines + Body;
            var shader = new Shader(ShaderName + "_" + features, null, frag, device);
            // 统一默认 uniform（未启用对应分支的变体会忽略这些同名项）：
            shader.Defaults.SetVector("uMask_ST", new Vector4(1f, 1f, 0f, 0f));
            shader.Defaults.SetFloat("uInvert", 0f);
            shader.Defaults.SetFloat("uMaskEnabled", 1f);
            shader.Defaults.SetFloat("uRectInvert", 0f);
            shader.Defaults.SetFloat("uRectEnabled", 1f);
            Variants[features] = shader;
            return shader;
        }

        // ===================== 创建材质 =====================

        /// <summary>
        /// 创建合并遮罩材质：按 <paramref name="features"/> 挑（或编译）对应宏变体，返回一个带该 Shader 效果的材质。
        /// 之后用本类的 Set* 方法就地配置 scissor / stencil / mask / rect 等。
        /// </summary>
        /// <param name="device">图形设备（首次用到某变体时编译）。</param>
        /// <param name="features">要启用的 Shader 遮罩组合（<see cref="MaskFeature.Alpha"/> / <see cref="MaskFeature.Rect"/> 可按位叠加）。</param>
        public static Material CreateMaterial(GraphicsDevice device, MaskFeature features = MaskFeature.None)
        {
            ShaderEffect effect = GetVariant(device, features).CreateEffectInstance();
            return new Material { Effect = effect, Blend = BlendState.NonPremultiplied };
        }

        // ===================== 剪刀测试（材质 Rasterizer + 设备裁剪矩形）=====================

        /// <summary>设置材质的剪刀测试开关（写入 <see cref="Material.Rasterizer"/>，保留已有的 CullMode；不影响模板状态）。</summary>
        public static void SetScissorTest(Material material, bool enabled)
        {
            RasterizerState r = material.Rasterizer ?? RasterizerState.CullNone;
            material.Rasterizer = new RasterizerState { CullMode = r.CullMode, ScissorTestEnable = enabled };
        }

        /// <summary>绘制前：把设备裁剪矩形设为 <paramref name="rect"/>（屏幕左上原点、Y 向下）。需配合 <see cref="SetScissorTest"/>(material, true)。</summary>
        public static void ApplyScissor(GraphicsDevice device, Rectangle rect)
            => device.SetScissor(rect.X, rect.Y, rect.Width, rect.Height);

        /// <summary>绘制后：把设备裁剪矩形恢复到整个视口（等于不裁剪）。</summary>
        public static void ClearScissor(GraphicsDevice device)
            => device.ClearScissor();

        // ===================== 模板（材质 DepthStencilState，两遍 Pass）=====================

        /// <summary>
        /// 设置材质的模板模式（写入 <see cref="Material.DepthStencil"/>，并保留已有的 ScissorTest 标志）。
        /// 两遍用法：第一遍用 <see cref="StencilMode.Write"/> 画遮罩形状，第二遍用 <see cref="StencilMode.Test"/> 画内容。
        /// </summary>
        public static void SetStencil(Material material, StencilMode mode)
        {
            RasterizerState r = material.Rasterizer ?? RasterizerState.CullNone;
            material.Rasterizer = new RasterizerState { CullMode = r.CullMode, ScissorTestEnable = r.ScissorTestEnable };
            material.DepthStencil = mode switch
            {
                StencilMode.Write => new DepthStencilState(DepthState.Disabled,
                    new StencilState(true, 1, 0xFF, 0xFF, CompareFunction.Always, StencilOp.Replace)),
                StencilMode.Test => new DepthStencilState(DepthState.Disabled,
                    new StencilState(true, 1, 0xFF, 0xFF, CompareFunction.Equal, StencilOp.Keep)),
                _ => DepthStencilState.None,
            };
        }

        // ===================== Alpha / 矩形：作用于材质的 ShaderEffect（uniform）=====================

        /// <summary>设置 Alpha 遮罩贴图（绑到 uMask，1 号单元）。仅该材质用 <see cref="MaskFeature.Alpha"/> 变体时生效。</summary>
        public static void SetMaskTexture(ShaderEffect effect, Texture2D mask)
            => effect.SetTexture("uMask", mask);

        /// <summary>直接设置遮罩图的缩放/偏移 <c>uMask_ST</c>（Unity _MainTex_ST：xy=tiling，zw=offset）。</summary>
        public static void SetMaskST(ShaderEffect effect, Vector4 st)
            => effect.SetVector("uMask_ST", st);

        /// <summary>
        /// 设置遮罩图（及它在图集里的子矩形，像素，相对纹理自身左上原点）；内部把像素矩形换算成 <c>uMask_ST</c>。
        /// null 矩形表示整张图 1:1（uMask_ST = identity）。
        /// </summary>
        public static void SetMaskAtlasRect(ShaderEffect effect, Texture2D mask, Rectangle? maskAtlasRect = null)
            => effect.SetVector("uMask_ST", AtlasRectToST(mask, maskAtlasRect));

        /// <summary>设置 Alpha 反相（uInvert）：true 时遮罩 Alpha 越低越可见。</summary>
        public static void SetMaskInvert(ShaderEffect effect, bool invert)
            => effect.SetFloat("uInvert", invert ? 1f : 0f);

        /// <summary>运行时开关 Alpha 遮罩（uMaskEnabled）：false 时本材质不做 Alpha 遮罩（即便变体含 Alpha）。</summary>
        public static void SetMaskEnabled(ShaderEffect effect, bool enabled)
            => effect.SetFloat("uMaskEnabled", enabled ? 1f : 0f);

        /// <summary>一次性设置 Alpha 遮罩相关 uniform（贴图 + 图集子矩形 + 反相 + 开关）。</summary>
        public static void ApplyMask(ShaderEffect effect, Texture2D mask, Rectangle? maskAtlasRect = null, bool invert = false, bool enabled = true)
        {
            SetMaskTexture(effect, mask);
            SetMaskAtlasRect(effect, mask, maskAtlasRect);
            SetMaskInvert(effect, invert);
            SetMaskEnabled(effect, enabled);
        }

        /// <summary>设置矩形遮罩的局部 UV 矩形 uRect（x,y,w,h，0..1；例如 (0,0,0.5,1) 只显示左半）。</summary>
        public static void SetRect(ShaderEffect effect, Vector4 rect)
            => effect.SetVector("uRect", rect);

        /// <summary>设置矩形反相（uRectInvert）：true 时矩形外可见、矩形内被裁。</summary>
        public static void SetRectInvert(ShaderEffect effect, bool invert)
            => effect.SetFloat("uRectInvert", invert ? 1f : 0f);

        /// <summary>运行时开关矩形裁切（uRectEnabled）：false 时本材质不做矩形裁切（即便变体含 Rect）。</summary>
        public static void SetRectEnabled(ShaderEffect effect, bool enabled)
            => effect.SetFloat("uRectEnabled", enabled ? 1f : 0f);

        /// <summary>一次性设置矩形遮罩相关 uniform（矩形 + 反相 + 开关）。</summary>
        public static void ApplyRect(ShaderEffect effect, Vector4 rect, bool invert = false, bool enabled = true)
        {
            SetRect(effect, rect);
            SetRectInvert(effect, invert);
            SetRectEnabled(effect, enabled);
        }

        /// <summary>
        /// 把图集子矩形（像素，相对纹理自身左上原点）换算成 Unity 风格的 <c>_ST</c>：
        /// xy = tiling = (rect.w / W, rect.h / H)，zw = offset = (rect.x / W, rect.y / H)，均归一化到 0..1。
        /// 注：若遮罩图在引擎里的 V 方向与约定相反，调用方可在传入前翻转 rect.Y 或结果 .W。
        /// </summary>
        private static Vector4 AtlasRectToST(Texture2D? mask, Rectangle? rect)
        {
            if (mask == null || rect is null) return new Vector4(1f, 1f, 0f, 0f);
            float iw = 1f / mask.Width;
            float ih = 1f / mask.Height;
            return new Vector4(rect.Value.Width * iw, rect.Value.Height * ih, rect.Value.X * iw, rect.Value.Y * ih);
        }
    }
}
