
namespace KFramework.Example3
{
    //板栗仔
    internal class Enemy_Goomba:EnemyBase
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

        public Enemy_Goomba(Level level, Vector2 position):base(level, position, 100)
        {
            LoadContent();
        }

        public void LoadContent()
        {
            WalkAnimation = new Animation(mLevel.mSpriteSheet_charactersAtlas, "characters_", 159, 160, 1 / 10f, true, true);
            DieAnimation = new Animation(mLevel.mSpriteSheet_charactersAtlas, "characters_", 158, 158, 1 / 10f, false);
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
            SpriteEffects flip = direction > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            mAniPlayer.Draw(KSceneMgr.SpriteBatch, flip);
        }

        public override void OnKilled(Player killedBy)
        {
            if (!IsDispose && mObjectState == EObjectState.Ok)
            {
                PlayerData.Instance.KillEnemy();
                mAniPlayer.PlayAnimation(DieAnimation);

                if (killedBy != null)
                {
                    hitByPlayerSound.Play();
                }

                mObjectState = EObjectState.Deading;
                KTween.delayedCall(this, 0.2f, () =>
                {
                    mObjectState = EObjectState.Dead;
                    Dispose();
                });
            }
        }

    }
}