using System;

namespace KFramework.Example3
{
    internal struct AnimationPlayer
    {
        Animation Animation;
        int nFrameIndex;
        private float time;

        public KTransform mTransform;

        //左下角为 原点

        public Vector2 Pivot { get; set; }

        public Vector2 Origin
        {
            get
            {
                KSprite mSprite = Animation.GetSprite(nFrameIndex);
                return mSprite.Rectangle.Size.ToVector2() * Pivot;
            }
        }

        public Rectangle BoundingRectangle
        {
            get
            {
                Point Size = FrameSize;
                Vector2 mPos = mTransform.WorldPosition - new Vector2(Size.X, Size.Y) * Pivot;
                return new Rectangle(mPos.ToPoint(), Size);
            }
        }

        public void PlayAnimation(Animation animation)
        {
            if (Animation == animation)
            {
                return;
            }

            this.Animation = animation;
            this.nFrameIndex = 0;
            this.time = 0.0f;
        }

        public void Update()
        {
            if (Animation.FrameCount <= 1) return;

            time += KTime.deltaTime;
            while (time > Animation.FrameTime)
            {
                time -= Animation.FrameTime;
                if (Animation.IsLooping)
                {
                    nFrameIndex++;
                    if (nFrameIndex > Animation.FrameCount - 1)
                    {
                        nFrameIndex = 0;
                    }
                }
                else
                {
                    if (nFrameIndex >= Animation.FrameCount - 1)
                    {
                        break;
                    }
                    else
                    {
                        nFrameIndex++;
                        if (nFrameIndex > Animation.FrameCount - 1)
                        {
                            nFrameIndex = Animation.FrameCount - 1;
                        }
                    }

                }
            }

        }
        
        public void Draw(SpriteBatch spriteBatch, SpriteEffects spriteEffects)
        {
            Draw(spriteBatch, spriteEffects, Color.White);
        }

        public void Draw(SpriteBatch spriteBatch, SpriteEffects spriteEffects, Color color)
        {
            KSprite mSprite = Animation.GetSprite(nFrameIndex);
            Rectangle targetRegion = new Rectangle(mTransform.WorldPosition.ToPoint(), FrameSize);

            spriteBatch.Draw(
                mSprite.Texture,
                targetRegion,
                mSprite.Rectangle,
                color,
                0.0f,
                Origin,
                spriteEffects,
                0.0f);
        }


        public int FrameWidth
        {
            get { return FrameSize.X; }
        }

        public int FrameHeight
        {
            get { return FrameSize.Y; }
        }

        public Point FrameSize
        {
            get
            {
                return (Animation.GetSprite(nFrameIndex).Rectangle.Size.ToVector2() * Tile.TileScale * mTransform.WorldScale).ToPoint();
            }
        }

    }
}