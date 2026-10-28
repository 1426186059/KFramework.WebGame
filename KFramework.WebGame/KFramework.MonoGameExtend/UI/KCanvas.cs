using KFramework.MonoGame;
using System;

namespace KFramework.MonoGameExtend
{
    public class KCanvas : KWidget, KDrawable
    {
        public enum ScaleMode
        {
            /// <summary>
            /// Using the Constant Pixel Size mode, positions and sizes of UI elements are specified in pixels on the screen.
            /// </summary>
            ConstantPixelSize,
            /// <summary>
            /// Using the Scale With Screen Size mode, positions and sizes can be specified according to the pixels of a specified reference resolution.
            /// If the current screen resolution is larger than the reference resolution, the Canvas will keep having only the resolution of the reference resolution, but will scale up in order to fit the screen. If the current screen resolution is smaller than the reference resolution, the Canvas will similarly be scaled down to fit.
            /// </summary>
            ScaleWithScreenSize,
            /// <summary>
            /// Using the Constant Physical Size mode, positions and sizes of UI elements are specified in physical units, such as millimeters, points, or picas.
            /// </summary>
            ConstantPhysicalSize
        }

        public enum ScreenMatchMode
        {
            /// <summary>
            /// Scale the canvas area with the width as reference, the height as reference, or something in between.
            /// </summary>
            MatchWidthOrHeight = 0,
            /// <summary>
            /// Expand the canvas area either horizontally or vertically, so the size of the canvas will never be smaller than the reference.
            /// </summary>
            Expand = 1,
            /// <summary>
            /// Crop the canvas area either horizontally or vertically, so the size of the canvas will never be larger than the reference.
            /// </summary>
            Shrink = 2
        }

        private const float kLogBase = 2;
        //离相机的距离
        public float Distance;
        public float targetFrame;
        protected Vector2 m_ReferenceResolution = new Vector2(GameConst.DesignWidth, GameConst.DesignHeight);

        public event EventHandler<EventArgs> DrawOrderChanged;
        public event EventHandler<EventArgs> VisibleChanged;

        public ScaleMode uiScaleMode { get; set; } = ScaleMode.ScaleWithScreenSize;
        public ScreenMatchMode screenMatchMode { get; set; } =  ScreenMatchMode.MatchWidthOrHeight;
        public float referencePixelsPerUnit { get; set; } = 100;
        public float matchWidthOrHeight { get; set; } = 0;
        private float scaleFactor { get; set; } = 1.0f;

        // 自动合批：普通控件沿用当前批材质；显式 BatchMaterial 的节点（如 KScrollView 的 scissor 材质）会切到独立批，画完子树再恢复。
        private Material _defaultMaterial;
        private Material _currentMat;
        private bool _began;
        private bool _scissorActive;

        public int DrawOrder => throw new NotImplementedException();

        public bool Visible => throw new NotImplementedException();

        public KCanvas()
        {
            DoChange();
            KSceneMgr.ScreenSizeChanged += OnWindowSizeChanged;
        }

        private void DoChange()
        {
            HandleScaleWithScreenSize();
            Viewport Screent = KSceneMgr.Game.GraphicsDevice.Viewport;

            float tagetWidth = GameConst.DesignHeight * (float)Screent.Width / Screent.Height;
            float tagetHeight = GameConst.DesignWidth / (float)Screent.Width * Screent.Height;
            float X = MathHelper.Lerp(GameConst.DesignWidth, tagetWidth, matchWidthOrHeight);
            float Y = MathHelper.Lerp(GameConst.DesignHeight, tagetHeight, 1 - matchWidthOrHeight);
            Size = new Vector2(X, Y);
            WorldPosition = Vector2.Zero;
            LocalScale = new Vector2(scaleFactor, scaleFactor);
        }

        public void OnWindowSizeChanged(object sender, EventArgs e)
        {
            PrintTool.Log("OnWindowSizeChanged");
            DoChange();
            Viewport Screent = KSceneMgr.Game.GraphicsDevice.Viewport;
            PrintTool.Log("KCanvas Size: ", Size);
            PrintTool.Log("Viewport Size: ", new Vector2(Screent.Width, Screent.Height));
            PrintTool.Log("KCanvas LocalPosition: " + LocalPosition);
            PrintTool.Log("KCanvas WorldPosition: " + WorldPosition);
            PrintTool.Log("KCanvas ScreenPosition: " + ScreenPosition);
            PrintTool.Log("KCanvas AnchorPosition: " + AnchorPosition);
        }

        protected virtual void HandleScaleWithScreenSize()
        {
            Viewport Screent = KSceneMgr.Game.GraphicsDevice.Viewport;
            Vector2 screenSize = new Vector2(Screent.Width, Screent.Height);

            float scaleFactor = 0;
            switch (screenMatchMode)
            {
                case ScreenMatchMode.MatchWidthOrHeight:
                    {
                        // 在计算平均值之前，我们取相对宽度和高度值的对数。
                        // 然后我们将其转换回原始空间。
                        // 在对数空间中进行转换的原因是为了获得更好的表现。
                        // 如果一个轴的分辨率是另一个轴的两倍，而另一个轴的分辨率是前者的一半，那么当widthOrHeight的值为0.5时，两者应达到平衡。
                        // 在正常空间中，平均值应为（0.5 + 2） / 2 = 1.25
                        // 在对数空间中，平均值为 (-1 + 1) / 2 = 0
                        float logWidth = (float)Math.Log(screenSize.X / m_ReferenceResolution.X, kLogBase);
                        float logHeight = (float)Math.Log(screenSize.Y / m_ReferenceResolution.Y, kLogBase);
                        float logWeightedAverage = MathHelper.Lerp(logWidth, logHeight, matchWidthOrHeight);
                        scaleFactor = (float)Math.Pow(kLogBase, logWeightedAverage);
                        break;
                    }
                case ScreenMatchMode.Expand:
                    {
                        scaleFactor = Math.Min(screenSize.X / m_ReferenceResolution.X, screenSize.Y / m_ReferenceResolution.Y);
                        break;
                    }
                case ScreenMatchMode.Shrink:
                    {
                        scaleFactor = Math.Max(screenSize.X / m_ReferenceResolution.X, screenSize.Y / m_ReferenceResolution.Y);
                        break;
                    }
            }

            this.scaleFactor = scaleFactor;
        }

        public override void Draw()
        {
            var batch = KSceneMgr.SpriteBatch;
            if (_defaultMaterial == null)
            {
                _defaultMaterial = new Material
                {
                    Blend = BlendState.NonPremultiplied,
                    Sampler = SamplerState.PointClamp,
                    DepthStencil = DepthStencilState.None,
                    Rasterizer = RasterizerState.CullNone,
                };
            }
            batch.Begin(_defaultMaterial, SpriteSortMode.Deferred, Matrix4x4.Identity);
            _currentMat = _defaultMaterial;
            _began = true;
            _scissorActive = false;

            DrawWidget(this);

            batch.AutoEndBatch(); // 提交最后一段（未 Begin 时安全 no-op）
            _began = false;
            _currentMat = null;
        }

        public void DrawWidget(KTransform t)
        {
            var mEntry = t.ChildList.First;
            while (mEntry != null)
            {
                var next = mEntry.Next;
                if (mEntry.Value.IsDispose)
                {
                    if (mEntry.List != null)
                    {
                        t.ChildList.Remove(mEntry);
                    }
                }
                else if (mEntry.Value.activeInHierarchy)
                {
                    if (mEntry.Value is KWidget w)
                    {
                        Material prevMat = _currentMat;
                        // 显式材质 != 当前批 → 切换到一个独立 SpriteBatch 段；null 则继承当前批（沿用父的材质/scissor）
                        if (w.BatchMaterial != null && w.BatchMaterial != _currentMat)
                        {
                            SwitchBatch(w.BatchMaterial);
                        }
                        w.Draw();
                        DrawWidget(mEntry.Value); // 子树继承当前批，scissor 材质对 KScrollView 内容同样生效
                        if (w.BatchMaterial != null && w.BatchMaterial != prevMat)
                        {
                            SwitchBatch(prevMat); // 恢复进入本节点前的批
                        }
                    }
                    else
                    {
                        DrawWidget(mEntry.Value);
                    }
                }
                mEntry = next;
            }
        }

        /// <summary>结束当前 SpriteBatch 段并以新材质开启下一段；若离开 scissor 批则顺手清掉设备级裁剪矩形。</summary>
        private void SwitchBatch(Material mat)
        {
            var batch = KSceneMgr.SpriteBatch;
            batch.AutoEndBatch(); // 提交当前段（切批：紧接 Begin 新材质即可）
            if (_scissorActive) { Mask.ClearScissor(KSceneMgr.Game.GraphicsDevice); _scissorActive = false; }
            batch.Begin(mat, SpriteSortMode.Deferred, Matrix4x4.Identity);
            _currentMat = mat;
            _began = true;
            _scissorActive = mat?.Rasterizer?.ScissorTestEnable == true;
        }

        public override void Dispose()
        {
            base.Dispose();
            KSceneMgr.ScreenSizeChanged -= OnWindowSizeChanged;
        }
    }
}
