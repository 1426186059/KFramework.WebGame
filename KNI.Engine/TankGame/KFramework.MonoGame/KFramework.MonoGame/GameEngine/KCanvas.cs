using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace KFramework.MonoGame
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

            PrintTool.Log("KCanvas Size: ", Size);
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
            Matrix viewMatrix = Matrix.Identity;
            var mSpriteBatch = KSceneMgr.SpriteBatch;
            mSpriteBatch.Begin(
                transformMatrix: viewMatrix,
                sortMode: SpriteSortMode.Deferred,
                samplerState: SamplerState.PointClamp,
                blendState: BlendState.NonPremultiplied);

            DrawWidget(this);

            mSpriteBatch.End();
        }

        public void DrawWidget(KTransform t)
        {
            foreach (var v in t.ChildList)
            {
                if (v is KWidget)
                {
                    (v as KWidget).Draw();
                }

                DrawWidget(v);
            }
        }
    }
}
