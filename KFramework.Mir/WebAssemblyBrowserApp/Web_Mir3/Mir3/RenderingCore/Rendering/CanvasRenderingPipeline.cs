using System;
using System.Collections.Generic;
using MirEngine;
using System.Numerics;
using System.Runtime.InteropServices;
using MirClient.Assets;
using Shared.Rendering;

namespace Shared.Rendering
{
    /// <summary>
    /// HTML5 Canvas 渲染管线：以 BrowserCanvas（main.js 的 mir.cr*）为后端实现 IRenderingPipeline。
    /// 所有绘制目标/纹理用 int 句柄标识（0=主画布，其余=JS 端 Image 或离屏 canvas）。
    /// </summary>
    public sealed class CanvasRenderingPipeline : IRenderingPipeline
    {
        private RenderingPipelineContext _context;
        private int _currentSurfaceId = BrowserCanvas.MainTarget;
        private Size _backBufferSize = new Size(1024, 768);

        // 真实 Zircon 客户端（MirLibrary/MirImage）通过 CreateTexture + LockTexture + Marshal.Copy 上传像素，
        // 但平台上没有原生指针，因此这里用"int 句柄 + 托管 RGBA 缓冲"模拟：
        //   CreateTexture 只分配一个 int 句柄并记录尺寸/格式；
        //   LockTexture 钉住一块 RGBA 缓冲，调用方把（DXT 或已解压的）字节拷进来；
        //   解锁时把缓冲解码为 RGBA 并上传到 canvas（mir.createImage）。
        private int _nextImageId = 1;
        private int _mainClearLogCount = 0;
        private readonly Dictionary<int, Size> _textureSizes = new Dictionary<int, Size>();
        private readonly Dictionary<int, RenderTextureFormat> _textureFormats = new Dictionary<int, RenderTextureFormat>();
        private static int _texLogCount = 0;

        // 无效纹理返回占位锁，避免使用零指针触发 TextureLock.From 的异常。
        private static readonly byte[] _sentinel = new byte[1];
        private static readonly GCHandle _sentinelHandle = GCHandle.Alloc(_sentinel, GCHandleType.Pinned);
        private static readonly TextureLock _placeholderLock =
            TextureLock.From(_sentinelHandle.AddrOfPinnedObject(), 1, () => { });

        private float _opacity = 1f;
        private bool _blending;
        private float _blendRate = 1f;
        private BlendMode _blendMode = BlendMode.NORMAL;
        private float _lineWidth = 1f;
        private TextureFilterMode _textureFilter = TextureFilterMode.Point;

        public string Id => "Canvas";

        public void Initialize(RenderingPipelineContext context)
        {
            _context = context;
            _currentSurfaceId = BrowserCanvas.MainTarget;
            BrowserCanvas.SetTarget(BrowserCanvas.MainTarget);
        }

        public void RunMessageLoop(Action loop)
        {
            // 浏览器端由 main.js 的 requestAnimationFrame 驱动 game.Frame，无需自启循环
        }

        public bool RenderFrame(Action drawScene)
        {
            _currentSurfaceId = BrowserCanvas.MainTarget;
            BrowserCanvas.SetTarget(BrowserCanvas.MainTarget);
            drawScene();
            BrowserCanvas.Flush();
            return true;
        }

        public bool SupportsCachedRenderTargets => false;
        public bool SupportsAtlasTextures => false;
        public bool SupportsBc7Textures => false;

        public void ToggleFullScreen() { }
        public void SetResolution(Size size) => _backBufferSize = size;
        public void SetTargetMonitor(int monitorIndex) { }
        public void CenterOnSelectedMonitor() { }
        public void ResetDevice() { }
        public void OnSceneChanged(bool isGameScene) { }
        public IReadOnlyList<Size> GetSupportedResolutions() => Array.Empty<Size>();
        public IReadOnlyList<GraphicsAdapterInfo> GetGraphicsAdapters(string pipelineId) => Array.Empty<GraphicsAdapterInfo>();

        public Size MeasureText(string text, MirEngine.Font font)
            => BrowserCanvas.MeasureText(text, ToCss(font), 0);
        public Size MeasureText(string text, MirEngine.Font font, Size proposedSize)
            => BrowserCanvas.MeasureText(text, ToCss(font), proposedSize.Width);
        public Size MeasureText(string text, MirEngine.Font font, Size proposedSize, TextFormatFlags flags)
            => BrowserCanvas.MeasureText(text, ToCss(font), proposedSize.Width);

        private static string ToCss(MirEngine.Font font)
            => font == null ? "12px sans-serif" : ToCss(font);

        public float GetHorizontalDpi() => 96;

        public Color ConvertHslToRgb(float h, float s, float l)
        {
            h = ((h % 360f) + 360f) % 360f;
            float c = (1f - Math.Abs(2f * l - 1f)) * s;
            float x = c * (1f - Math.Abs(((h / 60f) % 2f) - 1f));
            float m = l - c / 2f;

            float r = 0, g = 0, b = 0;
            if (h < 60) { r = c; g = x; }
            else if (h < 120) { r = x; g = c; }
            else if (h < 180) { g = c; b = x; }
            else if (h < 240) { g = x; b = c; }
            else if (h < 300) { r = x; b = c; }
            else { r = c; b = x; }

            return Color.FromArgb(
                (int)Math.Round((r + m) * 255),
                (int)Math.Round((g + m) * 255),
                (int)Math.Round((b + m) * 255));
        }

        public void SetOpacity(float opacity) => _opacity = opacity;
        public float GetOpacity() => _opacity;
        public void SetBlend(bool enabled, float rate = 1f, BlendMode mode = BlendMode.NORMAL)
        {
            _blending = enabled;
            _blendRate = rate;
            _blendMode = mode;
            BrowserCanvas.SetBlendState((int)mode, rate, enabled);
        }
        public bool IsBlending() => _blending;
        public float GetBlendRate() => _blendRate;
        public BlendMode GetBlendMode() => _blendMode;

        public float GetLineWidth() => _lineWidth;
        public void SetLineWidth(float width) => _lineWidth = width;

        public void DrawLine(IReadOnlyList<LinePoint> points, Color colour)
        {
            if (points == null || points.Count < 2) return;
            int argb = colour.ToArgb();
            for (int i = 0; i + 1 < points.Count; i++)
                BrowserCanvas.DrawLine(points[i].X, points[i].Y, points[i + 1].X, points[i + 1].Y, _lineWidth, argb);
        }

        public void FlushLines() { }

        public void DrawTexture(RenderTexture texture, Rectangle sourceRectangle, RectangleF destinationRectangle, Color colour)
        {
            if (!texture.IsValid) return;
            int id = (int)texture.NativeHandle;
            BrowserCanvas.DrawImage(id, sourceRectangle.X, sourceRectangle.Y, sourceRectangle.Width, sourceRectangle.Height,
                destinationRectangle.X, destinationRectangle.Y, destinationRectangle.Width, destinationRectangle.Height, colour.ToArgb());
        }

        public void DrawTexture(RenderTexture texture, Rectangle? sourceRectangle, Matrix3x2 transform, Vector3 center, Vector3 translation, Color colour)
        {
            if (!texture.IsValid) return;
            int id = (int)texture.NativeHandle;
            Rectangle src = sourceRectangle ?? new Rectangle(0, 0, _backBufferSize.Width, _backBufferSize.Height);

            // 复刻原版（D3D9/D3D11）DrawTexture 的变换契约：
            // 把"基础几何 (0,0,srcW,srcH)" 经 finalTransform 变换后绘制。
            //   finalTransform = translate(-center) * transform;   // center 作为缩放/旋转中心
            //   finalTransform.M31 += translation.X;               // 平移叠加到矩阵
            //   finalTransform.M32 += translation.Y;
            Matrix3x2 finalTransform = transform;
            if (center.X != 0 || center.Y != 0)
                finalTransform = Matrix3x2.CreateTranslation(-center.X, -center.Y) * finalTransform;
            finalTransform.M31 += translation.X;
            finalTransform.M32 += translation.Y;

            BrowserCanvas.DrawImageTransform(id, src.X, src.Y, src.Width, src.Height, finalTransform, colour.ToArgb());
        }

        public void BeginSpriteBatch() { }
        public void QueueSprite(RenderTexture texture, Rectangle sourceRectangle, RectangleF destinationRectangle, Color colour)
            => DrawTexture(texture, sourceRectangle, destinationRectangle, colour);
        public void EndSpriteBatch() { }

        public RenderSurface GetCurrentSurface() => RenderSurface.From(_currentSurfaceId);
        public void SetSurface(RenderSurface surface)
        {
            if (!surface.IsValid) return;
            _currentSurfaceId = (int)surface.NativeHandle;
            BrowserCanvas.SetTarget(_currentSurfaceId);
        }
        // 离屏“临时画布”：原版 Zircon 用一块与主画布同尺寸的共享 Scratch 缓冲，
        // 供 DXButton / DXTabControl 等控件临时绘制后再合成到目标。这里必须返回真正的离屏
        // 纹理，绝不能返回 MainTarget（id=0）——否则控件会直接画到主画布上，且其 Clear 会把
        // 主画布（含登录背景）整屏抹黑。
        private RenderTargetResource _scratchTarget;
        private RenderTargetResource ScratchTarget
        {
            get
            {
                if (!_scratchTarget.Surface.IsValid)
                    _scratchTarget = CreateRenderTarget(_backBufferSize);
                return _scratchTarget;
            }
        }
        public RenderSurface GetScratchSurface() => ScratchTarget.Surface;
        public RenderTexture GetScratchTexture() => ScratchTarget.Texture;

        public void ColorFill(RenderSurface surface, Rectangle rectangle, Color colorFill)
        {
            if (!surface.IsValid) return;
            int prev = _currentSurfaceId;
            _currentSurfaceId = (int)surface.NativeHandle;
            BrowserCanvas.SetTarget(_currentSurfaceId);
            BrowserCanvas.FillRect(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, colorFill.ToArgb());
            _currentSurfaceId = prev;
            BrowserCanvas.SetTarget(prev);
        }

        public RenderTargetResource CreateRenderTarget(Size size)
        {
            int id = BrowserCanvas.CreateOffscreen(Math.Max(1, size.Width), Math.Max(1, size.Height));
            return RenderTargetResource.From(RenderTexture.From(id), RenderSurface.From(id));
        }
        public void ReleaseRenderTarget(RenderTargetResource renderTarget) { }

        public Size GetBackBufferSize() => _backBufferSize;

        public void Clear(RenderClearFlags flags, Color colour, float z, int stencil, params Rectangle[] regions)
        {
            if (_currentSurfaceId == BrowserCanvas.MainTarget && _mainClearLogCount < 15)
            {
                _mainClearLogCount++;
                BrowserResource.Log("[C#-ClearMain] cur=" + _currentSurfaceId + " stack:\n" + Environment.StackTrace);
            }
            BrowserCanvas.Clear(colour.R, colour.G, colour.B, colour.A);
        }

        public void FlushSprite() { }

        public void RegisterControlCache(ITextureCacheItem control) { }
        public void UnregisterControlCache(ITextureCacheItem control) { }

        public RenderTexture CreateTexture(Size size, RenderTextureFormat format, RenderTextureUsage usage, RenderTexturePool pool)
        {
            int id = _nextImageId++;
            _textureSizes[id] = size;
            _textureFormats[id] = format;
            return RenderTexture.From(id);
        }

        public void ReleaseTexture(RenderTexture texture)
        {
            if (!texture.IsValid) return;
            int id = (int)texture.NativeHandle;
            _textureSizes.Remove(id);
            _textureFormats.Remove(id);
            BrowserCanvas.DisposeImage(id);
        }

        public TextureLock LockTexture(RenderTexture texture, TextureLockMode mode)
        {
            if (!texture.IsValid) return _placeholderLock;

            int id = (int)texture.NativeHandle;
            if (!_textureSizes.TryGetValue(id, out Size size) || !_textureFormats.TryGetValue(id, out RenderTextureFormat format))
                return _placeholderLock;

            int w = Math.Max(1, size.Width);
            int h = Math.Max(1, size.Height);
            int bytes = w * h * 4;
            if (bytes <= 0) return _placeholderLock;

            // 钉住一块 RGBA 缓冲，调用方（MirImage）会把（DXT 或已解压）字节拷进来；
            // 解锁时再解码为 RGBA 并上传到 canvas。
            byte[] buffer = new byte[bytes];
            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

            return TextureLock.From(handle.AddrOfPinnedObject(), w * 4, () =>
            {
                handle.Free();

                try
                {
                    byte[] rgba;
                    if (format == RenderTextureFormat.Dxt1 || format == RenderTextureFormat.Dxt5)
                    {
                        int blocksX = (w + 3) / 4;
                        int blocksY = (h + 3) / 4;
                        int blockSize = format == RenderTextureFormat.Dxt1 ? 8 : 16;
                        int dxtLen = blocksX * blocksY * blockSize;
                        // 限定为客户端自带实现：通用库 MirEngine 也导出了同名 MirEngine.DxtDecoder，直接写会歧义。
                        rgba = MirClient.Assets.DxtDecoder.Decode(buffer.AsSpan(0, Math.Min(dxtLen, buffer.Length)), w, h, format == RenderTextureFormat.Dxt1);
                    }
                    else
                    {
                        rgba = buffer;
                    }

                    BrowserCanvas.UploadImage(id, rgba, w, h);
                }
                catch (Exception ex)
                {
                    if (_texLogCount < 15) { _texLogCount++; BrowserResource.Log($"[Mir][Tex] 上传异常 id={id} fmt={format}: {ex.Message}"); }
                    // 单张纹理上传失败不应中断整帧
                }
            });
        }

        public void RegisterTextureCache(ITextureCacheItem texture) { }
        public void UnregisterTextureCache(ITextureCacheItem texture) { }

        public void RegisterSoundCache(ISoundCacheItem sound) { }
        public void UnregisterSoundCache(ISoundCacheItem sound) { }
        public IReadOnlyList<ISoundCacheItem> GetRegisteredSoundCaches() => Array.Empty<ISoundCacheItem>();

        public void MemoryClear() { }

        // 这些纹理同样必须返回真正的离屏纹理（原版返回的是 1x1 调色板/光照缓冲），
        // 返回 MainTarget(id=0) 会让使用方拿到无效纹理（crDraw 时 found=0 被跳过）。
        private RenderTexture _colourPaletteTexture;
        private RenderTexture _lightTexture;
        private RenderTexture _poisonTexture;
        private RenderTexture GetCachedTexture(ref RenderTexture field)
        {
            if (!field.IsValid)
                field = CreateRenderTarget(new Size(1, 1)).Texture;
            return field;
        }
        public RenderTexture GetColourPaletteTexture() => GetCachedTexture(ref _colourPaletteTexture);
        public byte[] GetColourPaletteData() => Array.Empty<byte>();
        public RenderTexture GetLightTexture() => GetCachedTexture(ref _lightTexture);
        public Size GetLightTextureSize() => new Size(1, 1);
        public RenderTexture GetPoisonTexture() => GetCachedTexture(ref _poisonTexture);
        public Size GetPoisonTextureSize() => new Size(1, 1);

        public TextureFilterMode GetTextureFilter() => _textureFilter;
        public void SetTextureFilter(TextureFilterMode mode) => _textureFilter = mode;

        public void Shutdown() { }
    }
}
