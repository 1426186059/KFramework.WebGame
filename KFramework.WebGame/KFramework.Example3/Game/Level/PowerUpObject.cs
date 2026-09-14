using System;
using System.Collections.Generic;

namespace KFramework.Example3
{
    internal class PowerUpObject : TileBase
    {
        public enum EMoveState
        {
            Appear = 1,
            Move = 2,
        }

        protected Level mLevel;
        private FaceDirection direction = FaceDirection.Right;
        protected EMoveState nMoveState;
        
        private const float GravityAcceleration = 1000.0f;
        private const float MaxFallSpeed = 550.0f;
        Vector2 Velocity = new Vector2(50, 0);
        Vector2 Velocity_Appear = new Vector2(0, -60);
        protected readonly LinkedListNode<PowerUpObject> mEntry = null;

        public PowerUpObject(Level level, Vector2 position, float MoveSpeed)
        {
            mEntry = new LinkedListNode<PowerUpObject>(this);
            this.mLevel = level;
            this.WorldPosition = position;
            this.BeginPos = position;
            this.Velocity.X = Math.Abs(MoveSpeed);
            this.direction = FaceDirection.Right;
            this.nMoveState = EMoveState.Appear;
            mLevel.mPowerUpObjectList.AddLast(this.mEntry);
        }

        public bool orCanEat()
        {
            return this.nMoveState == EMoveState.Move;
        }
        
        public override void Update()
        {
            float elapsed = KTime.deltaTime;
            if (nMoveState == EMoveState.Move)
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
            }
            else if (nMoveState == EMoveState.Appear)
            {
                WorldPosition += Velocity_Appear * elapsed;
                if (WorldPosition.Y <= BeginPos.Y)
                {
                    WorldPosition = BeginPos;
                    nMoveState = EMoveState.Move;
                }
            }

        }

    }
}