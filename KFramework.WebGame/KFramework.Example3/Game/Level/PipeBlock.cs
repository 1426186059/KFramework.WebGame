
namespace KFramework.Example3
{
    internal class PipeBlock : TileBase
    {
        internal enum EPipeType
        {
            Head,
            Connect,
            Connect2,
        }

        public EPipeType nPipeType;

        public int Count { get; set; }  // 金币数量，默认1
        private SoundEffect collectedSound;
        private Animation CoinAni;
        private AnimationPlayer mCoinAniPlayer;
        public Level Level;

        private const float GravityAcceleration = 3400.0f;
        private Vector2 Velocity;
        private readonly Vector2 HitInitVelocity = new Vector2(0, -300f);
        private Vector2 preBottomPos;

        private Vector2 CoinVelocity;
        private readonly Vector2 CoinHitInitVelocity = new Vector2(0, -600f);
        private Vector2 preCoinBottomPos;
        private SpriteRenderer mSprite = null;
        private SpriteRenderer mSprite_Connect = null;

        public override Rectangle Collider2DZone
        {
            get
            {
                if (nPipeType == EPipeType.Head)
                {
                    Vector2 Size = (mSprite.Sprite.Rectangle.Size.ToVector2() * Tile.TileScale);
                    return new Rectangle(
                        WorldPosition.ToPoint() - (Size * Pivot).ToPoint(),
                        Size.ToPoint());
                }
                else if (nPipeType == EPipeType.Connect)
                {
                    float SizeX = mSprite.Sprite.Rectangle.Size.X * Tile.TileScale.X;
                    float SizeY = (mSprite.Sprite.Rectangle.Size.Y + mSprite_Connect.Sprite.Rectangle.Size.Y) * Tile.TileScale.Y;
                    Vector2 Size = new Vector2(SizeX, SizeY);

                    return new Rectangle(
                        mSprite_Connect.WorldPosition.ToPoint() - (Size * Pivot).ToPoint(),
                        Size.ToPoint());
                }
                else if (nPipeType == EPipeType.Connect2)
                {
                    float SizeX = mSprite.Sprite.Rectangle.Size.X * Tile.TileScale.X;
                    float SizeY = (mSprite.Sprite.Rectangle.Size.Y + mSprite_Connect.Sprite.Rectangle.Size.Y) * Tile.TileScale.Y;
                    Vector2 Size = new Vector2(SizeX, SizeY);

                    return new Rectangle(
                        WorldPosition.ToPoint() - (Size * Pivot).ToPoint(),
                        Size.ToPoint()); ;
                }

                return Rectangle.Empty;
            }

        }

        public PipeBlock(Level level, Vector2 position, EPipeType nPipeType)
        {
            this.Level = level;
            this.WorldPosition = position;
            this.preBottomPos = WorldPosition;
            this.nPipeType = nPipeType;
            this.Pivot = new Vector2(0, 1);
            LoadContent();
        }

        public void LoadContent()
        {
            collectedSound = Level.mContentInstace.LoadSound("MyRes/Sounds/smb_coin");

            if (nPipeType == EPipeType.Head)
            {
                mSprite = new SpriteRenderer();
                mSprite.Sprite = new KSprite(Level.mSpriteSheet_misc3Atlas.Sprite("misc-3_18"));
                mSprite.Parent = this;
                mSprite.Pivot = new Vector2(0, 1);
                mSprite.LocalPosition = Vector2.Zero;
                mSprite.LocalScale = Tile.TileScale;
            }
            else if (nPipeType == EPipeType.Connect)
            {
                mSprite = new SpriteRenderer();
                mSprite_Connect = new SpriteRenderer();
                mSprite.Sprite = new KSprite(Level.mSpriteSheet_misc3Atlas.Sprite("misc-3_18"));
                mSprite_Connect.Sprite = new KSprite(Level.mSpriteSheet_misc3Atlas.Sprite("misc-3_47"));

                mSprite.Parent = this;
                mSprite_Connect.Parent = this;
                mSprite.Pivot = new Vector2(0, 1);
                mSprite_Connect.Pivot = new Vector2(0, 1);

                mSprite_Connect.LocalPosition = new Vector2(0, mSprite_Connect.Sprite.Rectangle.Size.Y * Tile.TileScale.Y * 0.5f);
                mSprite.LocalPosition = new Vector2(0, mSprite_Connect.LocalPosition.Y - mSprite_Connect.Sprite.Rectangle.Size.Y * Tile.TileScale.Y);

                mSprite.LocalScale = Tile.TileScale;
                mSprite_Connect.LocalScale = Tile.TileScale;

            }
            else if (nPipeType == EPipeType.Connect2)
            {
                mSprite = new SpriteRenderer();
                mSprite_Connect = new SpriteRenderer();
                mSprite.Sprite = new KSprite(Level.mSpriteSheet_misc3Atlas.Sprite("misc-3_18"));
                mSprite_Connect.Sprite = new KSprite(Level.mSpriteSheet_misc3Atlas.Sprite("misc-3_47"));

                mSprite.Parent = this;
                mSprite_Connect.Parent = this;

                mSprite.Pivot = new Vector2(0, 1);
                mSprite_Connect.Pivot = new Vector2(0, 1);

                mSprite_Connect.LocalPosition = new Vector2(0, mSprite_Connect.Sprite.Rectangle.Size.Y * Tile.TileScale.Y * 0f);
                mSprite.LocalPosition = new Vector2(0, mSprite_Connect.LocalPosition.Y - mSprite_Connect.Sprite.Rectangle.Size.Y * Tile.TileScale.Y);

                mSprite.LocalScale = Tile.TileScale;
                mSprite_Connect.LocalScale = Tile.TileScale;
            }

            //mCoinAniPlayer.mTransform = new KTransform();
            //mCoinAniPlayer.PlayAnimation(CoinAni);
            //preCoinBottomPos = WorldPosition + (Tile.TileSize - mCoinAniPlayer.FrameSize.ToVector2()) / 2f;
            //mCoinAniPlayer.mTransform.WorldPosition = preCoinBottomPos;
        }

        public void HandleHit(Player WhoHitMe)
        {
            if (Count == 0)
            {
                return;
            }

            PlayerData.Instance.CollectCoin();

            collectedSound.Play();
            Velocity = HitInitVelocity;
            CoinVelocity = CoinHitInitVelocity;
            mCoinAniPlayer.mTransform.activeSelf = true;
        }

        public override void Update()
        {
            //mCoinAniPlayer.Update(gameTime);

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
            mSprite.Draw();
            if(mSprite_Connect != null)
            {
                mSprite_Connect.Draw();
            }
            DrawCollider2DZone();
        }

    }
}