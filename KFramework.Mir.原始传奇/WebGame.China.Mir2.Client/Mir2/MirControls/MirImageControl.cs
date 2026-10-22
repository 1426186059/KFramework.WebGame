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

        // —— 方案2：图集绘制分支（与 .Lib 并存）——
        // AtlasSheet 为已加载的图集（Mir2AtlasLibrary.GetSheetAsync 返回），AtlasFrame 为帧名；
        // 二者齐备时 DrawControl 优先走图集精灵绘制，否则回退老的 Library.Draw(Index)。
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
        public string AtlasFrame
        {
            get { return _atlasFrame; }
            set
            {
                if (_atlasFrame == value) return;
                _atlasFrame = value;
                OnLibraryChanged();
            }
        }

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

                // 绘制目标矩形：以 DisplayLocation 为锚点、按「实际图片像素尺寸 TrueSize」绘制，
                // 与下方 Library 分支 Library.Draw(Index, DisplayLocation, ...) 在 DisplayLocation 以自然尺寸绘制的行为一致。
                //   • Size     = 控件布局/碰撞尺寸：DisplayRectangle、IsMouseOver 命中判定、LayerControl 烘焙 RT 尺寸都基于它。
                //   • TrueSize = 图片/精灵实际像素尺寸（atlas 帧 SourceRectangle 或 Library.GetTrueSize），即“实际要画多大”。
                // 不要用 Size 当绘制尺寸：那会把图片拉伸/压缩进布局框，既与 Library 分支不一致，也会把图扭曲。
                KFramework.MonoGame.Rectangle targetRect = new KFramework.MonoGame.Rectangle(DisplayLocation.X, DisplayLocation.Y, TrueSize.Width, TrueSize.Height);
                // 走 DXManager.Draw 安全入口（BeginSprite 会按当前上下文自动 Begin）：
                // 主渲染 / LayerControl 烘焙通用；直接调 SpriteBatch.DrawFrame 在烘焙时主批未 Begin 会抛异常。
                DXManager.Draw(atlasSprite.Texture, atlasSprite.SourceRectangle, targetRect, drawColor);
                
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

                if (Blending)
                    Library.DrawBlend(Index, DisplayLocation, ForeColour, false, BlendingRate);
                else
                    Library.Draw(Index, DisplayLocation, ForeColour, false, Opacity);

                if (GrayScale) DXManager.SetGrayscale(oldGray);
            }
        }

        public override bool IsMouseOver(Point p)
        {
            return base.IsMouseOver(p) && (!_pixelDetect || Library.VisiblePixel(Index, p.Subtract(DisplayLocation),true) || Moving);
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