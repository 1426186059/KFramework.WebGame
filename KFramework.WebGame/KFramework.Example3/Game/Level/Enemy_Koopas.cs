
namespace KFramework.Example3
{
    //乌龟
    internal class Enemy_Koopas : EnemyBase
    {
        public override Rectangle Collider2DZone
        {
            get
            {
                return mAniPlayer.BoundingRectangle;
            }
        }

        private Animation WalkAnimation;
        private Animation DieAnimation;
        private AnimationPlayer mAniPlayer;

        public Enemy_Koopas(Level level, Vector2 position):base(level, position, 100)
        {
            LoadContent();
        }

        public void LoadContent()
        {
            WalkAnimation = new Animation(mLevel.mSpriteSheet_charactersAtlas, "characters_", 169, 170, 1 / 10f, true, true);
            DieAnimation = new Animation(mLevel.mSpriteSheet_charactersAtlas, "characters_", 167, 167, 1 / 10f, false);
            mAniPlayer.mTransform = this;
            mAniPlayer.Pivot = new Vector2(0.5f, 1f);
            mAniPlayer.PlayAnimation(WalkAnimation);
        }

        public override void Update()
        {
            base.Update();
            mAniPlayer.Update();
        }

        public override void Draw()
        {
            SpriteEffects flip = direction < 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            mAniPlayer.Draw(KSceneMgr.SpriteBatch, flip);
        }

        public override void OnKilled(Player killedBy)
        {
            if (!IsDispose)
            {
                if (mObjectState == EObjectState.Ok)
                {
                    PlayerData.Instance.KillEnemy();
                    mAniPlayer.PlayAnimation(DieAnimation);

                    if (killedBy != null)
                    {
                        hitByPlayerSound.Play();
                    }

                    //乌龟是不会被杀死的，除非，自己 跳下悬崖
                    mObjectState = EObjectState.Deading;
                    SetMoveSpeed(0);
                    if (killedBy == null)
                    {
                        mObjectState = EObjectState.Dead;
                        Dispose();
                    }
                }
                else if (mObjectState == EObjectState.Deading)
                {
                    //再次接触 ，给左右一个速度
                    Vector2 depth = RectangleExtensions.GetIntersectionDepth(Collider2DZone, killedBy.Collider2DZone);
                    if (depth.X != 0)
                    {
                        //有了速度之后，他又活了
                        mObjectState = EObjectState.Ok;
                        WorldPosition = new Vector2(WorldPosition.X + depth.X, WorldPosition.Y);
                        SetMoveSpeed(300, depth.X < 0 ? FaceDirection.Left : FaceDirection.Right);
                    }
                }
            }

        }

    }
}