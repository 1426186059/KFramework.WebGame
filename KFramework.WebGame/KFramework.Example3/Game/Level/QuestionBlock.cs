
namespace KFramework.Example3
{
    internal class QuestionBlock : TileBase
    {
        public enum EGiftType
        {
            Coin, //金币
            Mushroom, //蘑菇（超级蘑菇）
            OneUpMushroom, //就是一个加命蘑菇道具
            FireFlower, //火花
        }

        public EGiftType nGiftType { get; set; }
        public int Count { get; set; }  // 金币数量，默认1
        public bool IsUsed { get { return Count == 0; } }    // 是否已经被顶过

        private SoundEffect collectedSound;
        public readonly Color Color = Color.Green;

        Animation WaitCollectAni;
        Animation CollectFinishAni;
        AnimationPlayer mAniPlayer;

        Animation CoinAni;
        AnimationPlayer mCoinAniPlayer;
        public Level mLevel;

        private const float GravityAcceleration = 3400.0f;
        private Vector2 Velocity;
        private readonly Vector2 HitInitVelocity = new Vector2(0, -300f);
        private Vector2 preBottomPos;

        private Vector2 CoinVelocity;
        private readonly Vector2 CoinHitInitVelocity = new Vector2(0, -600f);
        private Vector2 preCoinBottomPos;

        public Rectangle BoundingRectangle
        {
            get
            {
                return new Rectangle(WorldPosition.ToPoint(), Tile.TileSize.ToPoint());
            }
        }

        public QuestionBlock(Level level, Vector2 position, QuestionBlock.EGiftType GiftType, int nCount)
        {
            this.mLevel = level;
            this.WorldPosition = BeginPos = position;
            this.preBottomPos = WorldPosition;
            this.nGiftType = GiftType;
            this.Count = nCount;
            LoadContent();
        }

        public void LoadContent()
        {
            collectedSound = mLevel.mContentInstace.LoadSound("MyRes/Sounds/smb_coin");
            WaitCollectAni = new Animation(mLevel.mSpriteSheet_misc3Atlas, "misc-3_", 80, 82, 1 / 10f, true);
            CollectFinishAni = new Animation(mLevel.mSpriteSheet_misc3Atlas, "misc-3_", 30, 30, 1 / 10f, false);
            CoinAni = new Animation(mLevel.mSpriteSheet_misc3Atlas, "misc-3_", 73, 76, 1 / 10f, true);
            mAniPlayer.mTransform = this;
            mAniPlayer.PlayAnimation(WaitCollectAni);

            mCoinAniPlayer.mTransform = new KTransform();
            mCoinAniPlayer.PlayAnimation(CoinAni);
            preCoinBottomPos = WorldPosition + (Tile.TileSize - mCoinAniPlayer.FrameSize.ToVector2()) / 2f;
            mCoinAniPlayer.mTransform.WorldPosition = preCoinBottomPos;
        }

        public void HandleHit(Player WhoHitMe)
        {
            if (Count == 0)
            {
                return;
            }
            
            if (--Count <= 0)
            {
                mAniPlayer.PlayAnimation(CollectFinishAni);
            }

            collectedSound.Play();
            Velocity = HitInitVelocity;

            switch (nGiftType)
            {
                case EGiftType.Coin:
                    PlayerData.Instance.CollectCoin();
                    mCoinAniPlayer.mTransform.activeSelf = true;
                    CoinVelocity = CoinHitInitVelocity;
                    break;
                case EGiftType.Mushroom:
                    new PowerUp_Mushroom(mLevel, WorldPosition + new Vector2(mAniPlayer.FrameSize.X / 2, 0));
                    break;
                case EGiftType.OneUpMushroom:
                    new PowerUp_OneUpMushroom(mLevel, WorldPosition + new Vector2(mAniPlayer.FrameSize.X / 2, 0));
                    break;
                case EGiftType.FireFlower:
                    new PowerUp_FireFlower(mLevel, WorldPosition + new Vector2(mAniPlayer.FrameSize.X / 2, 0));
                    break;
            }

        }

        public override void Update()
        {
            mAniPlayer.Update();
            mCoinAniPlayer.Update();

            if (Velocity != Vector2.Zero)
            {
                float deltaTime = KTime.deltaTime;
                //速度 = 速度 + 重力加速度 × 时间
                //位置 = 位置 + 速度 × 时间
                Velocity.Y += GravityAcceleration * deltaTime;
                WorldPosition += Velocity * deltaTime;

                if (WorldPosition.Y > preBottomPos.Y)
                {
                    WorldPosition = preBottomPos;
                    Velocity = Vector2.Zero;
                }
            }

            if (CoinVelocity != Vector2.Zero)
            {
                float deltaTime = KTime.deltaTime;
                //速度 = 速度 + 重力加速度 × 时间
                //位置 = 位置 + 速度 × 时间
                CoinVelocity.Y += GravityAcceleration * deltaTime;
                mCoinAniPlayer.mTransform.WorldPosition += CoinVelocity * deltaTime;

                if (mCoinAniPlayer.mTransform.WorldPosition.Y > preCoinBottomPos.Y)
                {
                    mCoinAniPlayer.mTransform.WorldPosition = preCoinBottomPos;
                    CoinVelocity = Vector2.Zero;
                    mCoinAniPlayer.mTransform.activeSelf = false;
                }
            }

        }

        public override void Draw()
        {
            if (mCoinAniPlayer.mTransform.activeSelf)
            {
                mCoinAniPlayer.Draw(KSceneMgr.SpriteBatch, SpriteEffects.None);
            }
            mAniPlayer.Draw( KSceneMgr.SpriteBatch, SpriteEffects.None);
            DrawCollider2DZone();
        }

    }
}