
namespace KFramework.Example3
{
    internal class CoinBlock: TileBase
    {
        public enum EGiftType
        {
            Coin, //金币
            UltimateStar, // 无敌星星
        }
        
        public int Count { get; set; }  // 金币数量，默认1
            
        private SoundEffect collectedSound;

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
        KSprite mSprite;
        private EGiftType nGiftType;
        
        public CoinBlock(Level level, Vector2 position, EGiftType nGiftType, int nCount = 1)
        {
            this.mLevel = level;
            this.WorldPosition = position;
            this.preBottomPos = WorldPosition;
            this.Count = nCount;
            this.nGiftType = nGiftType;
            LoadContent();
        }

        public void LoadContent()
        {
            collectedSound = mLevel.mContentInstace.LoadSound("MyRes/Sounds/smb_coin");
            mSprite = new KSprite(mLevel.mSpriteSheet_misc3Atlas.Sprite("misc-3_13"));

            CoinAni = new Animation(mLevel.mSpriteSheet_misc3Atlas, "misc-3_", 73, 76, 1 / 10f, true);
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
                mSprite = new KSprite(mLevel.mSpriteSheet_misc3Atlas.Sprite("misc-3_30"));
            }

            if (nGiftType == EGiftType.UltimateStar)
            {
                new PowerUp_UltimateStar(mLevel, WorldPosition + new Vector2(Collider2DZone.Size.X / 2, 0));
            }
            else if(nGiftType == EGiftType.Coin)
            {
                PlayerData.Instance.CollectCoin();
                collectedSound.Play();

                Velocity = HitInitVelocity;
                CoinVelocity = CoinHitInitVelocity;
                mCoinAniPlayer.mTransform.activeSelf = true;
            }
        }

        public override void Update()
        {
            mCoinAniPlayer.Update();

            if (Velocity != Vector2.Zero)
            {
                float deltaTime = KTime.deltaTime;
                //速度 = 速度 + 重力加速度 × 时间
                //位置 = 位置 + 速度 × 时间
                Velocity.Y += GravityAcceleration * deltaTime;
                WorldPosition += Velocity * deltaTime;

                if(WorldPosition.Y > preBottomPos.Y)
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

            Rectangle targetRegion = new Rectangle(WorldPosition.ToPoint(), Tile.TileSize.ToPoint());
            KSceneMgr.SpriteBatch.Draw(
                mSprite.Texture,
                targetRegion,
                mSprite.Rectangle,
                Color.White,
                0.0f,
                Vector2.Zero,
                SpriteEffects.None,
                0.0f);

            DrawCollider2DZone();
        }

    }
}