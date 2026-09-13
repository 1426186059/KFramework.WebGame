using System;
using System.Collections.Generic;
using MirEngine;
using System.Numerics;
using Shared.Rendering;

namespace Shared.Rendering
{
    /// <summary>
    /// 渲染管线管理器（WASM / HTML5 Canvas 适配版）。
    /// 原版 Zircon 此文件依赖 DirectX（SilkD3D11/SilkVulkan）、WinForms（Control/Form/Screen/TextRenderer）
    /// 与 Graphics，均无法在浏览器运行；此处改为单一 Canvas 管线，所有公开方法委托给
    /// <see cref="CanvasRenderingPipeline"/>，API 表面与原版保持一致，便于后续逐步移植调用方。
    /// </summary>
    public static class RenderingPipelineManager
    {
        private const string DefaultPipelineId = "Canvas";
        private static readonly Dictionary<string, Func<IRenderingPipeline>> PipelineFactories = new(StringComparer.OrdinalIgnoreCase)
        {
            { DefaultPipelineId, () => new CanvasRenderingPipeline() },
        };

        private static IRenderingPipeline _activePipeline;
        private static RenderingPipelineContext _context;
        private static PipelineSession _activeSession;
        private static readonly List<ITextureCacheItem> FallbackControlCache = new();
        private static readonly List<ITextureCacheItem> FallbackTextureCache = new();
        private static readonly List<ISoundCacheItem> FallbackSoundCache = new();
        private static float _fallbackOpacity = 1F;
        private static bool _fallbackBlending;
        private static float _fallbackBlendRate = 1F;
        private static BlendMode _fallbackBlendMode = BlendMode.NORMAL;
        private static SpriteShaderEffectRequest? _spriteShaderEffect;
        private static float _fallbackLineWidth = 1F;
        private static TextureFilterMode _fallbackTextureFilter = TextureFilterMode.Point;
        private static string _pendingPipelineId;
        internal static RenderingHostSettings HostSettings => Settings;
        internal static object RenderTarget => _context?.RenderTarget;
        private static RenderingHostSettings Settings => _context?.Settings ?? DefaultSettings;
        private static readonly RenderingHostSettings DefaultSettings = new();

        public sealed class PipelineSession : IDisposable
        {
            internal PipelineSession(IRenderingPipeline pipeline, RenderingPipelineContext context)
            {
                Pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
                Context = context ?? throw new ArgumentNullException(nameof(context));
            }

            internal IRenderingPipeline Pipeline { get; }
            internal RenderingPipelineContext Context { get; }
            public string PipelineId => Pipeline.Id;
            public bool IsDisposed { get; private set; }

            public IDisposable Activate()
            {
                if (IsDisposed)
                    throw new ObjectDisposedException(nameof(PipelineSession));

                return RenderingPipelineManager.Activate(this);
            }

            public void Dispose()
            {
                if (IsDisposed)
                    return;

                IsDisposed = true;
                RenderingPipelineManager.DestroySession(this);
            }
        }

        private sealed class PipelineActivation : IDisposable
        {
            private readonly PipelineSession _previousSession;
            private readonly IRenderingPipeline _previousPipeline;
            private readonly RenderingPipelineContext _previousContext;
            private bool _disposed;

            public PipelineActivation(PipelineSession session)
            {
                _previousSession = _activeSession;
                _previousPipeline = _activePipeline;
                _previousContext = _context;
                SetActiveSession(session);
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;
                _activeSession = _previousSession;
                _activePipeline = _previousPipeline;
                _context = _previousContext;
            }
        }

        public static string DefaultPipelineIdentifier => DefaultPipelineId;
        public static string ActivePipelineId => _activePipeline?.Id;
        public static bool SupportsCachedRenderTargets => _activePipeline?.SupportsCachedRenderTargets ?? false;
        public static bool SupportsAtlasTextures => _activePipeline?.SupportsAtlasTextures ?? false;
        public static bool SupportsBc7Textures => _activePipeline?.SupportsBc7Textures ?? false;
        public static IReadOnlyCollection<string> AvailablePipelineIds => PipelineFactories.Keys;
        public static bool SupportsMultiplePipelines => PipelineFactories.Count > 1;
        public static bool IsDefaultPipelineOnly => PipelineFactories.Count == 1 && PipelineFactories.ContainsKey(DefaultPipelineId);

        public static IReadOnlyList<DisplayMonitorInfo> GetDisplayMonitors()
        {
            // 浏览器单显示器：返回占位的 1024x768 主显示器
            return new List<DisplayMonitorInfo> { new(0, "Canvas", true, new Rectangle(0, 0, 1024, 768)) };
        }

        public static int GetSelectedMonitorIndex() => 0;

        public static DisplayMonitorInfo GetSelectedMonitor() => GetDisplayMonitors()[0];

        public static void SelectMonitor(int monitorIndex) => Settings.DefaultMonitor = GetSelectedMonitor().DeviceName;

        public static void Initialize(string pipelineId, RenderingPipelineContext context)
        {
            CreateSession(pipelineId, context);
        }

        public static PipelineSession CreateSession(string pipelineId, RenderingPipelineContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (!PipelineFactories.TryGetValue(pipelineId, out Func<IRenderingPipeline> factory))
                throw new ArgumentException($"Unknown rendering pipeline '{pipelineId}'.", nameof(pipelineId));

            IRenderingPipeline pipeline = factory();
            PipelineSession session = new(pipeline, context);
            PipelineSession previousSession = _activeSession;
            IRenderingPipeline previousPipeline = _activePipeline;
            RenderingPipelineContext previousContext = _context;
            try
            {
                SetActiveSession(session);
                pipeline.Initialize(context);
                return session;
            }
            catch
            {
                if (ReferenceEquals(_activeSession, session))
                {
                    _activeSession = previousSession;
                    _activePipeline = previousPipeline;
                    _context = previousContext;
                }

                pipeline.Shutdown();
                throw;
            }
        }

        public static string InitializeWithFallback(string requestedPipelineId, RenderingPipelineContext context)
        {
            return CreateSessionWithFallback(requestedPipelineId, context).PipelineId;
        }

        public static PipelineSession CreateSessionWithFallback(string requestedPipelineId, RenderingPipelineContext context)
        {
            string pipelineToUse = string.IsNullOrWhiteSpace(requestedPipelineId) ? DefaultPipelineId : requestedPipelineId;

            try
            {
                return CreateSession(pipelineToUse, context);
            }
            catch (Exception ex)
            {
                if (pipelineToUse.Equals(DefaultPipelineId, StringComparison.OrdinalIgnoreCase) || !PipelineFactories.ContainsKey(DefaultPipelineId))
                    throw;

                PipelineSession session = CreateSession(DefaultPipelineId, context);
                Console.WriteLine($"Falling back to rendering pipeline '{DefaultPipelineId}' after '{pipelineToUse}' failed: {ex.Message}");
                return session;
            }
        }

        public static IDisposable Activate(PipelineSession session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            return new PipelineActivation(session);
        }

        private static void SetActiveSession(PipelineSession session)
        {
            _activeSession = session;
            _activePipeline = session?.Pipeline;
            _context = session?.Context;
        }

        public static void SwitchPipeline(string pipelineId)
        {
            pipelineId = NormalizePipelineId(pipelineId);

            if (string.Equals(ActivePipelineId, pipelineId, StringComparison.OrdinalIgnoreCase)) return;

            RenderingPipelineContext context = _context ?? throw new InvalidOperationException("No rendering pipeline context is available.");

            InvalidateAllControlTextures();
            Shutdown();

            InitializeWithFallback(pipelineId, context);
        }

        public static void RequestSwitchPipeline(string pipelineId)
        {
            string normalizedPipelineId = NormalizePipelineId(pipelineId);
            _pendingPipelineId = string.Equals(ActivePipelineId, normalizedPipelineId, StringComparison.OrdinalIgnoreCase)
                ? null
                : normalizedPipelineId;
        }

        public static bool ApplyPendingPipelineSwitch()
        {
            if (string.IsNullOrWhiteSpace(_pendingPipelineId))
                return false;

            string pipelineId = _pendingPipelineId;
            _pendingPipelineId = null;

            if (string.Equals(ActivePipelineId, pipelineId, StringComparison.OrdinalIgnoreCase))
                return false;

            string previousPipelineId = ActivePipelineId;
            RenderingPipelineContext context = _context ?? throw new InvalidOperationException("No rendering pipeline context is available.");
            RenderingHostSettings settings = context.Settings;

            try
            {
                InvalidateAllControlTextures();
                Shutdown();

                string activePipelineId = InitializeWithFallback(pipelineId, context);
                settings.RenderingPipeline = activePipelineId;
            }
            catch (Exception ex)
            {
                settings.ReportException(ex);

                if (string.IsNullOrWhiteSpace(previousPipelineId))
                    throw;

                string restoredPipelineId = InitializeWithFallback(previousPipelineId, context);
                settings.RenderingPipeline = restoredPipelineId;
            }

            return true;
        }

        private static void InvalidateAllControlTextures()
        {
            _activePipeline?.InvalidateTextureCaches();
            Settings.InvalidateRenderCaches?.Invoke();
        }

        public static void Shutdown()
        {
            if (_activeSession == null)
                return;

            _activeSession.Dispose();
        }

        private static void DestroySession(PipelineSession session)
        {
            if (session == null)
                return;

            session.Pipeline.Shutdown();

            if (ReferenceEquals(_activeSession, session))
                SetActiveSession(null);
        }

        public static void RunMessageLoop(Action loop)
        {
            // 浏览器端由 main.js 的 requestAnimationFrame 驱动 game.Frame，无需自启循环
            loop?.Invoke();
        }

        public static bool RenderFrame(Action drawScene)
        {
            if (_activePipeline == null)
                throw new InvalidOperationException("No rendering pipeline has been initialized.");

            return _activePipeline.RenderFrame(drawScene);
        }

        public static void ToggleFullScreen() => _activePipeline?.ToggleFullScreen();
        public static void SetResolution(Size size) => _activePipeline?.SetResolution(size);
        public static void SetTargetMonitor(int monitorIndex) => _activePipeline?.SetTargetMonitor(monitorIndex);
        public static void CenterOnSelectedMonitor() => _activePipeline?.CenterOnSelectedMonitor();
        public static void ResetDevice() => _activePipeline?.ResetDevice();
        public static void OnSceneChanged(bool isGameScene) => _activePipeline?.OnSceneChanged(isGameScene);

        public static IReadOnlyList<Size> GetSupportedResolutions()
            => _activePipeline?.GetSupportedResolutions() ?? Array.Empty<Size>();

        public static IReadOnlyList<GraphicsAdapterInfo> GetGraphicsAdapters(string pipelineId)
            => Array.Empty<GraphicsAdapterInfo>();

        public static string NormalizePipelineId(string pipelineId)
        {
            string requestedId = string.IsNullOrWhiteSpace(pipelineId) ? DefaultPipelineId : pipelineId;

            if (IsDefaultPipelineOnly)
                return DefaultPipelineId;

            if (!PipelineFactories.ContainsKey(requestedId) && PipelineFactories.ContainsKey(DefaultPipelineId))
                return DefaultPipelineId;

            return requestedId;
        }

        public static void RegisterFactory(string pipelineId, Func<IRenderingPipeline> factory)
        {
            if (string.IsNullOrWhiteSpace(pipelineId))
                throw new ArgumentException("Pipeline identifier must be provided.", nameof(pipelineId));
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            PipelineFactories[pipelineId] = factory;
        }

        public static Size MeasureText(string text, MirEngine.Font font)
        {
            if (_activePipeline != null)
                return _activePipeline.MeasureText(text, font);

            return new Size(0, 12);
        }

        public static Size MeasureText(string text, MirEngine.Font font, Size proposedSize)
        {
            if (_activePipeline != null)
                return _activePipeline.MeasureText(text, font, proposedSize);

            return new Size(0, 12);
        }

        public static Size MeasureText(string text, MirEngine.Font font, Size proposedSize, TextFormatFlags flags)
        {
            if (_activePipeline != null)
                return _activePipeline.MeasureText(text, font, proposedSize, flags);

            return new Size(0, 12);
        }

        public static void ConfigureGraphics(Graphics graphics)
        {
        }

        public static float GetHorizontalDpi() => _activePipeline?.GetHorizontalDpi() ?? 96F;

        public static Color ConvertHslToRgb(float h, float s, float l)
            => _activePipeline?.ConvertHslToRgb(h, s, l) ?? Color.Black;

        public static void SetOpacity(float opacity)
        {
            if (_activePipeline != null)
            {
                _activePipeline.SetOpacity(opacity);
                return;
            }

            _fallbackOpacity = opacity;
        }

        public static float GetOpacity() => _activePipeline?.GetOpacity() ?? _fallbackOpacity;

        public static void SetBlend(bool enabled, float rate = 1F, BlendMode mode = BlendMode.NORMAL)
        {
            if (_activePipeline != null)
            {
                _activePipeline.SetBlend(enabled, rate, mode);
                return;
            }

            _fallbackBlending = enabled;
            _fallbackBlendRate = rate;
            _fallbackBlendMode = mode;
        }

        public static bool IsBlending() => _activePipeline?.IsBlending() ?? _fallbackBlending;
        public static float GetBlendRate() => _activePipeline?.GetBlendRate() ?? _fallbackBlendRate;
        public static BlendMode GetBlendMode() => _activePipeline?.GetBlendMode() ?? _fallbackBlendMode;
        public static float GetLineWidth() => _activePipeline?.GetLineWidth() ?? _fallbackLineWidth;

        public static void SetLineWidth(float width)
        {
            if (_activePipeline != null)
            {
                _activePipeline.SetLineWidth(width);
                return;
            }

            _fallbackLineWidth = width;
        }

        public static void EnableOutlineEffect(Color colour, float thickness)
        {
            _spriteShaderEffect = new SpriteShaderEffectRequest(new OutlineEffectSettings(colour, thickness));
        }

        public static void EnableGrayscaleEffect()
        {
            _spriteShaderEffect = new SpriteShaderEffectRequest(SpriteShaderEffectKind.Grayscale);
        }

        public static void EnableSolidShadowFillEffect(float opacity)
        {
            _spriteShaderEffect = new SpriteShaderEffectRequest(SpriteShaderEffectKind.SolidShadowFill, Math.Clamp(opacity, 0F, 1F));
        }

        public static void EnableDropShadowEffect(Color colour, float width, float startOpacity, RectangleF? visibleBounds = null)
        {
            _spriteShaderEffect = new SpriteShaderEffectRequest(new DropShadowEffectSettings(colour, width, startOpacity, visibleBounds));
        }

        public static void DisableSpriteShaderEffect() => _spriteShaderEffect = null;
        public static void DisableOutlineEffect() => DisableSpriteShaderEffect();
        internal static SpriteShaderEffectRequest? GetSpriteShaderEffect() => _spriteShaderEffect;

        public static void DrawLine(IReadOnlyList<LinePoint> points, Color colour)
        {
            if (points == null || points.Count == 0)
                return;

            _activePipeline?.DrawLine(points, colour);
        }

        public static void FlushLines() => _activePipeline?.FlushLines();

        public static void DrawTextureBlend(RenderTexture texture, Rectangle? sourceRectangle, Matrix3x2 transform, Vector3 center, Vector3 translation, Color colour, float blendRate, BlendMode mode = BlendMode.NORMAL)
        {
            if (!texture.IsValid)
                throw new ArgumentException("A valid texture handle is required.", nameof(texture));

            if (_activePipeline == null)
                throw new InvalidOperationException("No rendering pipeline has been initialized.");

            bool oldBlend = _activePipeline.IsBlending();
            float oldRate = _activePipeline.GetBlendRate();
            BlendMode oldMode = _activePipeline.GetBlendMode();

            try
            {
                _activePipeline.SetBlend(true, blendRate, mode);
                _activePipeline.DrawTexture(texture, sourceRectangle, transform, center, translation, colour);
            }
            finally
            {
                _activePipeline.SetBlend(oldBlend, oldRate, oldMode);
            }
        }

        public static void DrawTexture(RenderTexture texture, Rectangle sourceRectangle, RectangleF destinationRectangle, Color colour)
        {
            if (!texture.IsValid)
                throw new ArgumentException("A valid texture handle is required.", nameof(texture));

            if (sourceRectangle.Width <= 0 || sourceRectangle.Height <= 0)
                return;

            if (destinationRectangle.Width <= 0 || destinationRectangle.Height <= 0)
                return;

            if (_activePipeline == null)
                throw new InvalidOperationException("No rendering pipeline has been initialized.");

            _activePipeline.DrawTexture(texture, sourceRectangle, destinationRectangle, colour);
        }

        public static void DrawTexture(RenderTexture texture, Rectangle? sourceRectangle, Matrix3x2 transform, Vector3 center, Vector3 translation, Color colour)
        {
            if (!texture.IsValid)
                throw new ArgumentException("A valid texture handle is required.", nameof(texture));

            if (_activePipeline == null)
                throw new InvalidOperationException("No rendering pipeline has been initialized.");

            _activePipeline.DrawTexture(texture, sourceRectangle, transform, center, translation, colour);
        }

        public static void BeginSpriteBatch() => _activePipeline?.BeginSpriteBatch();
        public static void QueueSprite(RenderTexture texture, Rectangle sourceRectangle, RectangleF destinationRectangle, Color colour)
        {
            if (!texture.IsValid)
                throw new ArgumentException("A valid texture handle is required.", nameof(texture));

            if (_activePipeline == null)
                throw new InvalidOperationException("No rendering pipeline has been initialized.");

            _activePipeline.QueueSprite(texture, sourceRectangle, destinationRectangle, colour);
        }
        public static void EndSpriteBatch() => _activePipeline?.EndSpriteBatch();

        public static RenderSurface GetCurrentSurface()
        {
            if (_activePipeline == null)
                throw new InvalidOperationException("No rendering pipeline has been initialized.");

            return _activePipeline.GetCurrentSurface();
        }

        public static void SetSurface(RenderSurface surface)
        {
            if (!surface.IsValid)
                throw new ArgumentException("A valid surface handle is required.", nameof(surface));

            if (_activePipeline == null)
                throw new InvalidOperationException("No rendering pipeline has been initialized.");

            _activePipeline.SetSurface(surface);
        }

        public static RenderSurface GetScratchSurface()
        {
            if (_activePipeline == null)
                throw new InvalidOperationException("No rendering pipeline has been initialized.");

            return _activePipeline.GetScratchSurface();
        }

        public static RenderTexture GetScratchTexture()
        {
            if (_activePipeline == null)
                throw new InvalidOperationException("No rendering pipeline has been initialized.");

            return _activePipeline.GetScratchTexture();
        }

        public static void ColorFill(RenderSurface surface, Rectangle rectangle, Color colorFill)
        {
            if (!surface.IsValid)
                throw new ArgumentException("A valid surface handle is required.", nameof(surface));

            if (_activePipeline == null)
                throw new InvalidOperationException("No rendering pipeline has been initialized.");

            _activePipeline.ColorFill(surface, rectangle, colorFill);
        }

        public static void FillRectangle(Rectangle rectangle, Color colour)
        {
            if (rectangle.Width <= 0 || rectangle.Height <= 0 || colour.A == 0)
                return;

            // HTML5 Canvas 直接填充矩形（无需中间白纹理）
            BrowserCanvas.FillRect(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, colour.ToArgb());
        }

        public static RenderTargetResource CreateRenderTarget(Size size)
        {
            if (_activePipeline == null)
                throw new InvalidOperationException("No rendering pipeline has been initialized.");

            return _activePipeline.CreateRenderTarget(size);
        }

        public static void ReleaseRenderTarget(RenderTargetResource renderTarget)
        {
            if (!renderTarget.IsValid)
                return;

            if (_activePipeline == null)
                return;

            _activePipeline.ReleaseRenderTarget(renderTarget);
        }

        public static Size GetBackBufferSize()
            => _activePipeline?.GetBackBufferSize() ?? new Size(1024, 768);

        public static RenderTexture GetColourPaletteTexture()
        {
            if (_activePipeline == null)
                throw new InvalidOperationException("No rendering pipeline has been initialized.");

            return _activePipeline.GetColourPaletteTexture();
        }

        public static byte[] GetColourPaletteData()
            => _activePipeline?.GetColourPaletteData() ?? Array.Empty<byte>();

        public static RenderTexture GetLightTexture()
        {
            if (_activePipeline == null)
                throw new InvalidOperationException("No rendering pipeline has been initialized.");

            return _activePipeline.GetLightTexture();
        }

        public static Size GetLightTextureSize()
            => _activePipeline?.GetLightTextureSize() ?? new Size(1, 1);

        public static RenderTexture GetPoisonTexture()
        {
            if (_activePipeline == null)
                throw new InvalidOperationException("No rendering pipeline has been initialized.");

            return _activePipeline.GetPoisonTexture();
        }

        public static Size GetPoisonTextureSize()
            => _activePipeline?.GetPoisonTextureSize() ?? new Size(1, 1);

        public static TextureFilterMode GetTextureFilter()
            => _activePipeline?.GetTextureFilter() ?? _fallbackTextureFilter;

        public static void SetTextureFilter(TextureFilterMode mode)
        {
            if (_activePipeline != null)
                _activePipeline.SetTextureFilter(mode);
            else
                _fallbackTextureFilter = mode;
        }

        public static void Clear(RenderClearFlags flags, Color colour, float z, int stencil, params Rectangle[] regions)
            => _activePipeline?.Clear(flags, colour, z, stencil, regions);

        public static void FlushSprite() => _activePipeline?.FlushSprite();

        public static void RegisterControlCache(ITextureCacheItem control)
        {
            if (control == null)
                throw new ArgumentNullException(nameof(control));

            if (_activePipeline != null)
            {
                _activePipeline.RegisterControlCache(control);
                return;
            }

            if (!FallbackControlCache.Contains(control))
                FallbackControlCache.Add(control);
        }

        public static void UnregisterControlCache(ITextureCacheItem control)
        {
            if (control == null)
                return;

            if (_activePipeline != null)
            {
                _activePipeline.UnregisterControlCache(control);
                return;
            }

            FallbackControlCache.Remove(control);
        }

        public static RenderTexture CreateTexture(Size size, RenderTextureFormat format, RenderTextureUsage usage, RenderTexturePool pool)
        {
            if (_activePipeline != null)
                return _activePipeline.CreateTexture(size, format, usage, pool);

            throw new InvalidOperationException("Rendering pipeline is not initialized.");
        }

        public static void ReleaseTexture(RenderTexture texture)
        {
            if (!texture.IsValid)
                return;

            if (_activePipeline != null)
            {
                _activePipeline.ReleaseTexture(texture);
                return;
            }

            throw new InvalidOperationException("Rendering pipeline is not initialized.");
        }

        public static TextureLock LockTexture(RenderTexture texture, TextureLockMode mode)
        {
            if (!texture.IsValid)
                throw new ArgumentException("A valid texture handle is required.", nameof(texture));

            if (_activePipeline != null)
                return _activePipeline.LockTexture(texture, mode);

            throw new InvalidOperationException("Rendering pipeline is not initialized.");
        }

        public static void RegisterTextureCache(ITextureCacheItem texture)
        {
            if (texture == null)
                throw new ArgumentNullException(nameof(texture));

            if (_activePipeline != null)
            {
                _activePipeline.RegisterTextureCache(texture);
                return;
            }

            if (!FallbackTextureCache.Contains(texture))
                FallbackTextureCache.Add(texture);
        }

        public static void UnregisterTextureCache(ITextureCacheItem texture)
        {
            if (texture == null)
                return;

            if (_activePipeline != null)
            {
                _activePipeline.UnregisterTextureCache(texture);
                return;
            }

            FallbackTextureCache.Remove(texture);
        }

        public static void RegisterSoundCache(ISoundCacheItem sound)
        {
            if (sound == null)
                throw new ArgumentNullException(nameof(sound));

            if (_activePipeline != null)
            {
                _activePipeline.RegisterSoundCache(sound);
                return;
            }

            if (!FallbackSoundCache.Contains(sound))
                FallbackSoundCache.Add(sound);
        }

        public static void UnregisterSoundCache(ISoundCacheItem sound)
        {
            if (sound == null)
                return;

            if (_activePipeline != null)
            {
                _activePipeline.UnregisterSoundCache(sound);
                return;
            }

            FallbackSoundCache.Remove(sound);
        }

        public static IReadOnlyList<ISoundCacheItem> GetRegisteredSoundCaches()
            => _activePipeline?.GetRegisteredSoundCaches() ?? FallbackSoundCache;

        public static void MemoryClear()
        {
            if (_activePipeline != null)
            {
                _activePipeline.MemoryClear();
                return;
            }

            FallbackControlCache.Clear();
            FallbackTextureCache.Clear();
            FallbackSoundCache.Clear();
        }

        // ---- 精灵着色器特效（Canvas 2D 暂以占位实现，保留 API 以便后续移植）----

        internal enum SpriteShaderEffectKind
        {
            Grayscale,
            SolidShadowFill,
            Outline,
            DropShadow,
        }

        internal readonly struct SpriteShaderEffectRequest
        {
            public SpriteShaderEffectRequest(SpriteShaderEffectKind kind, float opacity = 1F)
            {
                Kind = kind;
                Opacity = opacity;
                Outline = default;
                DropShadow = default;
            }

            public SpriteShaderEffectRequest(OutlineEffectSettings outline)
            {
                Kind = SpriteShaderEffectKind.Outline;
                Opacity = 1F;
                Outline = outline;
                DropShadow = default;
            }

            public SpriteShaderEffectRequest(DropShadowEffectSettings dropShadow)
            {
                Kind = SpriteShaderEffectKind.DropShadow;
                Opacity = dropShadow.StartOpacity;
                Outline = default;
                DropShadow = dropShadow;
            }

            public SpriteShaderEffectKind Kind { get; }
            public float Opacity { get; }
            public OutlineEffectSettings Outline { get; }
            public DropShadowEffectSettings DropShadow { get; }
        }

        internal readonly struct OutlineEffectSettings
        {
            public OutlineEffectSettings(Color colour, float thickness)
            {
                Colour = colour;
                Thickness = thickness;
            }

            public Color Colour { get; }
            public float Thickness { get; }
        }

        internal readonly struct DropShadowEffectSettings
        {
            public DropShadowEffectSettings(Color colour, float width, float startOpacity, RectangleF? visibleBounds)
            {
                Colour = colour;
                Width = width;
                StartOpacity = startOpacity;
                VisibleBounds = visibleBounds;
            }

            public Color Colour { get; }
            public float Width { get; }
            public float StartOpacity { get; }
            public RectangleF? VisibleBounds { get; }
        }
    }
}
