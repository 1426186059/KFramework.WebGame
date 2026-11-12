using Client.MirControls;
using KFramework.MonoGame;
using SlimDX.Direct3D9;
using WebGame.Mir2.MonoGame.Client;
using Color = MirEngine.Color;
using Rectangle = MirEngine.Rectangle;
using RectangleF = KFramework.MonoGame.RectangleF;

namespace Client.MirGraphics
{
    class DXManager
    {
        public static List<MImage> TextureList = new List<MImage>();
        public static List<MirControl> ControlList = new List<MirControl>();
        public static float Opacity = 1F;
        public static KFramework.MonoGame.Matrix4x4? RenderTransform = null;
        public static bool Blending;
        public static float BlendingRate;
        public static BlendMode BlendingMode;

        public static SlimDX.Direct3D9.Sprite Sprite = new SlimDX.Direct3D9.Sprite();
        public static SlimDX.Direct3D9.Line Line = new SlimDX.Direct3D9.Line();
        public static SlimDX.Direct3D9.Device Device = new SlimDX.Direct3D9.Device();
        public static SlimDX.Direct3D9.Surface CurrentSurface;
        public static bool GrayScale;
        public static SlimDX.Direct3D9.Texture RadarTexture;
        public static SlimDX.Direct3D9.Texture PoisonDotBackground;
        public static SlimDX.Direct3D9.Texture FloorTexture;
        public static SlimDX.Direct3D9.Surface FloorSurface;
        public static int DPSCounter;


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

        public static GraphicsDevice GDevice;
        public static SpriteBatch Batch;
        public static void Initialize(GraphicsDevice device, SpriteBatch batch)
        {
            GDevice = device;
            Batch = batch;

            var pp = GDevice.PresentationParameters;
            if (pp != null && pp.BackBufferWidth > 0 && pp.BackBufferHeight > 0)
            {
                var vp = GDevice.Viewport;
                vp.X = 0; vp.Y = 0;
                vp.Width = pp.BackBufferWidth;
                vp.Height = pp.BackBufferHeight;
                GDevice.Viewport = vp;
            }

            GDevice.PresentationParameters.PresentationInterval = Settings.FPSCap ? KFramework.MonoGame.PresentInterval.One : KFramework.MonoGame.PresentInterval.Immediate;
            GDevice.PresentationParameters.RenderTargetUsage = RenderTargetUsage.PreserveContents;
            LoadTextures();
        }

        private static void LoadTextures()
        {
            if (GDevice == null) return;
            if (RadarTexture == null)
            {
                int s = 4;
                byte[] d = new byte[s * s * 4];
                for (int i = 0; i < s * s; i++) { d[i * 4] = 255; d[i * 4 + 1] = 255; d[i * 4 + 2] = 255; d[i * 4 + 3] = 255; }
                RadarTexture = new SlimDX.Direct3D9.Texture(Device, s, s, 1, Usage.None, Format.A8R8G8B8, Pool.Managed);
                RadarTexture.PlainTexture = GDevice.CreateTexture(s, s, d);
            }
            if (PoisonDotBackground == null)
            {
                int s = 6;
                byte[] d = new byte[s * s * 4];
                for (int i = 0; i < s * s; i++) { d[i * 4] = 0; d[i * 4 + 1] = 0; d[i * 4 + 2] = 0; d[i * 4 + 3] = 255; }
                PoisonDotBackground = new SlimDX.Direct3D9.Texture(Device, s, s, 1, Usage.None, Format.A8R8G8B8, Pool.Managed);
                PoisonDotBackground.PlainTexture = GDevice.CreateTexture(s, s, d);
            }
        }

        public static void Create()
        {
        }

        static KFramework.MonoGame.Color ToColor(SlimDX.Color4 c) =>
            new KFramework.MonoGame.Color((byte)(c.Red * 255), (byte)(c.Green * 255), (byte)(c.Blue * 255), (byte)(c.Alpha * 255));

        static KFramework.MonoGame.Color ToColor(Color c) =>
            new KFramework.MonoGame.Color(c.R, c.G, c.B, c.A);

        static KFramework.MonoGame.Rectangle ToRect(Rectangle r) =>
            new KFramework.MonoGame.Rectangle(r.X, r.Y, r.Width, r.Height);

        private static KFramework.MonoGame.Material _batchMaterial;
        private static KFramework.MonoGame.Matrix4x4? _batchTransform;

        private static readonly Dictionary<KFramework.MonoGame.BlendState, KFramework.MonoGame.Material> _materials
            = new Dictionary<KFramework.MonoGame.BlendState, KFramework.MonoGame.Material>();

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

        private static KFramework.MonoGame.Shader _grayscaleShader;
        private static KFramework.MonoGame.Shader _tintShader;     // Normal + Magic 共用（同片元源码）
        private static KFramework.MonoGame.Shader _fadeShader;     // 仅淡出（不改色）

        private static KFramework.MonoGame.ShaderEffect _grayscaleEffect;
        private static KFramework.MonoGame.ShaderEffect _tintEffect;    // Normal / Magic 共用同一实例（互斥，每帧重写 uTint/uOpacity）
        private static KFramework.MonoGame.ShaderEffect _fadeEffect;

        private static int _colorEffect;
        private static System.Numerics.Vector4 _tintParam; // rgb = 染色色，a = 混合比例
        private static float _opacity = 1f;

        private static int _effectVersion;
        private static int _appliedEffectVersion = -1;

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

        public static void BeginSprite(KFramework.MonoGame.Material material, KFramework.MonoGame.Matrix4x4? transform = null)
        {
            material = ApplyCurrentEffect(material);
            bool sameMaterial = _batchMaterial != null && ReferenceEquals(_batchMaterial, material) && Nullable.Equals(_batchTransform, transform);
            bool effectStale = material.Effect != null && _appliedEffectVersion != _effectVersion;
            if (sameMaterial && !effectStale)
            {
                return;
            }

            Batch.AutoEndBatch();   // 安全收掉旧批（未开批则为 no-op，= 原版 Sprite.Flush 的"提交不收批"语义）
            Batch.Begin(material, KFramework.MonoGame.SpriteSortMode.Deferred, transform);
            _batchMaterial = material;
            _batchTransform = transform;
            if (material.Effect != null) _appliedEffectVersion = _effectVersion;
        }

        public static void BeginSprite(MirBlend blend, KFramework.MonoGame.Matrix4x4? transform = null)
        { 
             BeginSprite(GetMaterial(blend.ToBlendState()), transform);
        }

        public static void EndSprite()
        {
            Batch.AutoEndBatch();   // AutoEndBatch 已封装"批活跃才 End"，无需再自维护 _batchBegun 标志
            _batchMaterial = null;
            _batchTransform = null;
        }

        public static void FlushSprite() => EndSprite();

        private static KFramework.MonoGame.BlendState ResolveBlend(KFramework.MonoGame.BlendState? blendState)
            => blendState ?? (Blending ? MirBlend.Additive : MirBlend.Normal).ToBlendState();

        public static void Draw(Texture2D texture, Rectangle? sourceRect, SlimDX.Vector3? position, SlimDX.Color4 color, KFramework.MonoGame.BlendState? blendState = null)
        {
            if (texture == null) return;
            Rectangle src = sourceRect ?? new Rectangle(0, 0, texture.Width, texture.Height);
            SlimDX.Vector3 pos = position ?? SlimDX.Vector3.Zero;
            BeginSprite(GetMaterial(ResolveBlend(blendState)), RenderTransform);
            Batch.Draw(texture,
                new KFramework.MonoGame.Rectangle((int)pos.X, (int)pos.Y, src.Width, src.Height),
                ToRect(src), ToColor(color));
            CMain.DPSCounter++;
        }

        public static void Draw(Texture2D texture, Rectangle sourceRect, RectangleF destRect, SlimDX.Color4 color, KFramework.MonoGame.BlendState? blendState = null)
        {
            if (texture == null) return;
            BeginSprite(GetMaterial(ResolveBlend(blendState)), RenderTransform);
            // 目标矩形按浮点传递（引擎 SpriteBatch 已支持 RectangleF? 目标矩形），不再强转 int，
            // 保留亚像素定位，消除缩放精灵的逐帧取整抖动。
            Batch.Draw(texture, destRect, ToRect(sourceRect), ToColor(color));
            CMain.DPSCounter++;
        }

        public static void Draw(Texture2D texture, KFramework.MonoGame.Rectangle sourceRect, KFramework.MonoGame.Rectangle destRect, KFramework.MonoGame.Color color, KFramework.MonoGame.BlendState? blendState = null)
        {
            if (texture == null) return;
            BeginSprite(GetMaterial(ResolveBlend(blendState)), RenderTransform);
            Batch.Draw(texture, destRect, sourceRect, color);
            CMain.DPSCounter++;
        }

        public static void Draw(Texture2D texture, KFramework.MonoGame.Rectangle sourceRect, KFramework.MonoGame.Rectangle destRect, KFramework.MonoGame.Color color, KFramework.MonoGame.BlendState? blendState, KFramework.MonoGame.SpriteEffects effects)
        {
            if (texture == null) return;
            BeginSprite(GetMaterial(ResolveBlend(blendState)), RenderTransform);
            Batch.Draw(texture, destRect, sourceRect, color, 0f, KFramework.MonoGame.Vector2.Zero, (KFramework.MonoGame.Vector2?)null, effects, 0f);
            CMain.DPSCounter++;
        }

        /// <summary>浮点目标矩形版（含翻转）：用于缩放精灵的亚像素定位，避免整型取整导致的逐帧抖动。</summary>
        public static void Draw(Texture2D texture, KFramework.MonoGame.Rectangle sourceRect, RectangleF destRect, KFramework.MonoGame.Color color, KFramework.MonoGame.BlendState? blendState, KFramework.MonoGame.SpriteEffects effects)
        {
            if (texture == null) return;
            BeginSprite(GetMaterial(ResolveBlend(blendState)), RenderTransform);
            Batch.Draw(texture, destRect, sourceRect, color, 0f, KFramework.MonoGame.Vector2.Zero, (KFramework.MonoGame.Vector2?)null, effects, 0f);
            CMain.DPSCounter++;
        }

        public static void DrawOpaque(Texture2D texture, Rectangle? sourceRect, SlimDX.Vector3? position, SlimDX.Color4 color, float opacity)
        {
            var c = color; c.Alpha = opacity; Draw(texture, sourceRect, position, c);
        }

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

        public static void SetSurface(SlimDX.Direct3D9.Surface surface)
        {
            if (ReferenceEquals(CurrentSurface?.Owner, surface?.Owner)) return;
            FlushSprite();
            CurrentSurface = surface;
            GDevice?.SetRenderTarget(surface?.Owner?.RenderTarget);
        }

        public static void SetGrayscale(bool value)
        {
            GrayScale = value;
            _colorEffect = value ? 1 : 0;
            _effectVersion++;
        }
        public static void SetOpacity(float opacity) { Opacity = opacity; _opacity = opacity; _effectVersion++; }
        public static void SetBlend(bool value, float rate = 1F, BlendMode mode = BlendMode.NORMAL)
        {
            if (value == Blending &&
                rate == BlendingRate &&
                mode == BlendingMode)
            {
                return;
            }

            Blending = value; 
            BlendingRate = rate; 
            BlendingMode = mode;
            EndSprite();
            if (Blending)
            {
                BeginSprite(Blending ? MirBlend.Additive : MirBlend.Normal, RenderTransform);
            }
            else
            {
                BeginSprite(MirBlend.AlphaBlend);
            }

            Device.SetRenderTarget(0, CurrentSurface);
        }

        public static void SetNormal(float blend, Color tintcolor)
        {
            _colorEffect = 2; // 染色（normal）
            _tintParam = new System.Numerics.Vector4(tintcolor.R / 255f, tintcolor.G / 255f, tintcolor.B / 255f, blend);
            _effectVersion++;
        }

        public static void SetGrayscale(float blend, Color tintcolor)
        {
            SetGrayscale(true);
        }

        public static void SetBlendMagic(float blend, Color tintcolor)
        {
            _colorEffect = 3; // 染色（magic）
            _tintParam = new System.Numerics.Vector4(tintcolor.R / 255f, tintcolor.G / 255f, tintcolor.B / 255f, blend);
            _effectVersion++;
        }

        public static void AttemptReset() { }
        public static void ResetDevice() { }
        public static void AttemptRecovery()
        {
            try { EndSprite(); } catch { }
            try { SetSurface(null); } catch { }
        }

        public static SlimDX.Direct3D9.Texture CreateRenderTarget(int w, int h)
        {
            return new SlimDX.Direct3D9.Texture(DXManager.Device, w, h, 1, Usage.RenderTarget, Format.A8R8G8B8, Pool.Default);
        }

        public static void ClearControlTexture(SlimDX.Direct3D9.Texture tex, Color backColour)
        {
            if (tex?.RenderTarget == null) return;
            var saved = CurrentSurface;
            SetSurface(new SlimDX.Direct3D9.Surface(tex));
            GDevice?.Clear(ToColor(backColour));
            SetSurface(saved);
        }

        public static void PresentToScreen(KFramework.MonoGame.Texture2D texture)
        {
            if (texture == null) return;
            RenderTransform = null;
            Draw(texture,
                new Rectangle(0, 0, texture.Width, texture.Height),
                new RectangleF(0, 0, texture.Width, texture.Height),
                Color.White,
                MirBlend.Normal.ToBlendState());
        }

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
