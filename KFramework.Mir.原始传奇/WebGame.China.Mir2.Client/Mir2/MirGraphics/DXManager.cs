using System;
using Client.MirControls;
using Client.MirScenes;
using KFramework.MonoGame;
using MirEngine;
using SlimDX;
using SlimDX.Direct3D9;

// 让 Point/Color/Rectangle 默认指向 MirEngine（游戏逻辑层使用的类型）；KFramework.MonoGame 的同名类型一律全限定。
using Point = MirEngine.Point;
using Color = MirEngine.Color;
using Rectangle = MirEngine.Rectangle;
using WebGame.Mir2.MonoGame.Client;

namespace Client.MirGraphics
{
    // KFramework.MonoGame 版 DXManager：主精灵路径走 GraphicsDevice / SpriteBatch（Texture2D 承载），
    // 离屏渲染目标由 KFramework.MonoGame.RenderTarget2D（真·FBO）承载——CreateRenderTarget / SetSurface /
    // DrawOpaque(ControlTexture) 现在都会真正生效，GameScene 的地板 / 光照烘焙可正常合成。
    // 控制 ControlTexture 的 SlimDX 壳（Shims/SlimDX.cs 的 Texture/Surface）负责把调用转到底层 RenderTarget2D。
    class DXManager
    {
        public static List<MImage> TextureList = new List<MImage>();
        public static List<MirControl> ControlList = new List<MirControl>();

        public static float Opacity = 1F;

        // 全屏自适应：场景烘焙时注入的全局缩放变换（按屏幕高度统一缩放，参考 Unity 的
        // Scale-With-Screen-Size / Match=Height）。仅在 CreateTexture 烘焙期间置位，
        // PresentToScreen 上屏前必须清空，否则会把整帧再缩放一次（双重缩放/错位）。
        public static KFramework.MonoGame.Matrix4x4? RenderTransform = null;

        public static bool Blending;
        public static float BlendingRate;
        public static BlendMode BlendingMode;

        public static SlimDX.Direct3D9.Sprite Sprite = new SlimDX.Direct3D9.Sprite();
        public static SlimDX.Direct3D9.Line Line = new SlimDX.Direct3D9.Line();

        // 兼容原版占位（Forms/MirScene/GameScene 仍有 DXManager.Device.Clear / new Texture(DXManager.Device,...) 等调用）。
        public static SlimDX.Direct3D9.Device Device = new SlimDX.Direct3D9.Device();
        public static SlimDX.Direct3D9.Surface CurrentSurface;

        public static bool GrayScale;

        // 光照 / 雷达 / 地板等特效资源占位（浏览器端为可选视觉，暂以空纹理承载，不阻塞编译与基本运行）。
        public static SlimDX.Direct3D9.Texture RadarTexture;
        public static List<SlimDX.Direct3D9.Texture> Lights = new List<SlimDX.Direct3D9.Texture>();
        public static SlimDX.Direct3D9.Texture PoisonDotBackground;
        public static SlimDX.Direct3D9.Texture FloorTexture, LightTexture;
        public static SlimDX.Direct3D9.Surface FloorSurface, LightSurface;
        public static Point[] LightSizes =
        {
            new Point(125,95), new Point(205,156), new Point(285,217), new Point(365,277),
            new Point(445,338), new Point(525,399), new Point(605,460), new Point(685,521),
            new Point(765,581), new Point(845,642), new Point(925,703)
        };

        public static int DPSCounter;

        // 权威“全屏/窗口原生分辨率”尺寸：所有世界层（地板/地图/光照）与 UI 层渲染目标都以它为准，
        // 避免各处直接用 GDevice.Viewport 在某些宿主（浏览器/画布）下读到逻辑尺寸导致地图只铺左上角。
        // 优先用后备缓冲（BackBuffer）尺寸——它是真正的窗口像素，不随烘焙期临时改小的 Viewport 变化。
        public static MirEngine.Size FullScreenSize
        {
            get
            {
                var pp = GDevice?.PresentationParameters;
                int w = pp != null && pp.BackBufferWidth > 0 ? pp.BackBufferWidth : GDevice.Viewport.Width;
                int h = pp != null && pp.BackBufferHeight > 0 ? pp.BackBufferHeight : GDevice.Viewport.Height;
                return new MirEngine.Size(w, h);
            }
        }

        // KFramework.MonoGame 渲染后端（由 MirGame.Initialize 注入）。
        public static GraphicsDevice GDevice;
        // 主精灵批（对齐原版 DXManager 的主力绘制器）。文本渲染（DrawTextBox / IFont.Draw）同样吃这一支批。
        public static SpriteBatch Batch;
        public static void Initialize(GraphicsDevice device, SpriteBatch batch)
        {
            GDevice = device;
            Batch = batch;

            // 字体描述符 Font 作为 IFont 时，由 FontFactory 按本设备解析（缓存）真实字形资源 SpriteFont。
            MirEngine.FontFactory.Device = device;

            // 浏览器/WebGL 宿主下 GDevice.Viewport 常读到 0 或“逻辑尺寸”，而后备缓冲(BackBuffer)才是真实画布像素
            // （这也是 FullScreenSize 优先用 BackBuffer 的原因）。Settings.ScreenWidth/Height 直接读 GDevice.Viewport，
            // 若不校正，控件构造/布局时会拿到 0 → Recenter 把对话框定位到负坐标、且 MirControl.Draw 守卫
            // （Size.Width > Settings.ScreenWidth）直接跳过绘制 → 整屏 UI（登录/选人/游戏 UI）全不可见。
            // 这里把 Viewport 显式校正为后备缓冲尺寸，让全屏值在任何时刻都正确。
            var pp = GDevice.PresentationParameters;
            if (pp != null && pp.BackBufferWidth > 0 && pp.BackBufferHeight > 0)
            {
                var vp = GDevice.Viewport;
                vp.X = 0; vp.Y = 0;
                vp.Width = pp.BackBufferWidth;
                vp.Height = pp.BackBufferHeight;
                GDevice.Viewport = vp;
            }

            // 嵌套 RT 合成：切回画布（SetRenderTarget(null)）时保留已合成内容，避免闪烁。
            // 清屏由每帧 CMain.RenderEnvironment 的显式 Clear 负责；默认 DiscardContents 保持 XNA/MonoGame 兼容。
            GDevice.PresentationParameters.RenderTargetUsage = RenderTargetUsage.PreserveContents;

            // 浏览器端程序化生成灯贴图（光晕），填充 DXManager.Lights，使昼夜系统的蜡烛/火把/地图灯能照亮。
            CreateLights();
        }

        // 程序化生成灯贴图（光晕）填充 Lights：传统桌面实现用 GDI+ PathGradientBrush 绘制径向渐变，
        // 浏览器端 WebGL 无 GDI+，改为 CPU 逐像素生成等价光晕（中心白→边缘透明），按 LightSizes 的 15 档生成 14 张。
        public static void CreateLights()
        {
            if (GDevice == null) return;
            for (int i = Lights.Count - 1; i >= 0; i--)
                Lights[i]?.Dispose();
            Lights.Clear();

            float[] positions = { 0f, .20f, .40f, .60f, .80f, 1.0f };
            // 径向渐变停靠色（A,R,G,B），中心白、边缘透明，与桌面端 GDI+ 的 PathGradientBrush 一致。
            byte[][] swatches = new byte[][]
            {
                new byte[] { 255, 255, 255, 255 },
                new byte[] { 255, 210, 210, 210 },
                new byte[] { 255, 160, 160, 160 },
                new byte[] { 255, 70, 70, 70 },
                new byte[] { 255, 40, 40, 40 },
                new byte[] { 0, 0, 0, 0 },
            };

            for (int i = 1; i < LightSizes.Length; i++)
            {
                int w = LightSizes[i].X, h = LightSizes[i].Y;
                byte[] data = new byte[w * h * 4];
                float cx = w / 2f, cy = h / 2f;
                float rx = w / 2f, ry = h / 2f;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float nx = (x + 0.5f - cx) / rx;
                        float ny = (y + 0.5f - cy) / ry;
                        float r = (float)System.Math.Sqrt(nx * nx + ny * ny);
                        int idx = (y * w + x) * 4;
                        if (r > 1f) continue; // 椭圆外保持透明

                        int k = 0;
                        while (k < positions.Length - 1 && r > positions[k + 1]) k++;
                        float t = (r - positions[k]) / (positions[k + 1] - positions[k]);
                        byte[] a = swatches[k], b = swatches[k + 1];
                        data[idx]     = (byte)(a[1] + (b[1] - a[1]) * t); // R
                        data[idx + 1] = (byte)(a[2] + (b[2] - a[2]) * t); // G
                        data[idx + 2] = (byte)(a[3] + (b[3] - a[3]) * t); // B
                        data[idx + 3] = (byte)(a[0] + (b[0] - a[0]) * t); // A
                    }
                }

                var tex = new SlimDX.Direct3D9.Texture(Device, w, h, 1, Usage.None, Format.A8R8G8B8, Pool.Managed);
                tex.PlainTexture = GDevice.CreateTexture(w, h, data);
                Lights.Add(tex);
            }
        }

        public static void Create()
        {
            // 旧 TextRenderer 的 SetBlendProfile 混合档位已不再需要：混合语义由下方 Draw 按 Blending 标志选择。
        }

        static KFramework.MonoGame.Color ToColor(SlimDX.Color4 c) =>
            new KFramework.MonoGame.Color((byte)(c.Red * 255), (byte)(c.Green * 255), (byte)(c.Blue * 255), (byte)(c.Alpha * 255));

        static KFramework.MonoGame.Color ToColor(Color c) =>
            new KFramework.MonoGame.Color(c.R, c.G, c.B, c.A);

        static KFramework.MonoGame.Rectangle ToRect(Rectangle r) =>
            new KFramework.MonoGame.Rectangle(r.X, r.Y, r.Width, r.Height);

        // —— 主精灵路径：Texture2D（KFramework.MonoGame）——
        // 批次控制对齐原版 Sprite.Begin / Sprite.End / Sprite.Flush（原版 D3D9 固定管线）：
        //   Sprite.Begin(flags)  可重复调用（已处于 Begin 时忽略）—— 原版在 CMain.RenderEnvironment 与 DXManager.Draw 各调一次；
        //   Sprite.End()         收批（幂等）—— 换混合（SetBlend）、异常恢复（AttemptRecovery）、帧末各调一次；
        //   Sprite.Flush()       提交已排队绘制但不收批 —— 切渲染目标（SetSurface）前调用。
        // 本引擎 SpriteBatch 严格配对、且材质/投影在 Begin 时捕获，故用下面三个方法等价实现：
        //   BeginSprite —— 材质 / 世界变换没变则忽略（= 原版容忍的重复 Begin）；变了先 End 再 Begin（原版这一步在 SetBlend 里）。
        //   EndSprite   —— 收批；FlushSprite —— 提交（本引擎只能靠 End 提交）。
        //
        // 安全性依据：所有离屏绘制（MirLabel 文本、MirTextBox、控件/层烘焙）都经 DXManager.SetSurface
        // 切目标，而 SetSurface 会先 FlushSprite，故不会出现"同目标内批次交错提交"的乱序。
        private static bool _batchBegun;
        private static KFramework.MonoGame.Material _batchMaterial;
        private static KFramework.MonoGame.Matrix4x4? _batchTransform;

        // 复用的材质（引擎主力 API：SpriteBatch.Begin(Material, ...)）。按 BlendState 缓存，
        // 避免每次 Begin 都 new Material；材质状态在 Begin 时一次性下发，批内不变。
        private static readonly Dictionary<KFramework.MonoGame.BlendState, KFramework.MonoGame.Material> _materials
            = new Dictionary<KFramework.MonoGame.BlendState, KFramework.MonoGame.Material>();

        // 统一混合见 Client.MirGraphics.MirBlend 枚举（MirBlend → 引擎 BlendState 由 MirBlendExtensions.ToBlendState 转换）。
        // 游戏代码一律引用 MirBlend，不要再直接写 BlendState / 本类的 NormalBlend、AdditiveBlend。

        // 取某混合模式对应的复用材质（新式材质驱动 API 的入口：SpriteFlags.AlphaBlend 之类的 flags 用法一律改走这里）。
        public static KFramework.MonoGame.Material GetMaterial(KFramework.MonoGame.BlendState blend)
        {
            if (!_materials.TryGetValue(blend, out var mat))
            {
                mat = new KFramework.MonoGame.Material
                {
                    Blend = blend,
                    Sampler = KFramework.MonoGame.SamplerState.PointClamp,
                    DepthStencil = KFramework.MonoGame.DepthStencilState.None,
                    Rasterizer = KFramework.MonoGame.RasterizerState.CullNone,
                };
                _materials[blend] = mat;
            }
            return mat;
        }

        // —— 像素级后效（灰度 / 染色 / 魔法 / 淡出）：对齐原版 Crystal 的 Device.PixelShader（灰度 / Normal / Magic 三套 .ps）——
        // 原版用 D3D9 像素着色器（颜色后效）+ 全局混合因子（SetOpacity 的半透明）。本移植改用引擎 Material.Effect
        // （ShaderEffect，GLSL ES 3.00；仅 WebGL 后端真正生效，WebGPU 回落默认精灵着色器，页面照常只是无后效）。
        // 颜色后效（灰度 / 染色 / 魔法）三者互斥、共用一个“颜色后效槽” _colorEffect；全局不透明度 _opacity 与之正交叠加
        // （premultiplied 约定下只需把 alpha 乘 uOpacity，下游混合按 alpha 预乘 rgb，等于整层按 uOpacity 缩放）。
        // 后效资产：用引擎 Shader 类登记并编译一次（构造即编译、共享 Program）。类名 / 路径照 Unity 风格，
        // 可用 KFramework.MonoGame.Shader.Find("Mir2/...") 按名取。
        private static KFramework.MonoGame.Shader _grayscaleShader;
        private static KFramework.MonoGame.Shader _tintShader;     // Normal + Magic 共用（同片元源码）
        private static KFramework.MonoGame.Shader _fadeShader;     // 仅淡出（不改色）

        // 按后效状态持有的 ShaderEffect 实例：各自独立 uniform 字典，但包同一份共享 Program（由 Shader.CreateEffectInstance 产出）。
        private static KFramework.MonoGame.ShaderEffect _grayscaleEffect;
        private static KFramework.MonoGame.ShaderEffect _tintEffect;    // Normal / Magic 共用同一实例（互斥，每帧重写 uTint/uOpacity）
        private static KFramework.MonoGame.ShaderEffect _fadeEffect;

        // 后效状态：_colorEffect 为颜色后效槽（0 无；1 灰度；2 normal 染色；3 magic 染色）；_opacity 全局不透明度。
        private static int _colorEffect;
        private static System.Numerics.Vector4 _tintParam; // rgb = 染色色，a = 混合比例
        private static float _opacity = 1f;

        // 按 (混合, 后效) 缓存的材质：后效开启 / 切换时返回另一实例，关闭 / 回到默认时回到基础材质实例，保证 BeginSprite 去重正确。
        private static readonly Dictionary<(KFramework.MonoGame.BlendState, KFramework.MonoGame.ShaderEffect), KFramework.MonoGame.Material> _effectMaterials
            = new Dictionary<(KFramework.MonoGame.BlendState, KFramework.MonoGame.ShaderEffect), KFramework.MonoGame.Material>();

        private static void EnsureEffects()
        {
            if (GDevice == null || _grayscaleEffect != null) return;
            // 灰度：把像素转成亮度（遵循 premultiplied 约定，只改 rgb、不动 alpha）。uOpacity 统一淡出。
            const string grayscaleFrag = @"#version 300 es
precision highp float;
in vec2 vTexCoord;
in vec4 vColor;
uniform sampler2D uTexture;
uniform float uOpacity;
out vec4 fragColor;
void main()
{
    vec4 c = texture(uTexture, vTexCoord) * vColor;
    float g = dot(c.rgb, vec3(0.299, 0.587, 0.114));
    fragColor = vec4(g, g, g, c.a * uOpacity);
}";
            // 染色 / 魔法：把颜色朝 uTint.rgb 混合 uTint.a 比例（premultiplied 空间），用于伤害红闪 / 光效等。uOpacity 统一淡出。
            const string tintFrag = @"#version 300 es
precision highp float;
in vec2 vTexCoord;
in vec4 vColor;
uniform sampler2D uTexture;
uniform vec4 uTint;
uniform float uOpacity;
out vec4 fragColor;
void main()
{
    vec4 c = texture(uTexture, vTexCoord) * vColor;
    vec3 tinted = c.rgb * uTint.rgb;
    c.rgb = mix(c.rgb, tinted, clamp(uTint.a, 0.0, 1.0));
    fragColor = vec4(c.rgb, c.a * uOpacity);
}";
            // 淡出：仅把 alpha 乘 uOpacity（premultiplied 约定下 rgb 由下游混合按 alpha 预乘，等于整层按 uOpacity 缩放）。
            const string fadeFrag = @"#version 300 es
precision highp float;
in vec2 vTexCoord;
in vec4 vColor;
uniform sampler2D uTexture;
uniform float uOpacity;
out vec4 fragColor;
void main()
{
    vec4 c = texture(uTexture, vTexCoord) * vColor;
    fragColor = vec4(c.rgb, c.a * uOpacity);
}";
            // 用 Shader 类登记并编译一次（构造即编译，共享 Program）。Defaults 写完整默认 uniform，
            // 保证每个 CreateEffectInstance 出来的实例都从"全默认"起步，共享 Program 也不会串味。
            _grayscaleShader = new KFramework.MonoGame.Shader("Mir2/GrayScale", null, grayscaleFrag, GDevice);
            _tintShader = new KFramework.MonoGame.Shader("Mir2/Tint", null, tintFrag, GDevice);
            _fadeShader = new KFramework.MonoGame.Shader("Mir2/Fade", null, fadeFrag, GDevice);

            _grayscaleShader.Defaults.SetFloat("uOpacity", 1f);
            _tintShader.Defaults.SetFloat("uOpacity", 1f);
            _tintShader.Defaults.SetVector("uTint", new System.Numerics.Vector4(1f, 1f, 1f, 0f));
            _fadeShader.Defaults.SetFloat("uOpacity", 1f);

            // 每个后效状态持有一个独立 uniform 字典的实例（共享底层 Program，互不干扰）。
            _grayscaleEffect = _grayscaleShader.CreateEffectInstance();
            _tintEffect = _tintShader.CreateEffectInstance();
            _fadeEffect = _fadeShader.CreateEffectInstance();
        }

        // 依据当前颜色后效槽 + 全局不透明度，解析本批应使用的 ShaderEffect（null = 默认精灵着色器）。
        private static KFramework.MonoGame.ShaderEffect ResolveEffect()
        {
            if (_grayscaleEffect == null) return null; // 后效未就绪（GDevice 还没好）→ 无后效，安全降级
            switch (_colorEffect)
            {
                case 1:
                    _grayscaleEffect.SetFloat("uOpacity", _opacity);
                    return _grayscaleEffect;
                case 2:
                    _tintEffect.SetVector("uTint", _tintParam);
                    _tintEffect.SetFloat("uOpacity", _opacity);
                    return _tintEffect;
                case 3:
                    _tintEffect.SetVector("uTint", _tintParam);
                    _tintEffect.SetFloat("uOpacity", _opacity);
                    return _tintEffect;
                default:
                    if (_opacity != 1f)
                    {
                        _fadeEffect.SetFloat("uOpacity", _opacity);
                        return _fadeEffect;
                    }
                    return null;
            }
        }

        // 把当前后效合成进“基础材质”，返回本批使用材质（无后效时返回原实例，保持去重）。
        private static KFramework.MonoGame.Material ApplyCurrentEffect(KFramework.MonoGame.Material baseMaterial)
        {
            EnsureEffects();
            var effect = ResolveEffect();
            if (effect == null) return baseMaterial;
            var key = (baseMaterial.Blend, effect);
            if (!_effectMaterials.TryGetValue(key, out var m))
            {
                m = new KFramework.MonoGame.Material
                {
                    Blend = baseMaterial.Blend,
                    Sampler = baseMaterial.Sampler,
                    DepthStencil = baseMaterial.DepthStencil,
                    Rasterizer = baseMaterial.Rasterizer,
                    Effect = effect,
                };
                _effectMaterials[key] = m;
            }
            return m;
        }

        // 对齐 Sprite.Begin(...)：开出主批（材质 / 世界变换未变则忽略，＝原版容忍的重复 Begin）。
        // 主绘制路径传 RenderTransform；文本烘焙等需恒等变换的场景传 null。
        public static void BeginSprite(KFramework.MonoGame.Material material, KFramework.MonoGame.Matrix4x4? transform = null)
        {
            material = ApplyCurrentEffect(material);
            if (_batchBegun && ReferenceEquals(_batchMaterial, material) && Nullable.Equals(_batchTransform, transform)) return;
            if (_batchBegun) Batch.End();
            Batch.Begin(material, KFramework.MonoGame.SpriteSortMode.Deferred, transform);
            _batchBegun = true;
            _batchMaterial = material;
            _batchTransform = transform;
        }

        // 便捷重载：直接用游戏层 MirBlend 枚举，内部转换到引擎混合态（换引擎混合只改 MirBlendExtensions 一处）。
        public static void BeginSprite(MirBlend blend, KFramework.MonoGame.Matrix4x4? transform = null)
            => BeginSprite(GetMaterial(blend.ToBlendState()), transform);

        // 对齐 Sprite.End()：收批（幂等）。切渲染目标 / 帧末 / 异常恢复都要调用。
        public static void EndSprite()
        {
            if (!_batchBegun) return;
            Batch.End();
            _batchBegun = false;
        }

        // 对齐 Sprite.Flush()：提交当前批次（本引擎只能靠 End 提交，故等价于 EndSprite）。
        public static void FlushSprite() => EndSprite();

        // 本次绘制应使用的混合状态：显式传入优先，否则按全局 Blending 标志选「加法 / 非预乘」（对齐原版 SetBlend 语义）。
        private static KFramework.MonoGame.BlendState ResolveBlend(KFramework.MonoGame.BlendState? blendState)
            => blendState ?? (Blending ? MirBlend.Additive : MirBlend.Normal).ToBlendState();

        public static void Draw(Texture2D texture, Rectangle? sourceRect, SlimDX.Vector3? position, SlimDX.Color4 color, KFramework.MonoGame.BlendState? blendState = null)
        {
            if (texture == null) return;
            Rectangle src = sourceRect ?? new Rectangle(0, 0, texture.Width, texture.Height);
            SlimDX.Vector3 pos = position ?? SlimDX.Vector3.Zero;
            BeginSprite(GetMaterial(ResolveBlend(blendState)), RenderTransform);
            // 位置形态已并入「目标矩形形态」：落点 + 源尺寸就是目标矩形。
            Batch.Draw(texture,
                new KFramework.MonoGame.Rectangle((int)pos.X, (int)pos.Y, src.Width, src.Height),
                ToRect(src), ToColor(color));
            CMain.DPSCounter++;
        }

        public static void Draw(Texture2D texture, Rectangle sourceRect, RectangleF destRect, SlimDX.Color4 color, KFramework.MonoGame.BlendState? blendState = null)
        {
            if (texture == null) return;
            BeginSprite(GetMaterial(ResolveBlend(blendState)), RenderTransform);
            Batch.Draw(texture,
                new KFramework.MonoGame.Rectangle((int)destRect.X, (int)destRect.Y, (int)destRect.Width, (int)destRect.Height),
                ToRect(sourceRect), ToColor(color));
            CMain.DPSCounter++;
        }

        // 图集帧专用（与主 Texture2D 路径同源的安全入口：BeginSprite 会按当前上下文自动 Begin，
        // 主渲染 / 场景烘焙通用）。直接调 SpriteBatch.DrawFrame 在烘焙时主批未 Begin 会抛
        // “Draw 必须在 Begin / End 之间调用”。源/目标矩形与颜色直接与 atlas 帧(KSpriteInfo)一致。
        public static void Draw(Texture2D texture, KFramework.MonoGame.Rectangle sourceRect, KFramework.MonoGame.Rectangle destRect, KFramework.MonoGame.Color color, KFramework.MonoGame.BlendState? blendState = null)
        {
            if (texture == null) return;
            BeginSprite(GetMaterial(ResolveBlend(blendState)), RenderTransform);
            Batch.Draw(texture, destRect, sourceRect, color);
            CMain.DPSCounter++;
        }

        public static void DrawOpaque(Texture2D texture, Rectangle? sourceRect, SlimDX.Vector3? position, SlimDX.Color4 color, float opacity)
        {
            var c = color; c.Alpha = opacity; Draw(texture, sourceRect, position, c);
        }

        // —— 离屏渲染目标路径：ControlTexture 是包裹了 KFramework.MonoGame.RenderTarget2D 的 SlimDX 纹理，
        //    烘焙完成后当作普通 Texture2D 采样绘制（照 MonoGame 把 RenderTarget2D 当纹理用）。
        public static void Draw(SlimDX.Direct3D9.Texture texture, Rectangle? sourceRect, SlimDX.Vector3? position, SlimDX.Color4 color, KFramework.MonoGame.BlendState? blendState = null)
        {
            if (texture?.RenderTarget != null) { Draw(texture.RenderTarget, sourceRect, position, color, blendState); return; }
            if (texture?.PlainTexture != null) { Draw(texture.PlainTexture, sourceRect, position, color, blendState); return; }
        }

        public static void Draw(SlimDX.Direct3D9.Texture texture, Rectangle sourceRect, RectangleF destRect, SlimDX.Color4 color, KFramework.MonoGame.BlendState? blendState = null)
        {
            if (texture?.RenderTarget != null) { Draw(texture.RenderTarget, sourceRect, destRect, color, blendState); return; }
            if (texture?.PlainTexture != null) { Draw(texture.PlainTexture, sourceRect, destRect, color, blendState); return; }
        }

        public static void DrawOpaque(SlimDX.Direct3D9.Texture texture, Rectangle? sourceRect, SlimDX.Vector3? position, SlimDX.Color4 color, float opacity)
        {
            var c = color; c.Alpha = opacity;
            Draw(texture, sourceRect, position, c);
        }

        // 1×1 白色像素贴图，用于全屏纯色叠加（如昼夜环境压暗）。
        private static Texture2D _whitePixel;
        public static Texture2D WhitePixel
        {
            get
            {
                if (_whitePixel == null)
                {
                    byte[] rgba = { 255, 255, 255, 255 };
                    _whitePixel = GDevice.CreateTexture(1, 1, rgba);
                }
                return _whitePixel;
            }
        }

        // 画线：把折线各段画成细矩形（复用 1×1 白像素沿 SpriteBatch 拉伸）。
        // 控件边框都是轴对齐矩形，故无需旋转即可精确绘制；对角线段退化为其轴对齐包围盒（罕见）。
        public static void DrawLines(SlimDX.Vector2[] points, float width, Color color)
        {
            if (points == null || points.Length < 2) return;
            var c = ToColor(color);
            for (int i = 0; i < points.Length - 1; i++)
            {
                float x0 = points[i].X, y0 = points[i].Y;
                float x1 = points[i + 1].X, y1 = points[i + 1].Y;
                float dx = x1 - x0, dy = y1 - y0;
                KFramework.MonoGame.Rectangle dest;
                if (Math.Abs(dx) >= Math.Abs(dy))
                    dest = new KFramework.MonoGame.Rectangle(
                        (int)Math.Round(Math.Min(x0, x1)),
                        (int)Math.Round(Math.Min(y0, y1) - width / 2f),
                        (int)Math.Round(Math.Abs(dx)),
                        (int)Math.Round(width));
                else
                    dest = new KFramework.MonoGame.Rectangle(
                        (int)Math.Round(Math.Min(x0, x1) - width / 2f),
                        (int)Math.Round(Math.Min(y0, y1)),
                        (int)Math.Round(width),
                        (int)Math.Round(Math.Abs(dy)));
                if (dest.Width <= 0 || dest.Height <= 0) continue;
                Draw(WhitePixel, new KFramework.MonoGame.Rectangle(0, 0, 1, 1), dest, c);
            }
        }

        // 离屏渲染目标：把 surface 所属的 RenderTarget2D 绑到设备（null = 回默认画布）。
        // 嵌套合成时按"保存/恢复 CurrentSurface"的方式调用，保证子控件烘焙后能回到父 RT。
        public static void SetSurface(SlimDX.Direct3D9.Surface surface)
        {
            // 对齐原版 DXManager.SetSurface：目标相同则直接返回（原版 CurrentSurface == surface 时 return）。
            // 注意这里比较的是【渲染目标本身（Owner）】，而不是 Surface 包装对象：调用方普遍写成
            // `SetSurface(new Surface(ControlTexture))`，每次都是新对象，按引用比较会把"同一个目标"
            // 误判成切目标 —— 那会白白 Flush 掉已经攒好的批，把本该合并的一段绘制切成好几段（DC 白涨）。
            if (ReferenceEquals(CurrentSurface?.Owner, surface?.Owner)) return;
            // 切渲染目标会打断 SpriteBatch（Begin 时已捕获旧目标）：先收批提交到旧目标，
            // 下一次 Draw 会在新目标上重新 Begin（对齐原版 SetSurface 里的 Sprite.Flush()）。
            FlushSprite();
            CurrentSurface = surface;
            GDevice?.SetRenderTarget(surface?.Owner?.RenderTarget);
        }
        public static void SetGrayscale(bool value)
        {
            GrayScale = value;
            // 颜色后效槽：1=灰度；关灰度同时清掉染色（对齐原版 SetGrayscale(false) 把像素着色器置空）。
            _colorEffect = value ? 1 : 0;
        }
        public static void SetOpacity(float opacity) { Opacity = opacity; _opacity = opacity; }
        public static void SetBlend(bool value, float rate = 1F, BlendMode mode = BlendMode.NORMAL)
        {
            // 对齐原版：状态没变就直接 return（绝不重复换批 / 重设渲染状态）。
            if (value == Blending && rate == BlendingRate && mode == BlendingMode) return;

            Blending = value; BlendingRate = rate; BlendingMode = mode;

            // 对齐原版 SetBlend 里的 Sprite.Flush() + Sprite.End() + Sprite.Begin(...)：
            // 混合变了就收掉当前批次，再以新混合对应的材质重新开批。
            EndSprite();
            BeginSprite(Blending ? MirBlend.Additive : MirBlend.Normal, RenderTransform);
        }
        public static void SetNormal(float blend, Color tintcolor)
        {
            _colorEffect = 2; // 染色（normal）
            _tintParam = new System.Numerics.Vector4(tintcolor.R / 255f, tintcolor.G / 255f, tintcolor.B / 255f, blend);
        }
        public static void SetGrayscale(float blend, Color tintcolor)
        {
            // 原版这套在灰度着色器上再叠 tint 常量；本移植灰度着色器无 tint 项，退化为纯灰度。
            SetGrayscale(true);
        }
        public static void SetBlendMagic(float blend, Color tintcolor)
        {
            _colorEffect = 3; // 染色（magic）
            _tintParam = new System.Numerics.Vector4(tintcolor.R / 255f, tintcolor.G / 255f, tintcolor.B / 255f, blend);
        }

        public static void AttemptReset() { }
        public static void ResetDevice() { }
        // 对齐原版 DXManager.AttemptRecovery：收尾 Sprite.End（→ EndSprite）、EndScene（无对应物）、
        // 把渲染目标重置回主画布——否则异常帧残留的离屏目标会让下一帧清屏清错对象。
        public static void AttemptRecovery()
        {
            try { EndSprite(); } catch { }
            try { SetSurface(null); } catch { }
        }

        // 离屏渲染目标：走 KFramework.MonoGame.RenderTarget2D（SlimDX 壳的 Texture 构造里创建）。
        public static SlimDX.Direct3D9.Texture CreateRenderTarget(int w, int h)
        {
            return new SlimDX.Direct3D9.Texture(DXManager.Device, w, h, 1, Usage.RenderTarget, Format.A8R8G8B8, Pool.Default);
        }

        public static void ClearControlTexture(SlimDX.Direct3D9.Texture tex, Color backColour)
        {
            if (tex?.RenderTarget == null) return;
            // 绑到该离屏目标清屏，随后恢复到"当前渲染目标"——这是嵌套合成的关键：
            // 场景烘焙子控件时，子控件清屏后必须回到场景 RT，而非默认画布。
            var saved = CurrentSurface;
            SetSurface(new SlimDX.Direct3D9.Surface(tex));
            GDevice?.Clear(ToColor(backColour));
            SetSurface(saved);
        }

        // 浏览器端全屏呈现：场景（含所有子控件）已在固定逻辑分辨率(Settings.ScreenWidth/Height)
        // 下烘焙进离屏纹理(ControlTexture)，这里把它拉伸铺满整个画布(Viewport)。
        // 这样 UI 只需按设计分辨率布局一次即可等比居中铺满窗口，且不会只显示在左上角、
        // 四周露出 MirScene 的洋红(Magenta)底色。
        // 参数用 KFramework.MonoGame 的纹理类型（RenderTarget2D : Texture2D），
        // 避免与 SlimDX.Direct3D9.Texture 产生"不明确的引用"。
        public static void PresentToScreen(KFramework.MonoGame.Texture2D texture)
        {
            if (texture == null) return;
            RenderTransform = null;
            // 固定用 NonPremultiplied（预乘烤）上屏：场景已在不透明离屏 RT 内用 NonPremultiplied 合成完毕
            // （RT 内为预乘 rgb + 直线 alpha，且整帧不透明、alpha=1），此处按已预乘读取即 = 原色，不会二次预乘发暗；
            // 绝不能依赖全局 DXManager.Blending——光照/特效(SetBlend(true))会把全局混合残留成
            // 加法，若此处落到全局，则整帧(尤其 UI 层)被加法叠到亮世界上→暗色 UI 过曝发白
            // （与 MirImageControl 注释警告的“混合过头”同源，只是泄漏点在合成而非逐控件绘制）。
            Draw(texture,
                new Rectangle(0, 0, texture.Width, texture.Height),
                new RectangleF(0, 0, texture.Width, texture.Height),
                Color.White,
                MirBlend.Normal.ToBlendState());
        }

        //这个方法是每秒刷新一次
        public static void Clean()
        {
            for (int i = TextureList.Count - 1; i >= 0; i--)
            {
                MImage m = TextureList[i];
                if (m == null) 
                { 
                    TextureList.RemoveAt(i); 
                    continue; 
                }
                if (CMain.Time <= m.CleanTime) //自身还有一个清理时间
                {
                    continue;
                }
                m.DisposeTexture();
            }
            for (int i = ControlList.Count - 1; i >= 0; i--)
            {
                MirControl c = ControlList[i];
                if (c == null) 
                { 
                    ControlList.RemoveAt(i); 
                    continue; 
                }
                if (CMain.Time <= c.CleanTime) //自身还有一个清理时间
                {
                    continue;
                }
                c.DisposeTexture();
            }
        }

        public static void Dispose() { }
    }
}
