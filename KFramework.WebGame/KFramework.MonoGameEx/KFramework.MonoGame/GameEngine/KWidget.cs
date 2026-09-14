using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace KFramework.MonoGame
{
    public class KWidget : KTransform
    {
        public KCamera Camera { get; set; } = KCamera.Main;

        private Vector2 _cacheSize;
        private Vector2 _cachePovit;
        private KRectangleF _cacheMinMaxAnchor; //最小最大 锚点
        private KRectangleFOffset _cacheMinMaxAnchorOffset; //最小最大 锚点

        public KWidget()
        {
            Size = new Vector2(100, 100);
        }

        public Vector2 ScreenPosition
        {
            get
            {
                return Camera.WorldToScreen(WorldPosition);
            }
        }

        public Vector2 Size
        {
            get
            {
                return _cacheSize;
            }
            set
            {
                if (_cacheSize != value)
                {
                    _cacheSize = value;
                    UpdateAnchorOffset();
                    OnParentWidgetSizeChanged();
                }
            }
        }

        public Vector2 Anchor
        {
            set
            {
                MinMaxAnchor = KRectangleF.MinMax(value, value);
            }
        }

        public KRectangleF MinMaxAnchor
        {
            get { return _cacheMinMaxAnchor; }
            set
            {
                if (_cacheMinMaxAnchor != value)
                {
                    PrintTool.Assert(value.X >= 0 && value.X <= 1.0f);
                    PrintTool.Assert(value.Y >= 0 && value.Y <= 1.0f);
                    _cacheMinMaxAnchor = value;
                }
            }
        }

        public KRectangleFOffset AnchorOffset
        {
            get
            {
                return _cacheMinMaxAnchorOffset;
            }
            set
            {
                if (_cacheMinMaxAnchorOffset != value)
                {
                    _cacheMinMaxAnchorOffset = value;
                    this.UpdateRealRectangle();
                    this.OnParentWidgetSizeChanged();
                }
            }
        }

        public Vector2 AnchorPosition
        {
            get
            {
                KRectangleF mAnchorRectangle = GetAnchorRectangle();
                return LocalPosition - mAnchorRectangle.Location;
            }
            set
            {
                KRectangleF mAnchorRectangle = GetAnchorRectangle();
                LocalPosition = value + mAnchorRectangle.Location;
                UpdateAnchorOffset();
            }
        }
        
        public Vector2 Pivot
        {
            get
            {
                return _cachePovit;
            }
            set
            {
                if (value != _cachePovit)
                {
                    PrintTool.Assert(value.X >= 0 && value.X <= 1.0f);
                    PrintTool.Assert(value.Y >= 0 && value.Y <= 1.0f);

                    Vector2 lastPovit = _cachePovit;
                    _cachePovit = value;
                    LocalPosition += (_cachePovit - lastPovit) * Size;
                }
            }
        }

        private Vector2 GetParentWidgetSize()
        {
            if (GetParent<KWidget>() != null)
            {
                return GetParent<KWidget>().Size;
            }
            else
            {
                Viewport Screent = KSceneMgr.Game.GraphicsDevice.Viewport;
                return new Vector2(Screent.Width, Screent.Height);
            }
        }

        private Vector2 GetParentWidgetPivot()
        {
            if (GetParent<KWidget>() != null)
            {
                return GetParent<KWidget>().Pivot;
            }
            else
            {
                return Vector2.Zero;
            }
        }

        private KRectangleF GetAnchorRectangle()
        {
            var mParentWidgetSize = GetParentWidgetSize();
            var mParentWidgetPivot = GetParentWidgetPivot();
            return new KRectangleF(
                (MinMaxAnchor.Location - mParentWidgetPivot) * mParentWidgetSize,
                MinMaxAnchor.Size * mParentWidgetSize
                );
        }

        private KRectangleF GetRealRectangle()
        {
            return new KRectangleF(LocalPosition - Pivot * Size, Size);
        }

        protected override void OnLocalPositionChanged()
        {
            UpdateAnchorOffset();
        }

        protected override void OnParentChanged()
        {
            UpdateRealRectangle();
            OnParentWidgetSizeChanged();
        }

        private void OnParentWidgetSizeChanged()
        {
            foreach (var v in ChildList)
            {
                if (v is KWidget)
                {
                    (v as KWidget).UpdateRealRectangle();
                }
            }
        }
        
        private void UpdateRealRectangle()
        {
            KRectangleF mRealRectangle = new KRectangleF();
            KRectangleF mAnchorRectangle = GetAnchorRectangle();
            mRealRectangle.Left = mAnchorRectangle.Left + AnchorOffset.Left;
            mRealRectangle.Right = mAnchorRectangle.Right + AnchorOffset.Right;
            mRealRectangle.Top = mAnchorRectangle.Top + AnchorOffset.Top;
            mRealRectangle.Bottom = mAnchorRectangle.Bottom + AnchorOffset.Bottom;
            LocalPosition = mRealRectangle.Location + mRealRectangle.Size * Pivot;
            Size = mRealRectangle.Size;
        }

        private void UpdateAnchorOffset()
        {
            KRectangleF mRealRectangle = GetRealRectangle();
            KRectangleF mAnchorRectangle = GetAnchorRectangle();
            _cacheMinMaxAnchorOffset.Left = mRealRectangle.Left - mAnchorRectangle.Left;
            _cacheMinMaxAnchorOffset.Right = mRealRectangle.Right - mAnchorRectangle.Right;
            _cacheMinMaxAnchorOffset.Top = mRealRectangle.Top - mAnchorRectangle.Top;
            _cacheMinMaxAnchorOffset.Bottom = mRealRectangle.Bottom - mAnchorRectangle.Bottom;
        }

        public Vector2 LocalCenterPos
        {
            get { return (GetParentWidgetSize() - Size) / 2; }
        }

        public Vector2 LocalLeftPos
        {
            get { return new Vector2(0, (GetParentWidgetSize().Y - Size.Y) / 2); }
        }

        public Vector2 LocalTopPos
        {
            get { return new Vector2((GetParentWidgetSize().X - Size.X) / 2, 0); }
        }

        public Vector2 LocalRightPos
        {
            get { return new Vector2(GetParentWidgetSize().X - Size.X, (GetParentWidgetSize().Y - Size.Y) / 2); }
        }

        public Vector2 LocalBottomPos
        {
            get { return new Vector2((GetParentWidgetSize().X - Size.X) / 2, GetParentWidgetSize().Y - Size.Y); }
        }

        public Vector2 LocalTopLeftPos
        {
            get { return new Vector2(0, 0); }
        }

        public Vector2 LocalTopRightPos
        {
            get { return new Vector2(GetParentWidgetSize().X - Size.X, 0); }
        }

        public Vector2 LocalBottomRightPos
        {
            get { return GetParentWidgetSize() - Size; }
        }

        public Vector2 LocalBottomLeftPos
        {
            get { return new Vector2(0, GetParentWidgetSize().Y - Size.Y); }
        }


        private const bool bDrawWidgetZone = true;

        public KRectangleF GetWorldRectRectangle(KRectangleF LocalRectangle)
        {
            var Location = Parent.LocalToWorld(LocalRectangle.Location);
            var Size = LocalRectangle.Size * WorldScale;
            return new KRectangleF(Location, Size);
        }

        public void DrawWidgetZone(GameTime gameTime)
        {
            if (bDrawWidgetZone)
            {
                var batch = KSceneMgr.SpriteBatch;

                int nDrawType = 2;
                if (nDrawType == 1)
                {
                    Vector2 worldPos = WorldPosition;
                    Vector2 origin = Vector2.Zero;

                    Rectangle target = new Rectangle(
                        (WorldPosition - Size * Pivot * WorldScale).ToPoint(),
                        (Size * WorldScale).ToPoint());

                    Color mColor = new Color(255, 255, 255, 200);
                    batch.Draw(
                        KDefaultRes.DefaultSprite.Texture,
                        target,
                        KDefaultRes.DefaultSprite.Rectangle,
                        mColor,
                        WorldRotation,
                        origin,
                        SpriteEffects.None,
                        0);
                }
                else if (nDrawType == 2)
                {
                    Vector2 worldPos = WorldPosition;
                    Vector2 origin = Vector2.Zero;

                    KRectangleF target = GetWorldRectRectangle(GetAnchorRectangle());

                    Color mColor = new Color(0, 0, 0, 200);
                    batch.Draw(
                        KDefaultRes.DefaultSprite.Texture,
                        target,
                        KDefaultRes.DefaultSprite.Rectangle,
                        mColor,
                        WorldRotation,
                        origin,
                        SpriteEffects.None,
                        0);
                }

            }
        }
    }
}
