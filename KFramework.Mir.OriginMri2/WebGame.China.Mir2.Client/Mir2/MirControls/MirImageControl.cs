using System;
using System.Threading;
using System.Threading.Tasks;
using Client.MirGraphics;
using Client.MirObjects;   // Mir2OriginResource / DXManager 等

namespace Client.MirControls
{
    // 填充裁剪起点：Top/Bottom=竖直（从上/下开始），Left/Right=水平。
    public enum MirFillOrigin { Left, Right, Top, Bottom }

    // 绘制模式：Normal=整图缩放；NineGrid=九宫格（保留四角四边厚度，仅中间随 Size 拉伸）。
    public enum MirImageDrawMode { Normal, NineGrid }

    public class MirImageControl : MirControl
    {
        public override Point DisplayLocation { get { return UseOffSet ? base.DisplayLocation.Add(Library.GetOffSet(Index)) : base.DisplayLocation; } }
        public Point DisplayLocationWithoutOffSet { get { return base.DisplayLocation; } }

        // 绘制尺寸缩放（仅影响本控件图像绘制，不影响布局/命中）。默认 1 = 原尺寸。
        public float SizeScale { get; set; } = 1f;

        // 白鹭 MovieClip 原生帧对齐：开启后图集帧按【子图原始尺寸】绘制（不拉伸到 Size），
        // 且逐帧偏移用下面的统一缩放换算。默认关闭，其它控件行为不变。
        // 由 MirAnimatedAtlasControl.SetMovieClip 统一装配，外部不要逐个赋值。
        protected bool AtlasFrameNativeSize { get; set; }

        // AtlasFrameNativeSize 开启时，帧尺寸与逐帧偏移共用的统一缩放系数（如界面 ScaleF）。
        protected float AtlasFrameScale { get; set; } = 1f;

        // 填充裁剪（类似 Unity Image.fillAmount）：FillAmount<1 时只绘制图像的指定比例部分，不拉伸图像。
        // 用于固定形状（如半月血条）按比例显隐，避免改 Size 把形状压变形。
        public float FillAmount { get; set; } = 1f;
        // 填充起点（默认竖直：从顶部开始向下减少）。
        public MirFillOrigin FillOrigin { get; set; } = MirFillOrigin.Top;

        // 九宫格边框厚度（像素，基于源图）。四边任一 >0 且 DrawMode=NineGrid 时生效：
        // 四角原尺寸保留、四边单轴拉伸、中心双轴拉伸，UI 面板/边框随 Size 缩放不变形。
        public int BorderLeft { get; set; } = 0;
        public int BorderTop { get; set; } = 0;
        public int BorderRight { get; set; } = 0;
        public int BorderBottom { get; set; } = 0;
        // 绘制模式：默认 Normal（整图缩放）。NineGrid 时按上方四个边框做九宫格。
        public MirImageDrawMode DrawMode { get; set; } = MirImageDrawMode.Normal;

        // 翻转:FlipX=左右镜像, FlipY=上下镜像(作用于 AtlasFrame 绘制,Normal 模式;九宫格模式不翻转)。
        public bool FlipX { get; set; } = false;
        public bool FlipY { get; set; } = false;
        private KFramework.MonoGame.SpriteEffects FlipEffects
        {
            get
            {
                var e = KFramework.MonoGame.SpriteEffects.None;
                if (FlipX) e |= KFramework.MonoGame.SpriteEffects.FlipHorizontally;
                if (FlipY) e |= KFramework.MonoGame.SpriteEffects.FlipVertically;
                return e;
            }
        }

        private bool _autoSize;
        public bool AutoSize
        {
            get { return _autoSize; }
            set
            {
                if (_autoSize == value)
                    return;
                _autoSize = value;
                OnAutoSizeChanged(EventArgs.Empty);
            }
        }
        public event EventHandler AutoSizeChanged;
        private void OnAutoSizeChanged(EventArgs e)
        {
            TextureValid = false;
            if (AutoSizeChanged != null)
                AutoSizeChanged.Invoke(this, e);
        }

        private bool _drawImage;
        public bool DrawImage
        {
            get { return _drawImage; }
            set
            {
                if (_drawImage == value)
                    return;
                _drawImage = value;
                OnDrawImageChanged();
            }
        }
        public event EventHandler DrawImageChanged;
        private void OnDrawImageChanged()
        {
            Redraw();
            if (DrawImageChanged != null)
                DrawImageChanged.Invoke(this, EventArgs.Empty);
        }

        private int _index;
        public virtual int Index
        {
            get { return _index; }
            set
            {
                if (_index == value)
                    return;
                _index = value;
                OnIndexChanged();
            }
        }
        public event EventHandler IndexChanged;
        protected void OnIndexChanged()
        {
            OnSizeChanged();
            if (IndexChanged != null)
                IndexChanged.Invoke(this, EventArgs.Empty);
        }

        private MLibrary _library;
        public MLibrary Library
        {
            get { return _library; }
            set
            {
                if (_library == value)
                    return;
                _library = value;
                OnLibraryChanged();
            }
        }
        public event EventHandler LibraryChanged;
        private void OnLibraryChanged()
        {
            OnSizeChanged();
            if (LibraryChanged != null)
                LibraryChanged.Invoke(this, EventArgs.Empty);
        }

        private Mir2AtlasSheet _atlasSheet;
        public Mir2AtlasSheet AtlasSheet
        {
            get { return _atlasSheet; }
            set
            {
                if (_atlasSheet == value) return;
                _atlasSheet = value;
                OnLibraryChanged();
            }
        }

        private string _atlasFrame;
        public virtual string AtlasFrame
        {
            get { return _atlasFrame; }
            set
            {
                if (_atlasFrame == value) return;
                _atlasFrame = value;
                OnLibraryChanged();
            }
        }

        // —— 便捷加载入口：设一个字符串（支持 "sheet#frame" 或别名，见 Mir2AtlasLibrary.ResolveName），
        //    即自动异步加载该图集帧。同一图集的下载会被 Mir2AtlasLibrary 按 sheet key 去重（多控件共享一次），
        //    但每个控件各自持有完成事件 AtlasLoaded。加载完成后于主线程赋值并触发事件。
        private string _atlasNameKey;
        public string AtlasNameKey
        {
            get { return _atlasNameKey; }
            set
            {
                if (_atlasNameKey == value) return;
                _atlasNameKey = value;
                _ = LoadAtlasAsync(value);
            }
        }

        /// <summary>图集加载完成事件（主线程触发）：sender=本控件，e=EventArgs.Empty。</summary>
        public event EventHandler AtlasLoaded;

        private CancellationTokenSource _atlasCts;

        private async Task LoadAtlasAsync(string key)
        {
            // 取消上一次在途加载（重设 key / 切换资源时，避免旧结果覆盖新结果）。
            _atlasCts?.Cancel();
            _atlasCts?.Dispose();
            var cts = new CancellationTokenSource();
            _atlasCts = cts;
            var ct = cts.Token;

            if (string.IsNullOrWhiteSpace(key))
                return;

            var (sheetKey, frame) = Mir2AtlasLibrary.ResolveName(key);
            try
            {
                var sheet = await Mir2AtlasLibrary.GetSheetAsync(sheetKey, DXManager.GDevice, CancellationToken.None);
                if (ct.IsCancellationRequested) return;

                if (ct.IsCancellationRequested) return;
                AtlasSheet = sheet;
                if (!string.IsNullOrEmpty(frame))
                {
                    AtlasFrame = frame;
                }
                Redraw();
                ApplyAnchor();
                AtlasLoaded?.Invoke(this, EventArgs.Empty);
            }
            catch (TaskCanceledException) { }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                KFramework.MonoGame.PrintTool.LogError("[MirImageControl] AtlasNameKey 加载失败：" + ex);
            }
        }

        // —— 单张图远程加载：设一个图片 CDN 相对路径/URL，控件自动异步下载并解码为 Texture2D，
        //    绘制走 DrawControl 顶部已有的 Texture 通道（拉伸到 Size、按 ForeColour 着色）。
        //    可用于小地图/大地图等「按需远程取一张图」的场景。设 null/空串会清空 Texture。
        private string _imageUrl;
        public string ImageUrl
        {
            get { return _imageUrl; }
            set
            {
                if (_imageUrl == value) return;
                _imageUrl = value;
                _ = LoadRemoteImageAsync(value);
            }
        }

        // —— 按 manifest key 加载单图背景：设一个资源逻辑路径（如 resource/assets/gameui4/jpg/y_task_bg.png），
        //    控件自动经 manifest 解析成 CDN 哈希路径并异步下载解码为 Texture。等价于调用 SetManifestImageAsync，
        //    适合「用 manifest 资源当背景」的场景（区别于 ImageUrl 直接拼 CDN 相对路径会 404）。设 null/空串清空 Texture。
        private string _imageKey;
        public string ImageKey
        {
            get { return _imageKey; }
            set
            {
                if (_imageKey == value) return;
                _imageKey = value;
                _ = SetManifestImageAsync(value);
            }
        }

        /// <summary>远程单图加载完成事件（主线程触发）：sender=本控件，e=EventArgs.Empty。</summary>
        public event EventHandler RemoteImageLoaded;

        private CancellationTokenSource _remoteCts;

        private async Task LoadRemoteImageAsync(string url)
        {
            // 取消上一次在途加载（重设 url / 切换资源时，避免旧结果覆盖新结果）。
            _remoteCts?.Cancel();
            _remoteCts?.Dispose();
            var cts = new CancellationTokenSource();
            _remoteCts = cts;
            var ct = cts.Token;

            if (string.IsNullOrWhiteSpace(url))
            {
                Texture = null;
                Redraw();
                return;
            }

            try
            {
                var tex = await Mir2OriginResource.GetTextureAsync(url, DXManager.GDevice, ct);
                if (ct.IsCancellationRequested) return;
                Texture = tex;
                Redraw();
                ApplyAnchor();
                RemoteImageLoaded?.Invoke(this, EventArgs.Empty);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                //KFramework.MonoGame.PrintTool.LogError("[MirImageControl] ImageUrl 加载失败：" + url + " -> " + ex);
            }
        }

        /// <summary>按 manifest key 加载远程单图（如 gameui4/jpg/*.jpg 这类独立 jpg 底图）。
        /// 与 ImageUrl 的区别：ImageUrl 直接把 url 当路径拼到资源根，在 CDN 上会 404；
        /// 本方法经 Mir2AtlasManifest 把 key 解析成 CDN 哈希布局路径 resource/{hash前2}/{hash}_{s}.ext 再下载。
        /// 复用 _remoteCts 取消上一次在途加载，避免旧结果覆盖新结果。</summary>
        public async Task SetManifestImageAsync(string key)
        {
            _remoteCts?.Cancel();
            _remoteCts?.Dispose();
            var cts = new CancellationTokenSource();
            _remoteCts = cts;
            var ct = cts.Token;

            if (string.IsNullOrWhiteSpace(key))
            {
                Texture = null;
                Redraw();
                return;
            }

            try
            {
                if (Mir2OriginResource.Manifest == null)
                    await Mir2OriginResource.LoadManifestAsync(ct);
                var tex = await Mir2OriginResource.GetManifestAssetTextureAsync(key, DXManager.GDevice, ct);
                if (ct.IsCancellationRequested) return;
                Texture = tex;
                Redraw();
                ApplyAnchor();
                RemoteImageLoaded?.Invoke(this, EventArgs.Empty);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                KFramework.MonoGame.PrintTool.LogError("[MirImageControl] manifest 图片加载失败：" + key + " -> " + ex);
            }
        }

        // 逐像素命中：true 时点中透明像素不算命中（透明区穿透），false 时整框都算命中。
        private bool _pixelDetect;
        protected bool PixelDetect
        {
            set
            {
                if (_pixelDetect == value)
                    return;
                _pixelDetect = value;
                OnPixelDetectChanged();
            }
        }
        public event EventHandler PixelDetectChanged;
        private void OnPixelDetectChanged()
        {
            Redraw();
            if (PixelDetectChanged != null)
                PixelDetectChanged.Invoke(this, EventArgs.Empty);
        }

        private bool _useOffSet;
        public bool UseOffSet
        {
            protected get { return _useOffSet; }
            set
            {
                if (_useOffSet == value)
                    return;
                _useOffSet = value;
                OnUseOffSetChanged();
            }
        }
        public event EventHandler UseOffSetChanged;
        private void OnUseOffSetChanged()
        {
            OnLocationChanged();
            if (UseOffSetChanged != null)
                UseOffSetChanged.Invoke(this, EventArgs.Empty);
        }

        // 锚点（归一化 0~1，默认左上）：绘制时图像以自身该点对齐到布局框对应点，便于多帧动画尺寸不一致时统一对齐。
        private PointF _pivot = new PointF(0f, 0f);
        public PointF Pivot
        {
            get { return _pivot; }
            set { if (_pivot == value) return; _pivot = value; Redraw(); }
        }

        // 逐帧绘制偏移（白鹭 MovieClip 每帧 x/y）：默认无偏移；动画控件按当前帧覆盖，对齐原始 MovieClip 表现。
        protected virtual Point FrameDrawOffset => default;

        // MovieClip 统一注册点路径：返回当前帧的【完整矩形】(x,y,w,h)，逻辑像素、未缩放。
        // 由 EgretMovieClip 用「时间线 x/y + 图集子图 w/h」一次性算好，所有帧共用同一原点，
        // 脚底 (Y+Height) 全帧恒定。返回 true 时绘制侧只做一次整体缩放，不再拆偏移/尺寸换算。
        protected virtual bool TryGetMovieClipFrameRect(out KFramework.MonoGame.Rectangle rect)
        {
            rect = default;
            return false;
        }

        public override Size Size
        {
            set { base.Size = value; }
            get
            {
                Size result;
                if (AutoSize && Texture != null)
                    result = new Size(Texture.Width, Texture.Height);
                else if (AutoSize && AtlasSheet != null && !string.IsNullOrEmpty(AtlasFrame)
                    && AtlasSheet.Frames.TryGetValue(AtlasFrame, out var s))
                    result = new Size(s.SourceRectangle.Width, s.SourceRectangle.Height);
                else if (AutoSize && Library != null && Index >= 0)
                    result = Library.GetTrueSize(Index);
                else
                    result = base.Size;
                // SizeScale 直接收进 Size：绘制/布局框/命中区（DisplayRectangle 用 Size）一起缩放。
                if (SizeScale != 1f)
                    result = new Size((int)(result.Width * SizeScale), (int)(result.Height * SizeScale));
                return result;
            }
        }

        public override Size TrueSize
        {
            get
            {
                if (AtlasSheet != null && !string.IsNullOrEmpty(AtlasFrame)
                    && AtlasSheet.Frames.TryGetValue(AtlasFrame, out var s))
                    return new Size(s.SourceRectangle.Width, s.SourceRectangle.Height);
                if (Library != null && Index >= 0)
                    return Library.GetTrueSize(Index);
                return base.TrueSize;
            }
        }


        public MirImageControl()
        {
            _drawImage = true;
            _index = -1;
            ForeColour = Color.White;
            _autoSize = true;
        }

        /// <summary>直接渲染的纹理(引擎默认图,如 KDefaultRes.DefaultTexture2D 的 1×1 白纹理)。
        /// 设置后 DrawControl 会跳过 Libraries.* 图集,把该纹理拉伸到 Size 并按 ForeColour 着色。</summary>
        public KFramework.MonoGame.Texture2D Texture { get; set; }

        protected internal override void DrawControl()
        {
            base.DrawControl();
            if (!DrawImage) return;

            if (Texture != null)
            {
                Color drawColor = ForeColour;
                if (Opacity < 1f)
                {
                    drawColor = new Color(drawColor.R, drawColor.G, drawColor.B, (byte)(drawColor.A * Opacity));
                }

                Size drawSize = Size;
                int px = DisplayLocation.X - (int)(_pivot.X * drawSize.Width);
                int py = DisplayLocation.Y - (int)(_pivot.Y * drawSize.Height);
                var srcRect = new KFramework.MonoGame.Rectangle(0, 0, Texture.Width, Texture.Height);
                var dstRect = new KFramework.MonoGame.Rectangle(px, py, drawSize.Width, drawSize.Height);
                DXManager.Draw(Texture, srcRect, dstRect, drawColor, (Blending ? MirBlend.Additive : MirBlend.Normal).ToBlendState(), FlipEffects);
                return;
            }

            if (AtlasSheet != null && !string.IsNullOrEmpty(AtlasFrame)
                && AtlasSheet.Frames.TryGetValue(AtlasFrame, out var atlasSprite))
            {
                Color drawColor = ForeColour;
                if (Opacity < 1f)
                {
                    drawColor = new Color(drawColor.R, drawColor.G, drawColor.B, (byte)(drawColor.A * Opacity));
                }

                bool oldGray = DXManager.GrayScale;
                if (GrayScale)
                {
                    DXManager.SetGrayscale(true);
                }

                // 目标矩形按 Pivot（归一化锚点，默认左上）对齐：图像以自身 Pivot 点落在 DisplayLocation。
                // Size 已含 SizeScale（见 Size 重写），此处直接用 Size，避免重复乘。
                KFramework.MonoGame.Rectangle srcFull = atlasSprite.SourceRectangle;

                Size drawSize;
                int px, py;
                if (TryGetMovieClipFrameRect(out var clipRect))
                {
                    // MovieClip 统一注册点：整帧矩形 (x,y,w,h) 都在「同一原点」下。
                    // 用四条边各自缩放取整，而不是「top + Round(height)」——后者在
                    // 逐帧 w/h 不同时会因两次独立取整让脚底抖 1px。
                    // clipRect.Y + clipRect.Height 全帧恒定，所以 bottom 严格恒定，脚不会动。
                    float s = AtlasFrameScale;
                    int left = DisplayLocation.X + (int)Math.Round(clipRect.X * s);
                    int right = DisplayLocation.X + (int)Math.Round((clipRect.X + clipRect.Width) * s);
                    int top = DisplayLocation.Y + (int)Math.Round(clipRect.Y * s);
                    int bottom = DisplayLocation.Y + (int)Math.Round((clipRect.Y + clipRect.Height) * s);
                    drawSize = new Size(right - left, bottom - top);
                    px = left - (int)(_pivot.X * drawSize.Width);
                    py = top - (int)(_pivot.Y * drawSize.Height);
                }
                else
                {
                    // 非 MovieClip 的普通图集帧：按 Size 绘制，沿用旧的比例换算与逐帧偏移。
                    drawSize = Size;
                    float offsetScaleX, offsetScaleY;
                    if (AtlasFrameNativeSize && srcFull.Width > 0 && srcFull.Height > 0)
                    {
                        drawSize = new Size((int)Math.Round(srcFull.Width * AtlasFrameScale),
                                            (int)Math.Round(srcFull.Height * AtlasFrameScale));
                        offsetScaleX = offsetScaleY = AtlasFrameScale;
                    }
                    else
                    {
                        offsetScaleX = srcFull.Width > 0 ? (float)drawSize.Width / srcFull.Width : 1f;
                        offsetScaleY = srcFull.Height > 0 ? (float)drawSize.Height / srcFull.Height : 1f;
                    }
                    px = DisplayLocation.X - (int)(_pivot.X * drawSize.Width);
                    py = DisplayLocation.Y - (int)(_pivot.Y * drawSize.Height);

                    if (FrameDrawOffset.X != 0 || FrameDrawOffset.Y != 0)
                    {
                        px += (int)(FrameDrawOffset.X * offsetScaleX);
                        py += (int)(FrameDrawOffset.Y * offsetScaleY);
                    }
                }

                // 混合状态对齐原版 Crystal：用【控件自身的 Blending 属性】决定，而非全局 DXManager.Blending。
                var imgBlend = (Blending
                    ? MirBlend.Additive
                    : MirBlend.Normal).ToBlendState();

                // 九宫格：拆 9 块绘制，保留四角/四边厚度，仅中间随 Size 拉伸。
                int bordersAtlas = BorderLeft + BorderTop + BorderRight + BorderBottom;
                if (DrawMode == MirImageDrawMode.NineGrid && bordersAtlas > 0)
                {
                    ForEachNineGridRect(srcFull.Width, srcFull.Height, px, py, drawSize.Width, drawSize.Height,
                        BorderLeft, BorderTop, BorderRight, BorderBottom,
                        (src, dst) =>
                        {
                            var sR = new KFramework.MonoGame.Rectangle(srcFull.X + src.X, srcFull.Y + src.Y, src.Width, src.Height);
                            var dR = new KFramework.MonoGame.Rectangle((int)Math.Round(dst.X), (int)Math.Round(dst.Y), (int)Math.Round(dst.Width), (int)Math.Round(dst.Height));
                            DXManager.Draw(atlasSprite.Texture, sR, dR, drawColor, imgBlend);
                        });
                    if (GrayScale) DXManager.SetGrayscale(oldGray);
                    return;
                }

                KFramework.MonoGame.Rectangle dstFull = new KFramework.MonoGame.Rectangle(px, py, drawSize.Width, drawSize.Height);
                Rectangle srcSD = new Rectangle(srcFull.X, srcFull.Y, srcFull.Width, srcFull.Height);
                Rectangle dstSD = new Rectangle(dstFull.X, dstFull.Y, dstFull.Width, dstFull.Height);
                ApplyFillClip(FillAmount, FillOrigin, ref srcSD, ref dstSD);
                KFramework.MonoGame.Rectangle srcRect = new KFramework.MonoGame.Rectangle(srcSD.X, srcSD.Y, srcSD.Width, srcSD.Height);
                KFramework.MonoGame.Rectangle dstRect = new KFramework.MonoGame.Rectangle(dstSD.X, dstSD.Y, dstSD.Width, dstSD.Height);
                // 走 DXManager.Draw 安全入口（BeginSprite 会按当前上下文自动 Begin）：
                // 主渲染 / 场景烘焙通用；直接调 SpriteBatch.DrawFrame 在烘焙时主批未 Begin 会抛异常。
                DXManager.Draw(atlasSprite.Texture, srcRect, dstRect, drawColor, imgBlend, FlipEffects);

                if (GrayScale)
                {
                    DXManager.SetGrayscale(oldGray);
                }
                return;
            }

            if (Library != null)
            {
                // 资源缺失 / 尚未就绪：绘制默认占位图（灰色块），实现“无依赖 / 默认图片”兜底，避免界面空白。
                // （Index<0 是 Mir2“该层无图”的正常标记，保持透明不画占位。）
                if (Index >= 0 && Library.GetTrueSize(Index) == Size.Empty)
                {
                    DrawDefaultImage();
                    return;
                }
                bool oldGray = DXManager.GrayScale;

                if (GrayScale)
                {
                    DXManager.SetGrayscale(true);
                }

                // 以 Pivot 决定图像哪个点落在 DisplayLocation。Size 已含 SizeScale，直接用 Size。
                Size drawSize = Size;
                Point drawLoc = new Point(
                    DisplayLocation.X - (int)(_pivot.X * drawSize.Width),
                    DisplayLocation.Y - (int)(_pivot.Y * drawSize.Height));

                // 预乘透明度到颜色，再交给按 Size 缩放的重载（该重载走 DXManager.Draw，自身不处理 Opacity）。
                Color drawColor = ForeColour;
                if (Opacity < 1f)
                    drawColor = new Color(drawColor.R, drawColor.G, drawColor.B, (byte)(drawColor.A * Opacity));

                // 九宫格：拆 9 块绘制，保留四角/四边厚度，仅中间随 Size 拉伸。
                int bordersLib = BorderLeft + BorderTop + BorderRight + BorderBottom;
                if (DrawMode == MirImageDrawMode.NineGrid && bordersLib > 0)
                {
                    Size img = Library.GetTrueSize(Index);
                    ForEachNineGridRect(img.Width, img.Height, drawLoc.X, drawLoc.Y, drawSize.Width, drawSize.Height,
                        BorderLeft, BorderTop, BorderRight, BorderBottom,
                        (src, dst) => Library.Draw(Index, src, dst, drawColor, 1f));
                    if (GrayScale) DXManager.SetGrayscale(oldGray);
                    return;
                }

                if (FillAmount >= 1f)
                {
                    if (Blending)
                        Library.DrawBlend(Index, drawLoc, drawSize, drawColor, BlendingRate);
                    else
                        Library.Draw(Index, drawLoc, drawSize, drawColor);
                }
                else
                {
                    // fillAmount（裁剪，不拉伸）：按 FillOrigin 仅绘制图像指定比例部分。
                    Size imgSize = Library.GetTrueSize(Index);
                    Rectangle src = new Rectangle(0, 0, imgSize.Width, imgSize.Height);
                    Rectangle dst = new Rectangle(drawLoc.X, drawLoc.Y, drawSize.Width, drawSize.Height);
                    ApplyFillClip(FillAmount, FillOrigin, ref src, ref dst);
                    bool oldBlend = DXManager.Blending;
                    if (Blending) DXManager.SetBlend(true, BlendingRate);
                    Library.Draw(Index, src, new RectangleF(dst.X, dst.Y, dst.Width, dst.Height), drawColor, 1f);
                    if (Blending) DXManager.SetBlend(oldBlend);
                }

                if (GrayScale) DXManager.SetGrayscale(oldGray);
            }
        }

        // 默认占位图：无有效图资源时绘制一个灰色半透明矩形，作为“默认图片”兜底。
        private void DrawDefaultImage()
        {
            var tex = KFramework.MonoGameExtend.KDefaultRes.DefaultTexture2D;
            if (tex == null) return;
            Size drawSize = Size;
            if (drawSize.Width <= 0 || drawSize.Height <= 0) return;
            int px = DisplayLocation.X - (int)(_pivot.X * drawSize.Width);
            int py = DisplayLocation.Y - (int)(_pivot.Y * drawSize.Height);
            var srcRect = new KFramework.MonoGame.Rectangle(0, 0, tex.Width, tex.Height);
            var dstRect = new KFramework.MonoGame.Rectangle(px, py, drawSize.Width, drawSize.Height);
            Color drawColor = new Color(70, 70, 70, 160);
            DXManager.Draw(tex, srcRect, dstRect, drawColor, (Blending ? MirBlend.Additive : MirBlend.Normal).ToBlendState(), FlipEffects);
        }

        // 按 FillAmount / FillOrigin 把整图源/目标矩形裁剪为可见部分（不拉伸图像，仅按比例显隐）。
        private static void ApplyFillClip(float fillAmount, MirFillOrigin origin, ref Rectangle src, ref Rectangle dst)
        {
            float fa = fillAmount < 0f ? 0f : (fillAmount > 1f ? 1f : fillAmount);
            if (fa >= 1f) return;
            int fsw = src.Width, fsh = src.Height, fdw = dst.Width, fdh = dst.Height;
            int vsw = (int)(fsw * fa), vsh = (int)(fsh * fa);
            int vdw = (int)(fdw * fa), vdh = (int)(fdh * fa);
            switch (origin)
            {
                case MirFillOrigin.Left:
                    src.Width = vsw; dst.Width = vdw; break;
                case MirFillOrigin.Right:
                    src.X += fsw - vsw; src.Width = vsw;
                    dst.X += fdw - vdw; dst.Width = vdw; break;
                case MirFillOrigin.Top:
                    src.Height = vsh; dst.Height = vdh; break;
                case MirFillOrigin.Bottom:
                    src.Y += fsh - vsh; src.Height = vsh;
                    dst.Y += fdh - vdh; dst.Height = vdh; break;
            }
        }

        // 九宫格分块：把 (sw,sh) 源图与 (dx,dy,dw,dh) 目标矩形各切成 9 块（四角/四边/中心），
        // 对每块回调 (源矩形, 目标矩形)。边框会按目标尺寸减半钳制，避免目标过小时各块重叠。
        internal static void ForEachNineGridRect(int sw, int sh, int dx, int dy, int dw, int dh,
            int l, int t, int r, int b, Action<Rectangle, RectangleF> draw)
        {
            l = Math.Min(l, dw / 2); r = Math.Min(r, dw / 2);
            t = Math.Min(t, dh / 2); b = Math.Min(b, dh / 2);
            int mw = Math.Max(0, dw - l - r);
            int mh = Math.Max(0, dh - t - b);
            int smw = Math.Max(0, sw - l - r);
            int smh = Math.Max(0, sh - t - b);

            void Emit(int sx, int sy, int sww, int shh, int dxx, int dyy, int dww, int dhh)
            {
                if (sww <= 0 || shh <= 0 || dww <= 0 || dhh <= 0) return;
                draw(new Rectangle(sx, sy, sww, shh), new RectangleF(dxx, dyy, dww, dhh));
            }

            // 上行
            Emit(0, 0, l, t, dx, dy, l, t);
            Emit(l, 0, smw, t, dx + l, dy, mw, t);
            Emit(sw - r, 0, r, t, dx + l + mw, dy, r, t);
            // 中行
            Emit(0, t, l, smh, dx, dy + t, l, mh);
            Emit(l, t, smw, smh, dx + l, dy + t, mw, mh);
            Emit(sw - r, t, r, smh, dx + l + mw, dy + t, r, mh);
            // 下行
            Emit(0, sh - b, l, b, dx, dy + t + mh, l, b);
            Emit(l, sh - b, smw, b, dx + l, dy + t + mh, mw, b);
            Emit(sw - r, sh - b, r, b, dx + l + mw, dy + t + mh, r, b);
        }

        public override bool IsMouseOver(Point p)
        {
            if (!base.IsMouseOver(p)) return false;
            if (!_pixelDetect) return true;
            // 图集(atlas)模式下 Library 为空：无逐像素检测数据，退回包围盒命中。
            if (Library != null)
                return Library.VisiblePixel(Index, p.Subtract(DisplayLocation), true) || Moving;
            return true;
        }



        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            
            if (!disposing) return;

            DrawImageChanged = null;
            _drawImage = false;

            IndexChanged = null;
            _index = 0;

            LibraryChanged = null;
            Library = null;

            PixelDetectChanged = null;
            _pixelDetect = false;

            UseOffSetChanged = null;
            _useOffSet = false;
        }

        
    }
}