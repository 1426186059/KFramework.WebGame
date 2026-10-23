using System;
using System.Threading;
using System.Threading.Tasks;
using Client.MirGraphics;

namespace Client.MirControls
{
    public class MirImageControl : MirControl
    {
        public override Point DisplayLocation { get { return UseOffSet ? base.DisplayLocation.Add(Library.GetOffSet(Index)) : base.DisplayLocation; } }
        public Point DisplayLocationWithoutOffSet { get { return base.DisplayLocation; } }

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

        public override Size Size
        {
            set { base.Size = value; }
            get
            {
                if (AutoSize && AtlasSheet != null && !string.IsNullOrEmpty(AtlasFrame)
                    && AtlasSheet.Frames.TryGetValue(AtlasFrame, out var s))
                    return new Size(s.SourceRectangle.Width, s.SourceRectangle.Height);
                if (AutoSize && Library != null && Index >= 0)
                    return Library.GetTrueSize(Index);
                return base.Size;
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

        protected internal override void DrawControl()
        {
            base.DrawControl();

            if (!DrawImage) return;

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

                // 目标矩形按 Pivot（归一化锚点，默认左上）对齐：图像以自身 Pivot 点落在 DisplayLocation；WorldScale 沿父链累乘缩放绘制尺寸（默认 1，行为不变）。
                var ws = WorldScale;
                Size drawSize = new Size((int)(Size.Width * ws.X), (int)(Size.Height * ws.Y));
                int px = DisplayLocation.X - (int)(_pivot.X * drawSize.Width);
                int py = DisplayLocation.Y - (int)(_pivot.Y * drawSize.Height);
                KFramework.MonoGame.Rectangle targetRect = new KFramework.MonoGame.Rectangle(px, py, drawSize.Width, drawSize.Height);
                // 混合状态对齐原版 Crystal：用【控件自身的 Blending 属性】决定，而非全局 DXManager.Blending。
                // 否则会被其它特效遗留的全局加法混合污染——暗色 UI 在亮背景上被叠加成“过曝发白”（即“混合过头”）。
                //   • Blending=false（正常 UI）→ NonPremultiplied（直通 alpha，等价原版 SpriteFlags.AlphaBlend）
                //   • Blending=true        → Additive（发光/叠加特效，等价原版 SetBlend(true)）
                var imgBlend = Blending
                    ? KFramework.MonoGame.BlendState.Additive
                    : KFramework.MonoGame.BlendState.NonPremultiplied;
                // 走 DXManager.Draw 安全入口（BeginSprite 会按当前上下文自动 Begin）：
                // 主渲染 / LayerControl 烘焙通用；直接调 SpriteBatch.DrawFrame 在烘焙时主批未 Begin 会抛异常。
                DXManager.Draw(atlasSprite.Texture, atlasSprite.SourceRectangle, targetRect, drawColor, imgBlend);
                
                if (GrayScale)
                {
                    DXManager.SetGrayscale(oldGray);
                }
                return;
            }

            if (Library != null)
            {
                bool oldGray = DXManager.GrayScale;

                if (GrayScale)
                {
                    DXManager.SetGrayscale(true);
                }

                // 同图集分支：以 Pivot 决定图像哪个点落在 DisplayLocation；WorldScale 缩放绘制尺寸（默认 1，行为不变）。
                var ws = WorldScale;
                Size drawSize = new Size((int)(Size.Width * ws.X), (int)(Size.Height * ws.Y));
                Point drawLoc = new Point(
                    DisplayLocation.X - (int)(_pivot.X * drawSize.Width),
                    DisplayLocation.Y - (int)(_pivot.Y * drawSize.Height));

                // 预乘透明度到颜色，再交给按 Size 缩放的重载（该重载走 DXManager.Draw，自身不处理 Opacity）。
                Color drawColor = ForeColour;
                if (Opacity < 1f)
                    drawColor = new Color(drawColor.R, drawColor.G, drawColor.B, (byte)(drawColor.A * Opacity));

                if (Blending)
                    Library.DrawBlend(Index, drawLoc, drawSize, drawColor, BlendingRate);
                else
                    Library.Draw(Index, drawLoc, drawSize, drawColor);

                if (GrayScale) DXManager.SetGrayscale(oldGray);
            }
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

        private bool _uiHostChecked;
        private static readonly HashSet<MirControl> _uiHostReported = new HashSet<MirControl>();

        public override void Draw()
        {
            AssertUILayerHosted(this);
            base.Draw();
        }

        private static void AssertUILayerHosted(MirControl self)
        {
            if (self == null) return;

            // 沿"实际收录容器"向上走：Parent 可能仍指向场景而实体已被 MirScene.AddControl
            // 路由进某个层容器，所以要按 Controls 的收录关系找真正的上一级。
            MirControl ContainerOf(MirControl node)
            {
                if (node == null || node.Parent == null) return null;
                if (node.Parent.Controls.Contains(node)) return node.Parent;

                for (int i = 0; i < node.Parent.Controls.Count; i++)
                {
                    MirControl c = node.Parent.Controls[i];
                    if (c != null && c.Controls.Contains(node)) return c;
                }
                return null;
            }

            string Describe(MirControl c)
            {
                MirImageControl img = c as MirImageControl;
                string lib = (img != null && img.Library != null) ? " lib=" + img.Library.FileName + " idx=" + img.Index : "";
                return string.Format("{0}[Loc=({1},{2}) Size={3}x{4} Disp=({5},{6}){7}]",
                    c.GetType().Name, c.Location.X, c.Location.Y, c.Size.Width, c.Size.Height,
                    c.DisplayLocation.X, c.DisplayLocation.Y, lib);
            }

            // 每个控件只体检一次（字段在实例上，避免每帧走链）。
            MirImageControl selfImage = self as MirImageControl;
            if (selfImage != null)
            {
                if (selfImage._uiHostChecked) return;
                selfImage._uiHostChecked = true;
            }

            // 第一遍只判 ok，不分配、不拼串。
            MirControl cur = self, below = null;
            for (int i = 0; i < 64; i++)
            {
                MirControl next = ContainerOf(cur);
                if (next == null) break;
                below = cur;
                cur = next;
            }
            MirControl root = cur, layer = below;

            if (root is MirScene && layer is UILayerControl) return;

            // 第二遍才拼详细日志：同一控件只报一次，总数超过 200 条后收口。
            if (!_uiHostReported.Add(self)) return;
            if (_uiHostReported.Count > 200)
            {
                if (_uiHostReported.Count == 201)
                    KFramework.MonoGame.PrintTool.Log("[Mir][UIHost] 已达日志上限(200)，后续不再打印。");
                return;
            }

            string reason = !(root is MirScene)
                ? "根节点不是 MirScene（" + root.GetType().FullName + "）"
                : (layer == null ? "直接挂在场景根上，未进入任何层"
                                 : "所在层不是 UILayerControl（" + layer.GetType().FullName + "）");

            var chain = new List<MirControl>();
            for (MirControl node = self; node != null && chain.Count < 64; node = ContainerOf(node))
                chain.Add(node);
            string chainText = "";
            for (int i = 0; i < chain.Count; i++)
                chainText += (i > 0 ? " <- " : "") + Describe(chain[i]);

            KFramework.MonoGame.PrintTool.Log(string.Format("[Mir][UIHost] {0} | 链: {1} | 活动场景: {2}",
                reason, chainText, MirScene.ActiveScene == null ? "null" : MirScene.ActiveScene.GetType().Name));
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