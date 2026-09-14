using System;
using System.Collections.Generic;

namespace KFramework.Example3
{
    internal class Player_FireBall : TileBase
    {
        enum EState
        {
            Normal = 1,
            Explode = 2,
        }

        protected Level mLevel;
        private Player mPlayer;
        private const float GravityAcceleration = 1000.0f;
        private const float MaxFallSpeed = 550.0f;
        Vector2 Velocity = new Vector2(50, 0);

        protected SoundEffect hitByPlayerSound;

        private readonly LinkedListNode<Player_FireBall> mEntry;
        Animation FireballSpin;
        Animation FireballExplode;
        AnimationPlayer mAniPlayer = new AnimationPlayer();

        float previousBottom;
        float previousTop;
        
        private EState mState;

        public override Rectangle Collider2DZone
        {
            get
            {
                return new Rectangle((WorldPosition - mAniPlayer.Pivot * mAniPlayer.FrameSize.ToVector2()).ToPoint(), 
                    mAniPlayer.FrameSize);
            }
        }

        public Player_FireBall(Level mLevel, Player mPlayer, Vector2 position)
        {
            mEntry = new LinkedListNode<Player_FireBall>(this);
            this.mLevel = mLevel;
            this.mPlayer = mPlayer;
            this.WorldPosition = position;
            this.BeginPos = position;
            mPlayer.mFireBallList.AddLast(this.mEntry);

            FireballSpin = new Animation(mLevel.mSpriteSheet_misc3Atlas, "misc-3_", 413, 416, 1 / 10f, true, true);
            FireballExplode = new Animation(mLevel.mSpriteSheet_misc3Atlas, "misc-3_", 417, 417, 1 / 10f, false);
            mAniPlayer.mTransform = this;
            mAniPlayer.Pivot = new Vector2(0.5f, 1f);
            mAniPlayer.PlayAnimation(FireballSpin);

            WorldPosition += new Vector2(0, -30);
            if (mPlayer.direction == FaceDirection.Right)
            {
                this.Velocity.X = 500;
            }
            else
            {
                this.Velocity.X = -500;
            }
            mState = EState.Normal;
        }

        public override void Update()
        {
            if (mState == EState.Normal)
            {
                float elapsed = KTime.deltaTime;
                Velocity.Y = MathHelper.Clamp(Velocity.Y + GravityAcceleration * elapsed, -MaxFallSpeed, MaxFallSpeed);

                float spendTime = elapsed;
                float fixedTime1 = elapsed;
                float fixedTime2 = elapsed;
                
                float Coef = 0.5f;
                if (Math.Abs(Velocity.Y * elapsed) >= mAniPlayer.FrameSize.Y * Coef)
                {
                    float step = Math.Abs(Velocity.Y * elapsed) / (mAniPlayer.FrameSize.Y * Coef);
                    fixedTime1 = elapsed / step;
                }
                if (Math.Abs(Velocity.X * elapsed) >= mAniPlayer.FrameSize.X * Coef)
                {
                    float step = Math.Abs(Velocity.X * elapsed) / (mAniPlayer.FrameSize.X * Coef);
                    fixedTime2 = elapsed / step;
                }

                float fixedTime = Math.Min(fixedTime1, fixedTime2);
                while (spendTime > 0)
                {
                    spendTime -= fixedTime;
                    WorldPosition += Velocity * fixedTime;

                    int tileX = (int)Math.Floor(WorldPosition.X / Tile.TileWidth);
                    int tileY = (int)Math.Floor(WorldPosition.Y / Tile.TileHeight);

                    for (int i = tileX - 2; i <= tileX + 2; i++)
                    {
                        for (int j = tileY - 2; j <= tileY + 2; j++)
                        {
                            Tile mTile = mLevel.GetTile(i, j);
                            TileCollision collision = mTile.Collision;
                            if (collision != TileCollision.Passable && mTile.Target != null)
                            {
                                TileBase mTarget = mTile.Target;
                                Vector2 depth = RectangleExtensions.GetIntersectionDepth(Collider2DZone, mTarget.Collider2DZone);
                                if (depth != Vector2.Zero)
                                {
                                    if (depth.Y <= depth.X)
                                    {
                                        WorldPosition = new Vector2(WorldPosition.X, WorldPosition.Y + depth.Y);
                                        Velocity.Y = -Velocity.Y;
                                    }
                                    else
                                    {
                                        if (previousTop <= mTarget.Collider2DZone.Top)
                                        {
                                            WorldPosition = new Vector2(WorldPosition.X + depth.X, WorldPosition.Y);
                                        }
                                        else
                                        {
                                            OnKilled(mTile.Target);
                                            return;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    foreach (var mEnemy in mLevel.mEnemyObjectList)
                    {
                        if (!mEnemy.IsDispose)
                        {
                            Rectangle tileBounds = mEnemy.Collider2DZone;
                            Vector2 depth = RectangleExtensions.GetIntersectionDepth(Collider2DZone, tileBounds);
                            if (depth != Vector2.Zero)
                            {
                                mEnemy.OnKilled(mPlayer);
                                  OnKilled(mEnemy);
                                return;
                            }
                        }
                    }

                    previousBottom = Collider2DZone.Bottom;
                    previousTop = Collider2DZone.Top;

                    if (WorldPosition.Y >= Tile.TileFloorY)
                    {
                        OnKilled(null);
                    }
                }
            }
        }

        public override void Draw()
        {
            base.Draw();
            mAniPlayer.Draw(KSceneMgr.SpriteBatch, SpriteEffects.None);
            DrawCollider2DZone();
        }

        private void OnKilled(TileBase killedBy)
        {
            if (mState == EState.Normal)
            {
                mState = EState.Explode;
                mAniPlayer.PlayAnimation(FireballExplode);
                KTween.delayedCall(this, 0.5f, () =>
                {
                    Dispose();
                });
            }
        }

    }
}