using System;
using System.Collections.Generic;

namespace KFramework.Example3
{
    public enum EEnemyType
    {
        Goomba, //板栗仔
        Koopas, //乌龟
    }

    public enum EObjectState
    {
        Appearing = 1, //在播放 出生动画
        Ok = 2,
        Deading = 3, //在播放死亡动画
        Dead = 4, //完全死亡
    }

    internal class EnemyBase : TileBase
    {
        public enum EMoveState
        {
            Appear = 1,
            Move = 2,
        }

        protected Level mLevel;
        protected FaceDirection direction = FaceDirection.Right;
        protected EMoveState nMoveState;
        protected EObjectState mObjectState;

        private const float GravityAcceleration = 1000.0f;
        private const float MaxFallSpeed = 550.0f;
        Vector2 Velocity = new Vector2(50, 0);
        protected readonly LinkedListNode<EnemyBase> mEntry = null;

        protected SoundEffect hitByPlayerSound;

        public EnemyBase(Level level, Vector2 position, float MoveSpeed)
        {
            mEntry = new LinkedListNode<EnemyBase>(this);
            this.mLevel = level;
            this.WorldPosition = position;
            this.BeginPos = position;
            SetMoveSpeed(MoveSpeed);
            this.nMoveState = EMoveState.Appear;
            this.mObjectState = EObjectState.Appearing;
            mLevel.mEnemyObjectList.AddLast(this.mEntry);

            hitByPlayerSound = mLevel.mContentInstace.LoadSound("MyRes/Sounds/smb_stomp");
        }

        public EObjectState GetState()
        {
            return mObjectState;
        }

        public void SetMoveSpeed(float MoveSpeed, FaceDirection direction = FaceDirection.Left)
        {
            this.direction = direction;
            if (direction == FaceDirection.Left)
            {
                this.Velocity.X = -Math.Abs(MoveSpeed);
            }
            else
            {
                this.Velocity.X = Math.Abs(MoveSpeed);
            }
        }

        public override void Update()
        {
            float elapsed = KTime.deltaTime;

            if (mObjectState == EObjectState.Ok)
            {

                Velocity.Y = MathHelper.Clamp(Velocity.Y + GravityAcceleration * elapsed, -MaxFallSpeed, MaxFallSpeed);

                float posX = WorldPosition.X;
                int tileX = (int)Math.Floor(posX / Tile.TileWidth);
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
                            Rectangle tileBounds = mTarget.Collider2DZone;
                            Vector2 depth = RectangleExtensions.GetIntersectionDepth(Collider2DZone, tileBounds);
                            if (depth.LengthSquared() > 0)
                            {
                                if (Math.Abs(depth.Y) <= Math.Abs(depth.X))
                                {
                                    WorldPosition = new Vector2(WorldPosition.X, WorldPosition.Y + depth.Y);
                                    Velocity.Y = 0;
                                }
                                else
                                {
                                    WorldPosition = new Vector2(WorldPosition.X + depth.X, WorldPosition.Y);
                                    if (depth.X > 0)
                                    {
                                        direction = FaceDirection.Right;
                                    }
                                    else
                                    {
                                        direction = FaceDirection.Left;
                                    }

                                    Velocity.X = (int)direction * Math.Abs(Velocity.X);
                                }
                            }
                        }
                    }
                }

                WorldPosition += Velocity * elapsed;
                if (WorldPosition.Y >= Tile.TileFloorY)
                {
                    OnKilled(null);
                }
            }
            else if (mObjectState == EObjectState.Appearing)
            {
                mObjectState = EObjectState.Ok;
            }
        }

        public virtual void OnKilled(Player killedBy)
        {
            
        }

    }
}